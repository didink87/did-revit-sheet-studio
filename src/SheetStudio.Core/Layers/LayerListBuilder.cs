using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Layers;

public static class LayerListBuilder
{
    public const string UnknownDiscipline = "?";
    public const string UnknownType = "?|?";

    public static string TypeKey(string disc, int type) => $"{disc}|{type}";

    public static IReadOnlyList<LayerRow> Build(
        IReadOnlyList<SheetSnapshot> sheets, ProjectState state, NamingProfile p,
        IReadOnlySet<string> collapsed, string? filter)
    {
        var rows = new List<LayerRow>();
        var hasFilter = !string.IsNullOrWhiteSpace(filter);
        foreach (var disc in Group(sheets, state, p))
        {
            var types = disc.Types
                .Select(t => (t.Key, t.Label, Sheets: hasFilter ? t.Sheets.Where(s => Matches(s, filter!)).ToList() : t.Sheets))
                .Where(t => t.Sheets.Count > 0)
                .ToList();
            if (types.Count == 0) continue;

            var discOpen = !collapsed.Contains(disc.Key);
            var discSheets = types.SelectMany(t => t.Sheets).ToList();
            rows.Add(new LayerRow(RowKind.Disiplin, disc.Key, 0, disc.Label, disc.Key, discSheets.Count,
                discSheets.All(s => IsLocked(s, state, disc.Key)), discSheets.All(s => s.InSheetList), discOpen, true, null, null));
            if (!discOpen) continue;

            foreach (var t in types)
            {
                var typeOpen = !collapsed.Contains(t.Key);
                rows.Add(new LayerRow(RowKind.Tipe, t.Key, 1, t.Label, t.Key.Split('|')[1], t.Sheets.Count,
                    t.Sheets.All(s => IsLocked(s, state, disc.Key)), t.Sheets.All(s => s.InSheetList), typeOpen, true, null, disc.Key));
                if (!typeOpen) continue;
                foreach (var s in t.Sheets)
                    rows.Add(new LayerRow(RowKind.Lembar, s.UniqueId, 2, s.Title, s.Number, 0, IsLocked(s, state, disc.Key),
                        s.InSheetList, true, s.Editable, s.UniqueId, t.Key));
            }
        }
        return rows;
    }

    /// <summary>All sheets flattened in panel order (ignores filter and collapse).</summary>
    public static IReadOnlyList<SheetSnapshot> OrderedSheets(IReadOnlyList<SheetSnapshot> sheets, ProjectState state, NamingProfile p) =>
        Group(sheets, state, p).SelectMany(d => d.Types).SelectMany(t => t.Sheets).ToList();

    /// <summary>Group key of a sheet number: ("A", "A|1"), or the unknown group.</summary>
    public static (string Disc, string Type) GroupOf(string number, NamingProfile p)
    {
        var r = SheetId.Parse(number, p);
        return r.Ok ? (r.Id.Discipline, TypeKey(r.Id.Discipline, r.Id.Type)) : (UnknownDiscipline, UnknownType);
    }

    private sealed record TypeGroup(string Key, string Label, List<SheetSnapshot> Sheets);
    private sealed record DiscGroup(string Key, string Label, List<TypeGroup> Types);

    private static List<DiscGroup> Group(IReadOnlyList<SheetSnapshot> sheets, ProjectState state, NamingProfile p)
    {
        var byType = new Dictionary<string, List<SheetSnapshot>>();
        var discOf = new Dictionary<string, string>();
        foreach (var s in sheets)
        {
            var (d, t) = GroupOf(s.Number, p);
            if (!byType.TryGetValue(t, out var list)) byType[t] = list = [];
            list.Add(s);
            discOf[t] = d;
        }

        var result = new List<DiscGroup>();
        foreach (var disc in discOf.Values.Distinct().OrderBy(d => DiscRank(d, state, p)).ThenBy(d => d, StringComparer.Ordinal))
        {
            var types = byType.Keys.Where(k => discOf[k] == disc)
                .OrderBy(k => k == UnknownType ? int.MaxValue : int.Parse(k.Split('|')[1]))
                .Select(k => new TypeGroup(k, TypeLabel(k, p), OrderWithin(byType[k], k, state)))
                .ToList();
            result.Add(new DiscGroup(disc, DiscLabel(disc, p), types));
        }
        return result;
    }

    private static List<SheetSnapshot> OrderWithin(List<SheetSnapshot> sheets, string groupKey, ProjectState state)
    {
        var saved = state.OrderPerGroup.TryGetValue(groupKey, out var o) ? o : [];
        var rank = new Dictionary<string, int>();
        for (var i = 0; i < saved.Count; i++) rank.TryAdd(saved[i], i);
        return sheets
            .OrderBy(s => rank.TryGetValue(s.UniqueId, out var r) ? r : int.MaxValue)
            .ThenBy(s => s.Number, StringComparer.Ordinal)
            .ToList();
    }

    private static int DiscRank(string disc, ProjectState state, NamingProfile p)
    {
        if (disc == UnknownDiscipline) return int.MaxValue;
        var custom = state.DisciplineOrder.ToList().IndexOf(disc);
        if (custom >= 0) return custom - 1000; // custom order wins over profile order
        return p.FindDiscipline(disc)?.Order ?? 999;
    }

    private static string DiscLabel(string disc, NamingProfile p)
    {
        if (disc == UnknownDiscipline) return "TIDAK DIKENAL";
        var d = p.FindDiscipline(disc)!;
        if (disc.Length == 2)
        {
            var second = d.HurufKedua.FirstOrDefault(h => h.Huruf == disc[1]);
            return (second?.Nama ?? d.NamaId).ToUpperInvariant();
        }
        return d.NamaId.ToUpperInvariant();
    }

    private static string TypeLabel(string typeKey, NamingProfile p)
    {
        if (typeKey == UnknownType) return "NOMOR TIDAK STANDAR";
        var digit = int.Parse(typeKey.Split('|')[1]);
        var t = p.Tipe.FirstOrDefault(x => x.Digit == digit);
        return t is null ? digit.ToString() : t.Keterangan.Split(':')[0].TrimEnd('.').ToUpperInvariant();
    }

    private static bool IsLocked(SheetSnapshot s, ProjectState state, string disc) =>
        disc == UnknownDiscipline || !s.Editable || state.Locked.Contains(s.UniqueId);

    private static bool Matches(SheetSnapshot s, string filter) =>
        s.Number.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase) ||
        s.Title.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
}
