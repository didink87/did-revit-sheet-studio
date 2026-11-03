using Autodesk.Revit.UI;
using SheetStudio.Core.Diagnostics;
using SheetStudio.Core.Threading;

namespace SheetStudio.Revit.Revit;

/// <summary>Runs work in a valid Revit API context and hands the result back as a Task (drains within a time budget per event).</summary>
public sealed class ExternalEventQueue : IExternalEventHandler
{
    private const int DrainBudgetMs = 40;

    private readonly WorkQueue<UIApplication> _queue = new();
    private readonly ExternalEvent _event;

    public ExternalEventQueue() => _event = ExternalEvent.Create(this);

    public Task<T> Run<T>(Func<UIApplication, T> work)
    {
        var task = _queue.Enqueue(work);
        Raise();
        return task;
    }

    public Task Run(Action<UIApplication> work) => Run<bool>(app => { work(app); return true; });

    public void Execute(UIApplication app)
    {
        _queue.Drain(app, DrainBudgetMs);
        if (!_queue.IsEmpty) Raise();
    }

    public string GetName() => "SheetStudio.ExternalEventQueue";

    private void Raise()
    {
        var result = _event.Raise();
        if (result != ExternalEventRequest.Accepted && result != ExternalEventRequest.Pending) StudioLog.Write("event", result.ToString());
    }
}
