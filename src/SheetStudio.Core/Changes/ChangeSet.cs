using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Changes;

/// <summary>
/// Immutable staged edits: per-sheet changes plus the panel state (order, locks, discipline order).
/// Ordering ops need the snapshot list and profile to know each sheet's effective group.
/// <see cref="Touched"/> holds sheets whose position changed, <see cref="Relocked"/> those whose lock was toggled;
/// both count as pending. Only groups touched by the user are renumbered unless <see cref="NormalizeAll"/> is set.
/// </summary>
public sealed record ChangeSet(ProjectState State, IReadOnlyDictionary<string, SheetChange> Changes, IReadOnlySet<string> Touched)
{
    public static ChangeSet Start(ProjectState state) =>
        new(state, new Dictionary<string, SheetChange>(), new HashSet<string>());

    public IReadOnlySet<string> Relocked { get; init; } = new HashSet<string>();

    /// <summary>"Rapikan semua nomor": renumber every group, not only the ones the user touched.</summary>
    public bool NormalizeAll { get; init; }

    public int PendingCount => Changes.Keys.Union(Touched).Union(Relocked).Count() + (NormalizeAll ? 1 : 0);

    /// <summary>Groups whose numbers must be recomputed: source and target groups of moved, regrouped,
    /// manually numbered or deleted sheets. Null = all groups.</summary>
    public IReadOnlySet<string>? DirtyGroups(IReadOnlyList<SheetSnapshot> sheets, NamingProfile p)
    {
        if (NormalizeAll) return null;
        var byId = sheets.ToDictionary(s => s.UniqueId);
        var ids = Touched.Concat(Changes.Values
            .Where(c => c.NewDiscipline is not null || c.NewType is not null || c.ManualSeq is not null || c.Delete)
            .Select(c => c.UniqueId));
        var dirty = new HashSet<string>();
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var s)) continue;
            dirty.Add(EffectiveGroup(s, p));
            dirty.Add(LayerListBuilder.GroupOf(s.Number, p).Type);
        }
        return dirty;
    }
    public bool IsEmpty => PendingCount == 0;

    /// <summary>Type group key after staged discipline/type changes, e.g. "A|1".</summary>
    public string EffectiveGroup(SheetSnapshot s, NamingProfile p)
    {
        var (disc, type) = LayerListBuilder.GroupOf(s.Number, p);
        if (!Changes.TryGetValue(s.UniqueId, out var c) || (c.NewDiscipline is null && c.NewType is null)) return type;
        var parsed = SheetId.Parse(s.Number, p);
        var d = c.NewDiscipline ?? (parsed.Ok ? parsed.Id.Discipline : disc);
        var t = c.NewType ?? (parsed.Ok ? parsed.Id.Type : 0);
        return LayerListBuilder.TypeKey(d, t);
    }

    /// <summary>Snapshots re-numbered into their effective groups (sequence kept) so the layer builder groups them right.</summary>
    public IReadOnlyList<SheetSnapshot> EffectiveSheets(IReadOnlyList<SheetSnapshot> sheets, NamingProfile p) =>
        sheets.Select(s =>
        {
            if (!Changes.TryGetValue(s.UniqueId, out var c) || (c.NewDiscipline is null && c.NewType is null)) return s;
            var g = EffectiveGroup(s, p).Split('|');
            var parsed = SheetId.Parse(s.Number, p);
            var seq = parsed.Ok && parsed.Id.Seq > 0 ? parsed.Id.Seq : 1;
            return s with { Number = new SheetId(g[0], int.Parse(g[1]), seq).Format() };
        }).ToList();

    /// <summary>Ordered sheet ids of a type group as the panel shows them.</summary>
    public IReadOnlyList<string> GroupOrder(string group, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p) =>
        LayerListBuilder.OrderedSheets(EffectiveSheets(sheets, p), State, p)
            .Where(s => LayerListBuilder.GroupOf(s.Number, p).Type == group) // numbers already effective
            .Select(s => s.UniqueId).ToList();

    public OpResult MoveSelection(IReadOnlyList<string> ids, string targetGroup, int targetIndex, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p)
    {
        if (targetGroup == LayerListBuilder.UnknownType || targetGroup == LayerListBuilder.UnknownDiscipline)
            return new OpResult(this, "Tidak bisa memindah ke grup nomor tidak standar");

        var (movable, locked) = SplitMovable(ids, sheets, p);
        var pesan = locked > 0 ? $"{locked} lembar terkunci tidak dipindah" : null;
        if (movable.Count == 0) return new OpResult(this, pesan ?? "Tidak ada lembar yang bisa dipindah");

        var byId = sheets.ToDictionary(s => s.UniqueId);
        var panel = PanelOrder(sheets, p);
        movable = movable.OrderBy(id => panel.IndexOf(id)).ToList();

        var changes = Changes.ToDictionary(kv => kv.Key, kv => kv.Value);
        var order = State.OrderPerGroup.ToDictionary(kv => kv.Key, kv => kv.Value);

        // Target group per sheet: a discipline header keeps each sheet's own type.
        string TargetOf(string id)
        {
            if (targetGroup.Contains('|')) return targetGroup;
            var own = EffectiveGroup(byId[id], p).Split('|')[1];
            return LayerListBuilder.TypeKey(targetGroup, int.Parse(own));
        }

        var groupsBefore = movable.Select(id => EffectiveGroup(byId[id], p)).ToList();
        var targets = movable.Select(TargetOf).ToList();

        // Insertion index counts moved items that sit above it in the target group.
        var targetList = targetGroup.Contains('|') ? GroupOrder(targetGroup, sheets, p).ToList() : [];
        var adjusted = targetIndex - targetList.Take(Math.Max(0, targetIndex)).Count(movable.Contains);

        foreach (var src in groupsBefore.Distinct()) // remove all moved sheets from their source groups
            order[src] = GroupOrder(src, sheets, p).Where(x => !movable.Contains(x)).ToList();
        for (var i = 0; i < movable.Count; i++) // regroup
        {
            var id = movable[i];
            var original = LayerListBuilder.GroupOf(byId[id].Number, p).Type;
            var t = targets[i];
            var c = changes.TryGetValue(id, out var existing) ? existing : new SheetChange(id);
            if (t == original) c = c with { NewDiscipline = null, NewType = null };
            else
            {
                var parts = t.Split('|');
                c = c with { NewDiscipline = parts[0], NewType = int.Parse(parts[1]) };
            }
            if (c.IsEmpty) changes.Remove(id); else changes[id] = c;
        }

        var staged = this with { Changes = changes, State = State with { OrderPerGroup = order } };
        foreach (var grp in targets.Distinct())
        {
            var members = staged.GroupOrder(grp, sheets, p).Where(x => !movable.Contains(x)).ToList();
            var incoming = movable.Where((_, i) => targets[i] == grp).ToList();
            var at = targetGroup.Contains('|') ? Math.Clamp(adjusted, 0, members.Count) : members.Count;
            members.InsertRange(at, incoming);
            order[grp] = members;
        }

        var next = staged with { State = State with { OrderPerGroup = order } };
        var changedPos = movable.Where((id, i) => groupsBefore[i] != targets[i] ||
                                                   next.PanelOrder(sheets, p).IndexOf(id) != panel.IndexOf(id)).ToList();
        if (changedPos.Count == 0 && changes.Count == Changes.Count) return new OpResult(this, pesan);
        return new OpResult(next with { Touched = Touched.Union(movable).ToHashSet() }, pesan);
    }

    public OpResult MoveBy(IReadOnlyList<string> ids, int delta, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p) =>
        Reorder(ids, sheets, p, (list, sel) =>
        {
            var steps = Math.Abs(delta);
            for (var s = 0; s < steps; s++)
            {
                if (delta < 0)
                {
                    for (var i = 1; i < list.Count; i++)
                        if (sel.Contains(list[i]) && !sel.Contains(list[i - 1])) (list[i], list[i - 1]) = (list[i - 1], list[i]);
                }
                else
                {
                    for (var i = list.Count - 2; i >= 0; i--)
                        if (sel.Contains(list[i]) && !sel.Contains(list[i + 1])) (list[i], list[i + 1]) = (list[i + 1], list[i]);
                }
            }
        });

    public OpResult MoveToEdge(IReadOnlyList<string> ids, bool top, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p) =>
        Reorder(ids, sheets, p, (list, sel) =>
        {
            var picked = list.Where(sel.Contains).ToList();
            var rest = list.Where(x => !sel.Contains(x)).ToList();
            list.Clear();
            list.AddRange(top ? picked.Concat(rest) : rest.Concat(picked));
        });

    public OpResult MoveDisciplineGroup(string disc, int targetIndex, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p)
    {
        var current = LayerListBuilder.OrderedSheets(EffectiveSheets(sheets, p), State, p)
            .Select(s => EffectiveGroup(s, p).Split('|')[0])
            .Where(d => d != LayerListBuilder.UnknownDiscipline).Distinct().ToList();
        if (!current.Remove(disc)) return new OpResult(this, null);
        current.Insert(Math.Clamp(targetIndex, 0, current.Count), disc);
        var ranks = current.Select(d => p.FindDiscipline(d)?.Order ?? 99).ToList();
        var inUdsOrder = ranks.Zip(ranks.Skip(1)).All(x => x.First <= x.Second);
        var next = this with { State = State with { DisciplineOrder = current } };
        return new OpResult(next, inUdsOrder ? null : "Urutan disiplin tidak sesuai urutan UDS");
    }

    public OpResult ToggleLock(IReadOnlyList<string> ids)
    {
        var locked = State.Locked.ToHashSet();
        var allLocked = ids.All(locked.Contains);
        foreach (var id in ids) { if (allLocked) locked.Remove(id); else locked.Add(id); }
        var relocked = Relocked.ToHashSet();
        foreach (var id in ids) if (!relocked.Remove(id)) relocked.Add(id); // toggling twice is no change
        return new OpResult(this with { State = State with { Locked = locked }, Relocked = relocked }, null);
    }

    public OpResult SetInSheetList(IReadOnlyList<string> ids, bool value) =>
        ids.Aggregate(new OpResult(this, null), (r, id) => r.Next.Upsert(new SheetChange(id, InSheetList: value)));

    public OpResult MarkDelete(IReadOnlyList<string> ids, bool value)
    {
        var changes = Changes.ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var id in ids)
        {
            var c = (changes.TryGetValue(id, out var e) ? e : new SheetChange(id)) with { Delete = value };
            if (c.IsEmpty) changes.Remove(id); else changes[id] = c;
        }
        return new OpResult(this with { Changes = changes }, null);
    }

    public OpResult Upsert(SheetChange change)
    {
        var changes = Changes.ToDictionary(kv => kv.Key, kv => kv.Value);
        var merged = changes.TryGetValue(change.UniqueId, out var e) ? e.Merge(change) : change;
        if (merged.IsEmpty) changes.Remove(change.UniqueId); else changes[change.UniqueId] = merged;
        return new OpResult(this with { Changes = changes }, null);
    }

    /// <summary>All sheet ids in panel order.</summary>
    public List<string> PanelOrder(IReadOnlyList<SheetSnapshot> sheets, NamingProfile p) =>
        LayerListBuilder.OrderedSheets(EffectiveSheets(sheets, p), State, p).Select(s => s.UniqueId).ToList();

    private (List<string> Movable, int Locked) SplitMovable(IReadOnlyList<string> ids, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p)
    {
        var byId = sheets.ToDictionary(s => s.UniqueId);
        var movable = new List<string>();
        var locked = 0;
        foreach (var id in ids.Distinct())
        {
            if (!byId.TryGetValue(id, out var s)) continue; // header rows and unknown keys
            if (!s.Editable || State.Locked.Contains(id) || EffectiveGroup(s, p) == LayerListBuilder.UnknownType) locked++;
            else movable.Add(id);
        }
        return (movable, locked);
    }

    private OpResult Reorder(IReadOnlyList<string> ids, IReadOnlyList<SheetSnapshot> sheets, NamingProfile p, Action<List<string>, HashSet<string>> op)
    {
        var (movable, locked) = SplitMovable(ids, sheets, p);
        var pesan = locked > 0 ? $"{locked} lembar terkunci tidak dipindah" : null;
        var byId = sheets.ToDictionary(s => s.UniqueId);
        var sel = movable.ToHashSet();
        var order = State.OrderPerGroup.ToDictionary(kv => kv.Key, kv => kv.Value);
        var moved = new HashSet<string>();
        foreach (var grp in movable.Select(id => EffectiveGroup(byId[id], p)).Distinct())
        {
            var before = GroupOrder(grp, sheets, p).ToList();
            var list = before.ToList();
            op(list, sel);
            if (!list.SequenceEqual(before)) { order[grp] = list; moved.UnionWith(list.Where(sel.Contains)); }
        }
        if (moved.Count == 0) return new OpResult(this, pesan);
        return new OpResult(this with { State = State with { OrderPerGroup = order }, Touched = Touched.Union(moved).ToHashSet() }, pesan);
    }
}
