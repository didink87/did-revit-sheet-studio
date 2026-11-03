namespace SheetStudio.Core.Model;

/// <summary>Read-only picture of one Revit sheet at snapshot time. <see cref="Versi"/> detects later edits.</summary>
public sealed record SheetSnapshot(
    string UniqueId,
    string Number,
    string Title,
    string? KindId,
    string Subjek,
    bool InSheetList,
    bool Editable,
    string? BorrowedBy,
    string Versi,
    IReadOnlyDictionary<string, string> Params,
    string TitleBlockType)
{
    public static string MakeVersi(string number, string title) => number + "|" + title;
}

/// <summary>Panel order persisted per project. Group keys: discipline "A", type "A|1".</summary>
public sealed record ProjectState(
    IReadOnlyDictionary<string, IReadOnlyList<string>> OrderPerGroup,
    IReadOnlyList<string> DisciplineOrder,
    IReadOnlySet<string> Locked)
{
    public static ProjectState Empty { get; } = new(new Dictionary<string, IReadOnlyList<string>>(), [], new HashSet<string>());
}
