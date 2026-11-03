namespace SheetStudio.Core.Changes;

/// <summary>Staged edits for one sheet. Null = unchanged.</summary>
public sealed record SheetChange(
    string UniqueId,
    string? NewDiscipline = null,
    int? NewType = null,
    int? ManualSeq = null,
    string? NewTitle = null,
    bool? InSheetList = null,
    string? NewTitleBlockType = null,
    IReadOnlyDictionary<string, string>? NewParams = null,
    bool Delete = false)
{
    public bool IsEmpty =>
        NewDiscipline is null && NewType is null && ManualSeq is null && NewTitle is null && InSheetList is null &&
        NewTitleBlockType is null && (NewParams is null || NewParams.Count == 0) && !Delete;

    /// <summary>Fields set in <paramref name="other"/> win; params are merged.</summary>
    public SheetChange Merge(SheetChange other)
    {
        var merged = NewParams is null && other.NewParams is null
            ? null
            : (NewParams ?? new Dictionary<string, string>()).Concat(other.NewParams ?? new Dictionary<string, string>())
                .GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Last().Value);
        return this with
        {
            NewDiscipline = other.NewDiscipline ?? NewDiscipline,
            NewType = other.NewType ?? NewType,
            ManualSeq = other.ManualSeq ?? ManualSeq,
            NewTitle = other.NewTitle ?? NewTitle,
            InSheetList = other.InSheetList ?? InSheetList,
            NewTitleBlockType = other.NewTitleBlockType ?? NewTitleBlockType,
            NewParams = merged,
            Delete = other.Delete || Delete,
        };
    }
}

public sealed record OpResult(ChangeSet Next, string? Pesan);
