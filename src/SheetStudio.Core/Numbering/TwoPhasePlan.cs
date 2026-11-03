namespace SheetStudio.Core.Numbering;

public sealed record RenumberStep(string UniqueId, string Temp, string Final);

/// <summary>
/// Swapping numbers in Revit needs a temporary number first, or the second sheet collides.
/// Temps look like "SSTMP-1A2B3C-001": unique per apply, not UDS, and free of the characters
/// Revit forbids in sheet numbers ({ } [ ] | ; &lt; &gt; ? ` ~).
/// </summary>
public static class TwoPhasePlan
{
    public static IReadOnlyList<RenumberStep> Build(IEnumerable<NumberResult> results, string? nonce = null)
    {
        nonce ??= Random.Shared.Next(0, 0x1000000).ToString("X6");
        return results.Where(r => r.Changed)
            .Select((r, i) => new RenumberStep(r.UniqueId, $"SSTMP-{nonce}-{i + 1:000}", r.NewNumber))
            .ToList();
    }
}
