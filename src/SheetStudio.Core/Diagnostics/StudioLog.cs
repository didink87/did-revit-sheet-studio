using System.Diagnostics;

namespace SheetStudio.Core.Diagnostics;

/// <summary>Diagnostic log facade. Never throws; the host installs <see cref="Sink"/>.</summary>
public static class StudioLog
{
    public static Action<string>? Sink { get; set; }

    public static void Write(string evt, string detail = "") => Emit(evt, detail, null);

    public static IDisposable Time(string evt, string detail = "") => new Timer(evt, detail);

    private static void Emit(string evt, string detail, long? ms)
    {
        try
        {
            var sink = Sink;
            if (sink is null) return;
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{evt}]";
            if (detail.Length > 0) line += " " + detail;
            if (ms is { } m) line += $" ({m} ms)";
            sink(line);
        }
        catch
        {
            // logging must never break the caller
        }
    }

    private sealed class Timer(string evt, string detail) : IDisposable
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            Emit(evt, detail, _sw.ElapsedMilliseconds);
        }
    }
}
