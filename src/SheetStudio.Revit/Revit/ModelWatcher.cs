using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using SheetStudio.Core.Diagnostics;

namespace SheetStudio.Revit.Revit;

/// <summary>
/// Raises <see cref="Changed"/> (debounced, on the dispatcher) when sheets, title blocks, viewports or schedule instances change outside Sheet Studio.
/// Never touches the document inside DocumentChanged.
/// </summary>
public sealed class ModelWatcher : IDisposable
{
    private readonly Action<EventHandler<DocumentChangedEventArgs>> _unsubscribe;
    private readonly int _debounceMs;
    private readonly Action<Action> _dispatch;
    private readonly Dictionary<string, HashSet<ElementId>> _knownSheets = [];
    private readonly Dictionary<string, HashSet<ElementId>> _knownPlaced = [];
    private static readonly ElementFilter PlacedFilter = new LogicalOrFilter(
        new ElementClassFilter(typeof(Viewport)), new ElementClassFilter(typeof(ScheduleSheetInstance)));
    // Modifications: sheets and title blocks only. Moving a viewport or regenerating a schedule must not reload the panel.
    private static readonly ElementFilter ModifiedFilter = new LogicalOrFilter(
        new ElementClassFilter(typeof(ViewSheet)), new ElementCategoryFilter(BuiltInCategory.OST_TitleBlocks));
    // Additions: also viewports and schedule instances placed on a sheet.
    private static readonly ElementFilter AddedFilter = new LogicalOrFilter(ModifiedFilter, PlacedFilter);
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
        var doc = e.GetDocument();
        var key = doc.PathName is { Length: > 0 } p ? p : doc.Title;
        var deleted = e.GetDeletedElementIds();
        // A set built lazily here already reflects this event, so it cannot recognise ids deleted by it.
        var fresh = false;
        if (!_knownSheets.TryGetValue(key, out var known))
        {
            fresh = true;
            _knownSheets[key] = known = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).ToElementIds().ToHashSet();
        }
        if (!_knownPlaced.TryGetValue(key, out var placed))
        {
            fresh = true;
            _knownPlaced[key] = placed = new FilteredElementCollector(doc).WherePasses(PlacedFilter).ToElementIds().ToHashSet();
        }

        var added = e.GetAddedElementIds(AddedFilter);
        var relevant = added.Count > 0 || ModifiedSheetsOrTitleBlocks(e, doc) || (deleted.Count > 0 && fresh) || deleted.Any(id => known.Contains(id) || placed.Contains(id));
        foreach (var id in added) if (doc.GetElement(id) is ViewSheet) known.Add(id);
        foreach (var id in added) if (doc.GetElement(id) is Viewport or ScheduleSheetInstance) placed.Add(id);
        foreach (var id in deleted) { known.Remove(id); placed.Remove(id); }
        // Bookkeeping above always runs; ignoring our own commits only suppresses raising.
        if (!relevant || ShouldIgnore(e.GetTransactionNames(), e.Operation)) return;

        if (_debounceMs <= 0) { _dispatch(Raise); return; }
        _timer?.Dispose();
        _timer = new Timer(_ => _dispatch(Raise), null, _debounceMs, Timeout.Infinite);
    }

    /// <summary>
    /// Moving a viewport (or regenerating a schedule) also marks the owning sheet as modified. When a viewport/schedule
    /// instance is modified in the same event, the modified sheets are explained by it and do not count.
    /// </summary>
    private static bool ModifiedSheetsOrTitleBlocks(DocumentChangedEventArgs e, Document doc)
    {
        var modified = e.GetModifiedElementIds(ModifiedFilter);
        if (modified.Count == 0) return false;
        if (e.GetModifiedElementIds(PlacedFilter).Count == 0) return true;
        return modified.Any(id => doc.GetElement(id) is not ViewSheet);
    }

    private void Raise()
    {
        StudioLog.Write("watcher", "berubah");
        Changed?.Invoke();
    }

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
