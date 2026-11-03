using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SheetStudio.Core.Create;
using SheetStudio.Core.Model;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

/// <summary>IStudioGateway over the active document; every call runs through the ExternalEvent queue.</summary>
public sealed partial class RevitStudioGateway(ExternalEventQueue queue) : IStudioGateway
{
    public static string UserPrefsPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SheetStudio", "preferensi.json");

    public event Action? ModelChanged;

    internal void RaiseModelChanged() => ModelChanged?.Invoke();

    public Task<StudioSnapshot> LoadAsync() => queue.Run(ui => Load(ActiveDoc(ui)));

    public static StudioSnapshot Load(Document doc)
    {
        var (prefsJson, stateJson) = StorageAdapter.Read(doc);
        var userJson = File.Exists(UserPrefsPath) ? File.ReadAllText(UserPrefsPath) : null;
        var prefs = PreferencesStore.Merge(Prefs.Default, userJson, prefsJson);
        return SnapshotReader.Read(doc, prefs, ProjectStateJson.Deserialize(stateJson));
    }

    public Task<ApplyReport> ApplyAsync(ApplyPlan plan) => queue.Run(ui => ApplyExecutor.Apply(ActiveDoc(ui), plan));

    public Task SaveAsync(Prefs? prefs, ProjectState? state, bool userLevel) => queue.Run(ui =>
    {
        if (prefs is not null && userLevel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserPrefsPath)!);
            File.WriteAllText(UserPrefsPath, PreferencesStore.Serialize(prefs));
            if (state is null) return;
        }
        var doc = ActiveDoc(ui);
        using var t = new Transaction(doc, "Sheet Studio: Simpan preferensi");
        t.Start();
        StorageAdapter.Write(doc, prefs is not null && !userLevel ? PreferencesStore.Serialize(prefs) : null,
            state is null ? null : ProjectStateJson.Serialize(state));
        t.Commit();
    });

    public Task OpenSheetAsync(string uniqueId) => queue.Run(ui =>
    {
        var doc = ActiveDoc(ui);
        if (doc.GetElement(uniqueId) is ViewSheet s) ui.ActiveUIDocument.RequestViewChange(s);
    });

    internal static Document ActiveDoc(UIApplication ui) =>
        ui.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Tidak ada dokumen Revit yang aktif");
}
