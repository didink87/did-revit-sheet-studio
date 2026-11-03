using System.Windows.Input;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Numbering;

namespace SheetStudio.Core.ViewModels;

public enum DropPosition { Sebelum, Sesudah, Ke }

public sealed record DropEvaluation(DropPosition Pos, bool Allowed, string? Reason);

/// <summary>Photoshop-style layers panel: selection, drag-drop, keyboard moves, lock/eye, collapse, filter.</summary>
public sealed class LayersViewModel : ObservableObject
{
    private readonly StudioViewModel _studio;
    private readonly HashSet<string> _collapsed = [];
    private IReadOnlyList<LayerRowViewModel> _rows = [];
    private IReadOnlyList<LayerRow> _allRows = [];
    private string _filter = "";
    private readonly Dictionary<string, byte[]> _thumbs = [];
    private readonly Dictionary<string, (bool Ok, string? Reason)> _dropCache = [];
    private readonly HashSet<string> _thumbRequested = [];

    internal LayersViewModel(StudioViewModel studio)
    {
        _studio = studio;
        Selection.Changed += OnSelectionChanged;
        MoveUp = new RelayCommand(() => Move(-1));
        MoveDown = new RelayCommand(() => Move(+1));
        MoveTop = new RelayCommand(() => Edge(true));
        MoveBottom = new RelayCommand(() => Edge(false));
        DeleteSelected = new RelayCommand(MarkDelete);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelection = new RelayCommand(() => Selection.Clear());
        BeginRename = new RelayCommand(StartRename, () => SingleSelectedSheet() is not null);
        ToggleLockSelected = new RelayCommand(() => _studio.Push(_studio.Current.ToggleLock(SelectedSheetIds())), () => SelectedSheetIds().Count > 0);
        ToggleEyeSelected = new RelayCommand(() => ToggleEyeIds(SelectedSheetIds()), () => SelectedSheetIds().Count > 0);
        OpenSelected = new AsyncRelayCommand(() => SingleSelectedSheet() is { } id ? OpenAsync(id) : Task.CompletedTask, () => SingleSelectedSheet() is not null, studio.ReportError);
    }

    public ICommand BeginRename { get; }
    public ICommand ToggleLockSelected { get; }
    public ICommand ToggleEyeSelected { get; }
    public ICommand OpenSelected { get; }

    /// <summary>Where would a drop at this point of the target row land, and is it allowed?
    /// Sheet rows: upper half = before, lower half = after. Group rows: into the group.</summary>
    public DropEvaluation Evaluate(IReadOnlyList<string> keys, string targetKey, double yFraction)
    {
        var target = _allRows.FirstOrDefault(r => r.Key == targetKey);
        if (target is null) return new DropEvaluation(DropPosition.Ke, false, null);
        var pos = target.Kind == RowKind.Lembar ? (yFraction < 0.5 ? DropPosition.Sebelum : DropPosition.Sesudah) : DropPosition.Ke;
        if (keys.Contains(targetKey)) return new DropEvaluation(pos, false, null);
        var ok = CanDrop(keys, targetKey, pos, out var reason);
        return new DropEvaluation(pos, ok, reason);
    }

    public DropEvaluation ShowDropHint(IReadOnlyList<string> keys, string targetKey, double yFraction)
    {
        var e = Evaluate(keys, targetKey, yFraction);
        foreach (var r in _rows)
        {
            if (r.Key != targetKey) { r.DropHint = DropHint.None; r.DropReason = null; continue; }
            r.DropHint = !e.Allowed ? DropHint.Ditolak : e.Pos switch
            {
                DropPosition.Sebelum => DropHint.Atas,
                DropPosition.Sesudah => DropHint.Bawah,
                _ => DropHint.Ke,
            };
            r.DropReason = e.Reason;
        }
        return e;
    }

    public void ClearDropHint()
    {
        foreach (var r in _rows) { r.DropHint = DropHint.None; r.DropReason = null; }
    }

    public void CommitRename(LayerRowViewModel row)
    {
        if (!row.IsEditing) return;
        row.IsEditing = false;
        RenameInline(row.Key, row.EditText);
    }

    public void CancelRename(LayerRowViewModel row) => row.IsEditing = false;

    /// <summary>Loads missing previews for the given (visible) keys; cached per sheet version.</summary>
    public async Task LoadThumbnailsAsync(IEnumerable<string> keys)
    {
        var byId = _studio.Sheets.ToDictionary(s => s.UniqueId);
        foreach (var key in keys.Where(byId.ContainsKey).ToList())
        {
            var cacheKey = key + "|" + byId[key].Versi;
            if (!_thumbRequested.Add(cacheKey)) continue;
            var bytes = await _studio.Gateway.ThumbnailAsync(key);
            if (bytes is null) continue;
            _thumbs[cacheKey] = bytes;
            foreach (var r in _rows.Where(r => r.Key == key)) r.Thumbnail = bytes;
        }
    }

    public SelectionModel Selection { get; } = new();

