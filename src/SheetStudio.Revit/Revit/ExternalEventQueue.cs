using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace SheetStudio.Revit.Revit;

/// <summary>Runs work in a valid Revit API context and hands the result back as a Task (one item per event).</summary>
public sealed class ExternalEventQueue : IExternalEventHandler
{
    private readonly ConcurrentQueue<Action<UIApplication>> _queue = new();
    private readonly ExternalEvent _event;

    public ExternalEventQueue() => _event = ExternalEvent.Create(this);

    public Task<T> Run<T>(Func<UIApplication, T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(app =>
        {
            try { tcs.SetResult(work(app)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        _event.Raise();
        return tcs.Task;
    }

    public Task Run(Action<UIApplication> work) => Run<bool>(app => { work(app); return true; });

    public void Execute(UIApplication app)
    {
        if (_queue.TryDequeue(out var work)) work(app);
        if (!_queue.IsEmpty) _event.Raise();
    }

    public string GetName() => "SheetStudio.ExternalEventQueue";
}
