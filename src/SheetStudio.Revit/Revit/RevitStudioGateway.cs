using Autodesk.Revit.DB;
using SheetStudio.Core.Diagnostics;
using SheetStudio.Core.Create;
using SheetStudio.Core.Model;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

/// <summary>IStudioGateway over the active document; every call runs through the ExternalEvent queue.</summary>
/// <param name="openSheet">Opens a sheet in the Revit UI; null in tests.</param>
public sealed partial class RevitStudioGateway(IDocumentRunner runner, Func<string, Task>? openSheet = null) : IStudioGateway
{
    public static string UserPrefsPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SheetStudio", "preferensi.json");

    public event Action? ModelChanged;

    internal void RaiseModelChanged() => ModelChanged?.Invoke();

    public Task<StudioSnapshot> LoadAsync() => runner.Run(Load);

    public static StudioSnapshot Load(Document doc)
    {
        using var _ = StudioLog.Time("load");
        var (prefsJson, stateJson) = StorageAdapter.Read(doc);
        var prefs = MergePrefs(prefsJson);
        var snapshot = SnapshotReader.Read(doc, prefs, ProjectStateJson.Deserialize(stateJson));
        StudioLog.Write("load", $"{snapshot.Sheets.Count} lembar");
        return snapshot;
    }

    /// <summary>Preferences only (defaults &lt; user file &lt; project storage): no sheet scan.</summary>
    public static Prefs LoadPrefs(Document doc) => MergePrefs(StorageAdapter.Read(doc).Preferensi);

    private static Prefs MergePrefs(string? projectPrefsJson)
    {
        var userJson = File.Exists(UserPrefsPath) ? File.ReadAllText(UserPrefsPath) : null;
        return PreferencesStore.Merge(Prefs.Default, userJson, projectPrefsJson);
    }

    public async Task<ApplyReport> ApplyAsync(ApplyPlan plan)
    {
        using var _ = StudioLog.Time("apply");
        return await runner.Run(doc => ApplyExecutor.Apply(doc, plan));
    }

    public Task SaveAsync(Prefs? prefs, ProjectState? state, bool userLevel) => runner.Run(doc => { Save(doc, prefs, state, userLevel); return true; });

    /// <summary>User level writes %AppData% (preferences only); project level writes Extensible Storage.</summary>
    public static void Save(Document doc, Prefs? prefs, ProjectState? state, bool userLevel)
    {
        if (prefs is not null && userLevel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserPrefsPath)!);
            File.WriteAllText(UserPrefsPath, PreferencesStore.Serialize(prefs));
            if (state is null) return;
        }
        using var t = new Transaction(doc, "Sheet Studio: Simpan preferensi");
        t.Start();
        StorageAdapter.Write(doc, prefs is not null && !userLevel ? PreferencesStore.Serialize(prefs) : null,
            state is null ? null : ProjectStateJson.Serialize(state));
        t.Commit();
    }

    public Task OpenSheetAsync(string uniqueId) => openSheet?.Invoke(uniqueId) ?? Task.CompletedTask;
}
