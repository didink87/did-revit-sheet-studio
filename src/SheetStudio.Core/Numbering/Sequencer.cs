using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Numbering;

public enum Strategy { Rentang, Urut }

public sealed record NumberingOptions(Strategy Strategy, int Mulai, int Langkah)
{
    public static NumberingOptions Default { get; } = new(Strategy.Rentang, 1, 1);
}

public sealed record NumberResult(string UniqueId, string OldNumber, string NewNumber, string SortKey, bool Changed);

public sealed record SequenceOutcome(IReadOnlyList<NumberResult> Results, IReadOnlyList<string> Errors);

/// <summary>Derives every sheet number from the panel order (plus locks and manual numbers).</summary>
public static class Sequencer
{
    public static SequenceOutcome Compute(IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs, NamingProfile p, NumberingOptions o)
    {
        var errors = new List<string>();
        var results = new List<NumberResult>();
        var original = sheets.ToDictionary(s => s.UniqueId, s => s.Number);
        var dirty = cs.DirtyGroups(sheets, p);
        var ordered = LayerListBuilder.OrderedSheets(cs.EffectiveSheets(sheets, p), cs.State, p)
            .Where(s => !(cs.Changes.TryGetValue(s.UniqueId, out var c) && c.Delete))
            .ToList();

        foreach (var group in ordered.GroupBy(s => LayerListBuilder.GroupOf(s.Number, p).Type))
        {
            var members = group.ToList();
            if (group.Key == LayerListBuilder.UnknownType)
            {
                results.AddRange(members.Select(s => new NumberResult(s.UniqueId, original[s.UniqueId], original[s.UniqueId],
                    "99.?.?." + members.IndexOf(s).ToString("0000"), false)));
                continue;
            }

            var parts = group.Key.Split('|');
            var disc = parts[0];
            var type = int.Parse(parts[1]);
            var assigned = dirty is null || dirty.Contains(group.Key) ? AssignGroup(disc, type, members, cs, p, o, errors) : null;
            for (var i = 0; i < members.Count; i++)
            {
                var s = members[i];
                var newNumber = assigned?[s.UniqueId] ?? original[s.UniqueId];
                var id = SheetId.Parse(newNumber, p);
                var key = id.Ok ? SortKey.For(id.Id, p, i) : SortKey.For(new SheetId(disc, type, 0), p, i);
                results.Add(new NumberResult(s.UniqueId, original[s.UniqueId], newNumber, key,
                    !string.Equals(newNumber, original[s.UniqueId], StringComparison.Ordinal)));
            }
        }
        return new SequenceOutcome(results, errors);
    }

    /// <summary>Would dropping <paramref name="ids"/> at the end of <paramref name="targetGroup"/> number cleanly?</summary>
    public static bool CanAccept(IReadOnlyList<string> ids, string targetGroup, IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs,
        NamingProfile p, NumberingOptions o, out string? reason)
    {
        var moved = cs.MoveSelection(ids, targetGroup, int.MaxValue / 2, sheets, p);
        if (ReferenceEquals(moved.Next, cs)) { reason = moved.Pesan; return moved.Pesan is null; } // no-op drop is fine
        var outcome = Compute(sheets, moved.Next, p, o);
        var label = targetGroup.Contains('|') ? GroupLabel(targetGroup) : targetGroup;
        reason = outcome.Errors.FirstOrDefault(e => e.Contains(label));
        return reason is null;
    }

    public static string GroupLabel(string typeKey)
    {
        var parts = typeKey.Split('|');
        return parts[0].Length == 1 ? $"{parts[0]}-{parts[1]}" : $"{parts[0]}{parts[1]}";
    }

    /// <summary>Range of sequences a sheet may take: its kind's range when the kind belongs to this group.</summary>
    public static (int From, int To) RangeFor(string disc, int type, string? kindId, NamingProfile p, NumberingOptions o)
    {
        var kind = p.FindKind(kindId);
        if (o.Strategy == Strategy.Rentang && kind is not null && kind.Disiplin == disc && kind.Tipe == type)
            return (kind.SeqFrom, kind.SeqTo);
        return (disc == "G" && type == 0 ? 0 : 1, 99);
    }

    private static Dictionary<string, string>? AssignGroup(string disc, int type, List<SheetSnapshot> members, ChangeSet cs,
        NamingProfile p, NumberingOptions o, List<string> errors)
    {
        var label = GroupLabel(LayerListBuilder.TypeKey(disc, type));
        var result = new Dictionary<string, string>();
        var used = new HashSet<int>();
        var free = new List<SheetSnapshot>();

        bool IsFixed(SheetSnapshot s) => !s.Editable || cs.State.Locked.Contains(s.UniqueId);
        foreach (var s in members.Where(IsFixed)) // fixed sheets keep their numbers
        {
            var id = SheetId.Parse(s.Number, p);
            if (id.Ok) used.Add(id.Id.Seq);
            result[s.UniqueId] = s.Number;
        }
        foreach (var s in members.Where(s => !IsFixed(s)))
        {
            var manual = cs.Changes.TryGetValue(s.UniqueId, out var c) ? c.ManualSeq : null;
            if (manual is int m)
            {
                if (m is >= 0 and <= 99 && used.Add(m)) { result[s.UniqueId] = new SheetId(disc, type, m).Format(); continue; }
                errors.Add($"Number {new SheetId(disc, type, m).Format()} is already in use — numbered automatically");
            }
            free.Add(s);
        }

        if (o.Strategy == Strategy.Rentang)
        {
            var cursors = new Dictionary<(int, int), int>();
            foreach (var s in free)
            {
                var range = RangeFor(disc, type, s.KindId, p, o);
                var seq = cursors.TryGetValue(range, out var cur) ? cur : range.From;
                while (seq <= range.To && used.Contains(seq)) seq++;
                if (seq > range.To)
                {
                    errors.Add($"Range {label} {range.From:00}–{range.To:00} is full; change the range in the profile or use a second letter");
                    return null;
                }
                used.Add(seq);
                cursors[range] = seq + 1;
                result[s.UniqueId] = new SheetId(disc, type, seq).Format();
            }
        }
        else
        {
            var start = disc == "G" && type == 0 ? 0 : o.Mulai;
            var step = Math.Max(1, o.Langkah);
            var last = -1;
            for (var k = 0; k < free.Count; k++)
            {
                var seq = Math.Max(start + k * step, last + 1);
                while (seq <= 99 && used.Contains(seq)) seq++;
                if (seq > 99)
                {
                    errors.Add($"Group {label} is full (more than 99 sheets); reduce the step or use a second letter");
                    return null;
                }
                used.Add(seq);
                last = seq;
                result[free[k].UniqueId] = new SheetId(disc, type, seq).Format();
            }
        }
        return result;
    }
}
