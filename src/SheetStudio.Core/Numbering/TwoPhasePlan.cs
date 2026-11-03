namespace SheetStudio.Core.Numbering;

public sealed record RenumberStep(string UniqueId, string Temp, string Final);

/// <summary>Swapping numbers in Revit needs a temporary number first, or the second sheet collides.</summary>
public static class TwoPhasePlan
{
    public static IReadOnlyList<RenumberStep> Build(IEnumerable<NumberResult> results) =>
        results.Where(r => r.Changed)
            .Select((r, i) => new RenumberStep(r.UniqueId, $"~TMP-{i + 1:000}", r.NewNumber))
            .ToList();
}
