using SheetStudio.Core.Layers;

namespace SheetStudio.Core.ViewModels;

/// <summary>Drop feedback drawn on a row while dragging: insert line above/below, box around a group, or refusal.</summary>
public enum DropHint { None, Atas, Bawah, Ke, Ditolak }

/// <summary>Display state of one layer row. Rebuilt on every staged change; only IsSelected/Thumbnail mutate in place.</summary>
public sealed class LayerRowViewModel : ObservableObject
{
    private bool _isSelected;
    private byte[]? _thumbnail;
    private DropHint _dropHint;
    private bool _isEditing;
    private string _editText = "";
    private string? _dropReason;

    public LayerRowViewModel(LayerRow row) => Row = row;

    public LayerRow Row { get; }
    public RowKind Kind => Row.Kind;
    public string Key => Row.Key;
    public int Indent => Row.Indent;
    public string Label => Row.Label;
    public string? Number { get; init; }
    public string? NewNumber { get; init; }
    public bool NumberChanged => Kind == RowKind.Lembar && !string.Equals(Number, NewNumber, StringComparison.Ordinal);
    public string? NumberTooltip => NumberChanged ? $"{Number} → {NewNumber}" : null;
    public int Count => Row.Count;
    public bool Warning => WarningText is not null;
    public string? WarningText { get; init; }
    public bool Edited { get; init; }
    public bool Deleted { get; init; }
    public bool Locked => Row.Locked;
    public bool Borrowed { get; init; }
    public string? BorrowedBy { get; init; }
    public bool InSheetList { get; init; }
    public bool Expanded => Row.Expanded;
    public bool IsNew { get; init; }
    public bool IsGroup => Kind != RowKind.Lembar;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public byte[]? Thumbnail
    {
        get => _thumbnail;
        set => Set(ref _thumbnail, value);
    }

    public DropHint DropHint
    {
        get => _dropHint;
        set => Set(ref _dropHint, value);
    }

    /// <summary>Why a drop here is refused (tooltip while dragging).</summary>
    public string? DropReason
    {
        get => _dropReason;
        set => Set(ref _dropReason, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    public string EditText
    {
        get => _editText;
        set => Set(ref _editText, value ?? "");
    }
}
