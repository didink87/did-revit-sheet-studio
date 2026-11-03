namespace SheetStudio.Core.Layers;

public enum RowKind { Disiplin, Tipe, Lembar }

/// <summary>
/// One row of the flat, indented layers list. Key: discipline "A", type "A|1", sheet = UniqueId.
/// GroupKey: sheet → its type key, type → its discipline key, discipline → null.
/// </summary>
public sealed record LayerRow(
    RowKind Kind,
    string Key,
    int Indent,
    string Label,
    string? Number,
    int Count,
    bool Locked,
    bool InSheetList,
    bool Expanded,
    bool Editable,
    string? SheetUniqueId,
    string? GroupKey);
