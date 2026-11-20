using SheetStudio.Core.Naming;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Create;

public static class BatchMatrix
{
    /// <summary>Per-level kinds give one row per level (lowest elevation first); other kinds one row.</summary>
    public static IReadOnlyList<BatchRow> Build(IEnumerable<string> kindIds, IEnumerable<(string Name, double Elevation)> levels,
        NamingProfile p, Kertas k, bool buatView)
    {
        var sorted = levels.OrderBy(l => l.Elevation).ToList();
        var rows = new List<BatchRow>();
        foreach (var id in kindIds)
        {
            var kind = p.FindKind(id) ?? throw new ArgumentException($"Kind '{id}' is not in the profile", nameof(kindIds));
            if (kind.PerLevel)
                rows.AddRange(sorted.Select(l => new BatchRow(kind.Id, TitleRules.Normalize(l.Name), l.Name, null, null, 0, k, buatView)));
            else
                rows.Add(new BatchRow(kind.Id, "", null, null, null, 0, k, buatView));
        }
        return rows;
    }
}
