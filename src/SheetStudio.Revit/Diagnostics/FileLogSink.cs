namespace SheetStudio.Revit.Diagnostics;

/// <summary>Appends log lines to <c>sheetstudio-yyyyMMdd.log</c> in <paramref name="dir"/>. IO errors are swallowed.</summary>
public sealed class FileLogSink(string dir)
{
    private readonly object _lock = new();

    public static string DefaultDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SheetStudio", "log");

    public void Write(string line)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, $"sheetstudio-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
            }
        }
        catch
        {
            // diagnostics must never break the add-in
        }
    }
}
