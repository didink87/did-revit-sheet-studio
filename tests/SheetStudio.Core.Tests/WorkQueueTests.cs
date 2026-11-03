using SheetStudio.Core.Threading;

namespace SheetStudio.Core.Tests;

public class WorkQueueTests
{
    [Fact]
    public async Task Drain_RunsAllWithinBudget()
    {
        var q = new WorkQueue<int>(() => 0);
        var t1 = q.Enqueue(c => c + 1);
        var t2 = q.Enqueue(c => c + 2);
        var t3 = q.Enqueue(c => c + 3);

        Assert.Equal(3, q.Drain(10, 40));

        Assert.Equal(11, await t1);
        Assert.Equal(12, await t2);
        Assert.Equal(13, await t3);
        Assert.True(q.IsEmpty);
    }

    [Fact]
    public async Task Drain_StopsAtBudget_RunsAtLeastOne()
    {
        long now = 0;
        var q = new WorkQueue<int>(() => now += 50);
        var t1 = q.Enqueue(c => 1);
        var t2 = q.Enqueue(c => 2);

        Assert.Equal(1, q.Drain(0, 40));
        Assert.True(t1.IsCompletedSuccessfully);
        Assert.False(q.IsEmpty);

        Assert.Equal(1, q.Drain(0, 40));
        Assert.Equal(2, await t2);
        Assert.True(q.IsEmpty);
    }

    [Fact]
    public async Task Enqueue_Exception_FaultsTaskAndContinues()
    {
        var q = new WorkQueue<int>(() => 0);
        var t1 = q.Enqueue<int>(c => throw new InvalidOperationException("boom"));
        var t2 = q.Enqueue(c => 7);

        Assert.Equal(2, q.Drain(0, 40));

        Assert.True(t1.IsFaulted);
        Assert.Equal(7, await t2);
    }
}