    public IReadOnlyList<LayerRowViewModel> Rows
    {
        get => _rows;
        private set => Set(ref _rows, value);
    }

    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value ?? "")) Rebuild(); }
    }

    public ICommand MoveUp { get; }
    public ICommand MoveDown { get; }
    public ICommand MoveTop { get; }
    public ICommand MoveBottom { get; }
    public ICommand DeleteSelected { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearSelection { get; }

    public void Click(string key) => Selection.Click(key);
    public void CtrlClick(string key) => Selection.CtrlClick(key);
    public void ShiftClick(string key) => Selection.ShiftClick(key, VisibleRows());
    public void CtrlShiftClick(string key) => Selection.CtrlShiftClick(key, VisibleRows());
    public void SelectAll() => Selection.SelectAll(VisibleRows());

    public IReadOnlyList<string> SelectedSheetIds() => Selection.SelectedSheetIds(_allRows);

    /// <summary>Can the dragged keys land at target/pos? Reason explains a refusal (shown as tooltip).</summary>
    public bool CanDrop(IReadOnlyList<string> keys, string targetKey, DropPosition pos, out string? reason)
    {
        if (TryDisciplineDrop(keys, targetKey, out var discIndex))
        {
            reason = discIndex < 0 ? "Grup disiplin hanya bisa dilepas di grup disiplin lain" : null;
            return discIndex >= 0;
        }
        var cacheKey = string.Join(",", keys) + "|" + targetKey + "|" + pos;
        if (_dropCache.TryGetValue(cacheKey, out var cached)) { reason = cached.Reason; return cached.Ok; }
        var ok = Decide(keys, targetKey, pos, out reason);
        _dropCache[cacheKey] = (ok, reason);
        return ok;
    }

    /// <summary>One MoveSelection + one Compute per candidate drop. Only errors the drop would add count —
    /// a group that already had a warning still accepts a plain reorder.</summary>
    private bool Decide(IReadOnlyList<string> keys, string targetKey, DropPosition pos, out string? reason)
    {
        var (group, index) = Target(targetKey, pos);
        if (group is null) { reason = "Tidak bisa dilepas di sini"; return false; }
        var ids = Expand(keys);
        var r = _studio.Current.MoveSelection(ids, group, index, _studio.Sheets, _studio.Profile);
        if (ReferenceEquals(r.Next, _studio.Current)) { reason = r.Pesan; return r.Pesan is null; }
        var before = _studio.Outcome.Errors.ToHashSet();
        var added = Sequencer.Compute(_studio.Sheets, r.Next, _studio.Profile, _studio.Numbering).Errors.Where(e => !before.Contains(e)).ToList();
        if (added.Count > 0) { reason = added[0]; return false; }
        reason = r.Pesan;
        return true;
    }

    public void Drop(IReadOnlyList<string> keys, string targetKey, DropPosition pos)
    {
        if (TryDisciplineDrop(keys, targetKey, out var discIndex))
        {
            if (discIndex >= 0) _studio.Push(_studio.Current.MoveDisciplineGroup(keys[0], discIndex, _studio.Sheets, _studio.Profile));
            return;
        }
        var (group, index) = Target(targetKey, pos);
        if (group is null) return;
        _studio.Push(_studio.Current.MoveSelection(Expand(keys), group, index, _studio.Sheets, _studio.Profile));
    }

    public void ToggleLock(string key) => _studio.Push(_studio.Current.ToggleLock(Expand([key])));

    public void ToggleEye(string key) => ToggleEyeIds(Expand([key]));

    private void ToggleEyeIds(IReadOnlyList<string> ids)
    {
        var display = _studio.DisplaySheets().Where(s => ids.Contains(s.UniqueId)).ToList();
        _studio.Push(_studio.Current.SetInSheetList(ids, !display.All(s => s.InSheetList)));
    }

    public void ToggleCollapse(string key, bool all)
    {
        var collapse = !_collapsed.Contains(key);
        if (all)
        {
            foreach (var r in _allRows.Where(r => r.Kind != RowKind.Lembar))
                if (collapse) _collapsed.Add(r.Key); else _collapsed.Remove(r.Key);
        }
        else if (collapse) _collapsed.Add(key);
        else _collapsed.Remove(key);
        Rebuild();
    }

    public void RenameInline(string uniqueId, string title)
    {
        var sheet = _studio.Sheets.FirstOrDefault(s => s.UniqueId == uniqueId);
        if (sheet is null) return;
        _studio.Push(Edit.BulkEdit.SetTitle(_studio.Current, [sheet], title));
    }

    public void MarkDelete() => MarkDeleteKeys(SelectedSheetIds());

    public void MarkDeleteKeys(IReadOnlyList<string> keys)
    {
        var ids = Expand(keys);
        var allMarked = ids.All(id => _studio.Current.Changes.TryGetValue(id, out var c) && c.Delete);
        _studio.Push(_studio.Current.MarkDelete(ids, !allMarked));
    }

    public Task OpenAsync(string uniqueId) => _studio.Gateway.OpenSheetAsync(uniqueId);

    internal void Rebuild()
    {
        _dropCache.Clear();
        var display = _studio.DisplaySheets();
        var state = _studio.Current.State;
        _allRows = LayerListBuilder.Build(display, state, _studio.Profile, new HashSet<string>(), _filter);
        var visible = LayerListBuilder.Build(display, state, _studio.Profile, _collapsed, _filter);
        var byId = _studio.Sheets.ToDictionary(s => s.UniqueId);
        var shown = display.ToDictionary(s => s.UniqueId);

        Rows = visible.Select(r =>
        {
            if (r.Kind != RowKind.Lembar) return new LayerRowViewModel(r) { IsSelected = Selection.IsSelected(r) };
            var orig = byId[r.Key];
            _studio.Current.Changes.TryGetValue(r.Key, out var c);
            return new LayerRowViewModel(r)
            {
                Number = orig.Number,
                NewNumber = shown[r.Key].Number,
                WarningText = _studio.WarningFor(shown[r.Key]),
                Edited = c is not null && (c.NewTitle is not null || c.NewParams is { Count: > 0 } || c.NewTitleBlockType is not null || c.InSheetList is not null),
                Deleted = c?.Delete == true,
                Borrowed = orig.BorrowedBy is not null,
                BorrowedBy = orig.BorrowedBy,
                InSheetList = shown[r.Key].InSheetList,
                IsNew = _studio.NewSheetIds.Contains(r.Key),
                IsSelected = Selection.IsSelected(r),
                Thumbnail = _thumbs.GetValueOrDefault(r.Key + "|" + orig.Versi),
            };
        }).ToList();
    }

    private string? SingleSelectedSheet()
    {
        var ids = SelectedSheetIds();
        return ids.Count == 1 && Selection.Selected.Count == 1 && Selection.Selected[0] == ids[0] ? ids[0] : null;
    }

    private void StartRename()
    {
        if (SingleSelectedSheet() is not { } id) return;
        foreach (var r in _rows) r.IsEditing = false;
        var row = _rows.FirstOrDefault(r => r.Key == id);
        if (row is null) return;
        row.EditText = row.Label;
        row.IsEditing = true;
    }

    private IReadOnlyList<LayerRow> VisibleRows() => Rows.Select(r => r.Row).ToList();

    private void OnSelectionChanged()
    {
        foreach (var r in _rows) r.IsSelected = Selection.IsSelected(r.Row);
        _studio.Inspector.Refresh();
        foreach (var c in new[] { BeginRename, ToggleLockSelected, ToggleEyeSelected }.OfType<RelayCommand>()) c.RaiseCanExecuteChanged();
        (OpenSelected as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private IReadOnlyList<string> Expand(IReadOnlyList<string> keys)
    {
        var set = keys.ToHashSet();
        return _allRows.Where(r => r.Kind == RowKind.Lembar &&
                (set.Contains(r.Key) || (r.GroupKey is { } g && (set.Contains(g) || set.Contains(g.Split('|')[0])))))
            .Select(r => r.Key).ToList();
    }

    /// <summary>Target type group and insertion index for a drop on a row.</summary>
    private (string? Group, int Index) Target(string targetKey, DropPosition pos)
    {
        var row = _allRows.FirstOrDefault(r => r.Key == targetKey);
        if (row is null) return (null, 0);
        switch (row.Kind)
        {
            case RowKind.Disiplin:
                return (row.Key, int.MaxValue / 2);
            case RowKind.Tipe:
                return (row.Key, pos == DropPosition.Sebelum ? 0 : int.MaxValue / 2);
            default:
                var group = row.GroupKey!;
                var order = _studio.Current.GroupOrder(group, _studio.Sheets, _studio.Profile).ToList();
                var i = order.IndexOf(row.Key);
                return (group, pos == DropPosition.Sesudah ? i + 1 : i);
        }
    }

    /// <summary>Dragging a single discipline header onto another reorders the set. -1 = not allowed.</summary>
    private bool TryDisciplineDrop(IReadOnlyList<string> keys, string targetKey, out int index)
    {
        index = -1;
        if (keys.Count != 1 || _allRows.FirstOrDefault(r => r.Key == keys[0])?.Kind != RowKind.Disiplin) return false;
        var discs = _allRows.Where(r => r.Kind == RowKind.Disiplin && r.Key != LayerListBuilder.UnknownDiscipline).Select(r => r.Key).ToList();
        var target = _allRows.FirstOrDefault(r => r.Key == targetKey);
        if (target?.Kind == RowKind.Disiplin && target.Key != LayerListBuilder.UnknownDiscipline) index = discs.IndexOf(target.Key);
        return true;
    }

    private void Move(int delta) => _studio.Push(_studio.Current.MoveBy(SelectedSheetIds(), delta, _studio.Sheets, _studio.Profile));
    private void Edge(bool top) => _studio.Push(_studio.Current.MoveToEdge(SelectedSheetIds(), top, _studio.Sheets, _studio.Profile));
}
