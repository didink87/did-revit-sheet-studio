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
    private readonly Action<EventHandler<DocumentChangedEventArgs>> _unsubscribe;
    private readonly int _debounceMs;
    private readonly Action<Action> _dispatch;
    private readonly Dictionary<string, HashSet<ElementId>> _knownSheets = [];
    private readonly ElementFilter _filter = new LogicalOrFilter(
        new ElementClassFilter(typeof(ViewSheet)), new ElementCategoryFilter(BuiltInCategory.OST_TitleBlocks));
    private Timer? _timer;

    public ModelWatcher(Application app, int debounceMs = 500, Action<Action>? dispatch = null)
        : this(h => app.DocumentChanged += h, h => app.DocumentChanged -= h, debounceMs, dispatch) { }

    public ModelWatcher(ControlledApplication app, int debounceMs = 500, Action<Action>? dispatch = null)
        : this(h => app.DocumentChanged += h, h => app.DocumentChanged -= h, debounceMs, dispatch) { }

    private ModelWatcher(Action<EventHandler<DocumentChangedEventArgs>> subscribe, Action<EventHandler<DocumentChangedEventArgs>> unsubscribe,
        int debounceMs, Action<Action>? dispatch)
    {
        _unsubscribe = unsubscribe;
        _debounceMs = debounceMs;
        _dispatch = dispatch ?? (a => a());
        subscribe(OnChanged);
    }

    public event Action? Changed;

    private void OnChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (ShouldIgnore(e.GetTransactionNames(), e.Operation)) return;
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

    /// <summary>Our own commits are already reflected by the panel; Revit Undo/Redo of them is not.</summary>
    public static bool ShouldIgnore(IEnumerable<string> transactionNames, UndoOperation operation) =>
        operation == UndoOperation.TransactionCommitted &&
        transactionNames.Any(n => n.StartsWith("Sheet Studio", StringComparison.Ordinal));

    public void Dispose()
    {
        _unsubscribe(OnChanged);
        _timer?.Dispose();
    }
}
