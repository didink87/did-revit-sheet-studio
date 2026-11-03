using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;

namespace SheetStudio.Revit.Revit;

/// <summary>
/// Raises <see cref="Changed"/> (debounced, on the dispatcher) when sheets or title blocks change outside Sheet Studio.
/// Never touches the document inside DocumentChanged.
/// </summary>
public sealed class ModelWatcher : IDisposable
{
    private readonly Application _app;
    private readonly int _debounceMs;
    private readonly Action<Action> _dispatch;
    private readonly Dictionary<string, HashSet<ElementId>> _knownSheets = [];
    private readonly ElementFilter _filter = new LogicalOrFilter(
        new ElementClassFilter(typeof(ViewSheet)), new ElementCategoryFilter(BuiltInCategory.OST_TitleBlocks));
    private Timer? _timer;

    public ModelWatcher(Application app, int debounceMs = 500, Action<Action>? dispatch = null)
    {
        _app = app;
        _debounceMs = debounceMs;
        _dispatch = dispatch ?? (a => a());
        _app.DocumentChanged += OnChanged;
    }

    public event Action? Changed;

    private void OnChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.GetTransactionNames().Any(n => n.StartsWith("Sheet Studio", StringComparison.Ordinal))) return;
        var doc = e.GetDocument();
        var key = doc.PathName is { Length: > 0 } p ? p : doc.Title;
        if (!_knownSheets.TryGetValue(key, out var known))
            _knownSheets[key] = known = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).ToElementIds().ToHashSet();

        var added = e.GetAddedElementIds(_filter);
        var relevant = added.Count > 0 || e.GetModifiedElementIds(_filter).Count > 0 || e.GetDeletedElementIds().Any(known.Contains);
        foreach (var id in added) if (doc.GetElement(id) is ViewSheet) known.Add(id);
        foreach (var id in e.GetDeletedElementIds()) known.Remove(id);
        if (!relevant) return;

        if (_debounceMs <= 0) { _dispatch(Raise); return; }
        _timer?.Dispose();
        _timer = new Timer(_ => _dispatch(Raise), null, _debounceMs, Timeout.Infinite);
    }

    private void Raise() => Changed?.Invoke();

    public void Dispose()
    {
        _app.DocumentChanged -= OnChanged;
        _timer?.Dispose();
    }
}
