using System.Collections.Concurrent;

namespace SheetStudio.Core.Threading;

/// <summary>FIFO queue of work items that run with a caller-supplied context, drained within a time budget.</summary>
public sealed class WorkQueue<TCtx>(Func<long>? clockMs = null)
{
    private readonly ConcurrentQueue<Action<TCtx>> _queue = new();
    private readonly Func<long> _clock = clockMs ?? (() => Environment.TickCount64);

    public bool IsEmpty => _queue.IsEmpty;

    public Task<T> Enqueue<T>(Func<TCtx, T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(ctx =>
        {
            try { tcs.SetResult(work(ctx)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>Runs items until empty or the budget elapsed; always runs at least one when present.</summary>
    public int Drain(TCtx ctx, int budgetMs)
    {
        var start = _clock();
        var ran = 0;
        while (_queue.TryDequeue(out var item))
        {
            item(ctx);
            ran++;
            if (_clock() - start >= budgetMs) break;
        }
        return ran;
    }
}
