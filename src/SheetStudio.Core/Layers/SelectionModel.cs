namespace SheetStudio.Core.Layers;

/// <summary>Photoshop-style selection over layer row keys: click, Ctrl, Shift, Ctrl+Shift, select all.</summary>
public sealed class SelectionModel
{
    private readonly List<string> _selected = [];

    public IReadOnlyList<string> Selected => _selected;
    public string? Anchor { get; private set; }
    public event Action? Changed;

    public void Click(string key)
    {
        _selected.Clear();
        _selected.Add(key);
        Anchor = key;
        Changed?.Invoke();
    }

    public void CtrlClick(string key)
    {
        if (!_selected.Remove(key)) _selected.Add(key);
        Anchor = key;
        Changed?.Invoke();
    }

    public void ShiftClick(string key, IReadOnlyList<LayerRow> visible)
    {
        var range = Range(key, visible);
        _selected.Clear();
        _selected.AddRange(range);
        Changed?.Invoke();
    }

    public void CtrlShiftClick(string key, IReadOnlyList<LayerRow> visible)
    {
        foreach (var k in Range(key, visible))
            if (!_selected.Contains(k)) _selected.Add(k);
        Changed?.Invoke();
    }

    public void SelectAll(IReadOnlyList<LayerRow> visible)
    {
        _selected.Clear();
        _selected.AddRange(visible.Where(r => r.Kind == RowKind.Lembar).Select(r => r.Key));
        Anchor = _selected.FirstOrDefault();
        Changed?.Invoke();
    }

    public void Clear()
    {
        _selected.Clear();
        Anchor = null;
        Changed?.Invoke();
    }

    /// <summary>Selected sheets with group selections expanded, in row order, without duplicates.
    /// Pass rows built without collapse so collapsed children are included.</summary>
    public IReadOnlyList<string> SelectedSheetIds(IReadOnlyList<LayerRow> all)
    {
        var set = _selected.ToHashSet();
        return all
            .Where(r => r.Kind == RowKind.Lembar &&
                        (set.Contains(r.Key) || (r.GroupKey is { } t && (set.Contains(t) || set.Contains(t.Split('|')[0])))))
            .Select(r => r.Key)
            .ToList();
    }

    public bool IsSelected(LayerRow row) =>
        _selected.Contains(row.Key) ||
        (row.GroupKey is { } g && (_selected.Contains(g) || _selected.Contains(g.Split('|')[0])));

    private List<string> Range(string key, IReadOnlyList<LayerRow> visible)
    {
        var to = IndexOf(visible, key);
        var from = Anchor is null ? -1 : IndexOf(visible, Anchor);
        if (to < 0) return [];
        if (from < 0) { Anchor = key; return [key]; }
        var (a, b) = from <= to ? (from, to) : (to, from);
        // Expanded headers inside the range are skipped (their visible children are already in it);
        // collapsed headers stand for their hidden children; the clicked endpoints always count.
        return visible.Skip(a).Take(b - a + 1)
            .Where(r => r.Kind == RowKind.Lembar || !r.Expanded || r.Key == key || r.Key == Anchor)
            .Select(r => r.Key).ToList();
    }

    private static int IndexOf(IReadOnlyList<LayerRow> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++) if (rows[i].Key == key) return i;
        return -1;
    }
}
