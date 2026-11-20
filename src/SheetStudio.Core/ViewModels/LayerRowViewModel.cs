using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;

namespace SheetStudio.Core.ViewModels;

/// <summary>Drop feedback drawn on a row while dragging: insert line above/below, box around a group, or refusal.</summary>
public enum DropHint { None, Atas, Bawah, Ke, Ditolak }

/// <summary>Display state of one layer row. Reused across rebuilds by key; staged changes update its properties in place.</summary>
public sealed class LayerRowViewModel : ObservableObject
{
    private bool _isSelected;
    private byte[]? _thumbnail;
    private DropHint _dropHint;
    private bool _isEditing;
    private string _editText = "";
    private string? _dropReason;

    private LayerRow _row = null!;
    private string? _number, _newNumber, _warningText, _borrowedBy;
    private IReadOnlyList<PlacedView> _placedViews = [];
    private bool _edited, _deleted, _borrowed, _inSheetList, _isNew;

    public LayerRowViewModel(LayerRow row) => _row = row;

    public LayerRow Row
    {
        get => _row;
        set
        {
            if (!Set(ref _row, value)) return;
            Raise(nameof(Kind), nameof(Key), nameof(Indent), nameof(Label), nameof(Count), nameof(Locked), nameof(Expanded),
                nameof(IsGroup), nameof(NumberChanged), nameof(NumberTooltip), nameof(Warning));
        }
    }

    public RowKind Kind => Row.Kind;
    public string Key => Row.Key;
    public int Indent => Row.Indent;
    public string Label => Row.Label;
    public string? Number { get => _number; set { if (Set(ref _number, value)) Raise(nameof(NumberChanged), nameof(NumberTooltip)); } }
    public string? NewNumber { get => _newNumber; set { if (Set(ref _newNumber, value)) Raise(nameof(NumberChanged), nameof(NumberTooltip)); } }
    public bool NumberChanged => Kind == RowKind.Lembar && !string.Equals(Number, NewNumber, StringComparison.Ordinal);
    public string? NumberTooltip => NumberChanged ? $"{Number} → {NewNumber}" : null;
    public int Count => Row.Count;
    public bool Warning => WarningText is not null;
    public string? WarningText { get => _warningText; set { if (Set(ref _warningText, value)) Raise(nameof(Warning)); } }
    public bool Edited { get => _edited; set => Set(ref _edited, value); }
    public bool Deleted { get => _deleted; set => Set(ref _deleted, value); }
    public bool Locked => Row.Locked;
    public bool Borrowed { get => _borrowed; set => Set(ref _borrowed, value); }
    public string? BorrowedBy { get => _borrowedBy; set => Set(ref _borrowedBy, value); }
    public bool InSheetList { get => _inSheetList; set => Set(ref _inSheetList, value); }
    public IReadOnlyList<PlacedView> PlacedViews
    {
        get => _placedViews;
        set { if (Set(ref _placedViews, value)) Raise(nameof(HasViews), nameof(ViewCountText)); }
    }
    public bool HasViews => !IsGroup && _placedViews.Count > 0;
    public string ViewCountText => _placedViews.Count switch { 0 => "Empty sheet", 1 => "1 view", var n => $"{n} views" };
    public bool Expanded => Row.Expanded;
    public bool IsNew { get => _isNew; set => Set(ref _isNew, value); }
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
