using SheetStudio.Core.Diagnostics;

namespace SheetStudio.Core.Tests;

[Collection("StudioLog")]
public class StudioLogTests
{
    [Fact]
    public void Time_WritesEventDetailAndElapsed()
    {
        var lines = new List<string>();
        StudioLog.Sink = lines.Add;
        using (StudioLog.Time("load", "12 lembar")) Thread.Sleep(20);
        StudioLog.Sink = null;
        Assert.Matches(@"^\d\d:\d\d:\d\d\.\d{3} \[load\] 12 lembar \(\d+ ms\)$", Assert.Single(lines));
    }

    [Fact]
    public void Write_SinkThrowsOrNull_DoesNotThrow()
    {
        StudioLog.Sink = null;
        StudioLog.Write("x");
        StudioLog.Sink = _ => throw new IOException();
        StudioLog.Write("x");
        using (StudioLog.Time("y")) { }
        StudioLog.Sink = null;
    }
}
