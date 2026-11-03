using System.Windows.Controls;
using NUnit.Framework;
using SheetStudio.Core.Create;
using SheetStudio.Core.Model;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;
using SheetStudio.Revit.Ui;

namespace SheetStudio.Revit.Tests;

[TestFixture]
public class UiSmokeTests
{
    internal sealed class Fake : IStudioGateway
    {
        public List<SheetSnapshot> Sheets { get; } =
        [
            Snap("L1", "A-110", "DENAH LANTAI 1", "denah"),
            Snap("L2", "A-111", "DENAH LANTAI 2", "denah"),
            Snap("T1", "A-201", "TAMPAK DEPAN", "tampak-bangunan"),
        ];

        private static SheetSnapshot Snap(string id, string n, string t, string k) =>
            new(id, n, t, k, "", true, true, null, n + "|" + t, new Dictionary<string, string> { ["Drawn By"] = "" }, "PUPR_KOP_A3");

        public event Action? ModelChanged { add { } remove { } }
        public Task<StudioSnapshot> LoadAsync() => Task.FromResult(new StudioSnapshot("doc1", Sheets, ProjectState.Empty,
            Prefs.Default with { KonfirmasiTerapkan = false, Thumbnail = false }, [("Lantai 1", 0)], ["PUPR_KOP_A3"], []));
        public Task<ApplyReport> ApplyAsync(ApplyPlan plan) => Task.FromResult(new ApplyReport(0, []));
        public Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done, int Total, string Title)> progress, CancellationToken ct) =>
            Task.FromResult(new CreateReport([], false));
        public Task<CreateReport> DuplicateAsync(IReadOnlyList<string> ids, DuplicateMode mode) => Task.FromResult(new CreateReport([], false));
        public Task OpenSheetAsync(string uniqueId) => Task.CompletedTask;
        public Task<byte[]?> ThumbnailAsync(string uniqueId) => Task.FromResult<byte[]?>(null);
        public Task SaveAsync(Prefs? prefs, ProjectState? state, bool userLevel) => Task.CompletedTask;
    }

    [Test]
    public async Task LayersPanel_BindsRowsFromViewModel()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        var pane = new StudioPane(vm);
        pane.Measure(new System.Windows.Size(900, 600));
        pane.Arrange(new System.Windows.Rect(0, 0, 900, 600));
        pane.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { })); // let bindings resolve
        var list = pane.LayersView.LayerList;
        Assert.That(list, Is.Not.Null);
        Assert.That(list.Items.Count, Is.EqualTo(vm.Layers.Rows.Count));
        Assert.That(list.Items.Count, Is.EqualTo(6)); // A, A|1, L1, L2, A|2, T1
    }

    [Test]
    public async Task PreviewWindow_ListsPlannedChanges()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        vm.Layers.Drop(["L2"], "L1", DropPosition.Sebelum);
        var window = new PreviewWindow(vm, vm.BuildPlan(), confirm: true);
        Assert.That(window.Rows.Count, Is.EqualTo(2));
        Assert.That(window.Rows.Select(r => r.Lama), Is.EquivalentTo(new[] { "A-110", "A-111" }));
        window.Close();
    }

    private static void Pump(System.Windows.FrameworkElement e)
    {
        e.Measure(new System.Windows.Size(1000, 800));
        e.Arrange(new System.Windows.Rect(0, 0, 1000, 800));
        e.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
    }

    [Test]
    public async Task NamePicker_Instantiates_WithProfilePupr()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        var create = vm.NewCreateSheet();
        var picker = new NamePicker { DataContext = create.Picker };
        Pump(picker);
        Assert.That(picker.Disciplines.Items.Count, Is.LessThan(21));
        create.Picker.ShowAllDisciplines = true;
        Pump(picker);
        Assert.That(picker.Disciplines.Items.Count, Is.EqualTo(21));
        Assert.That(picker.NumberGrid.Items.Count, Is.EqualTo(99));
    }

    [Test]
    public async Task CreateWindow_ConfirmDisabledWhenInvalid()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        var create = vm.NewCreateSheet();
        var window = new CreateSheetWindow(create);
        Pump(window);
        Assert.That(window.CreateButton.IsEnabled, Is.False);
        create.Picker.SelectedKindId = "denah";
        create.Picker.Subject = "LANTAI 3";
        Pump(window);
        Assert.That(window.CreateButton.IsEnabled, Is.True);
        window.Close();
    }

    [Test]
    public async Task BatchWindow_Instantiates()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        var batch = vm.NewBatch();
        var window = new BatchWindow(batch);
        batch.LoadPreset("DENAH SEMUA LANTAI");
        Pump(window);
        Assert.That(window.RowsGrid.Items.Count, Is.EqualTo(1));
        window.Close();
    }

    [Test]
    public async Task Inspector_ShowsBerbedaForMixed()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        var panel = new InspectorPanel { DataContext = vm.Inspector };
        vm.Layers.Click("L1");
        vm.Layers.CtrlClick("T1");
        Pump(panel);
        Assert.That(panel.MixedTitleHint.Visibility, Is.EqualTo(System.Windows.Visibility.Visible));
        Assert.That(panel.SeqBox.IsEnabled, Is.False);
        vm.Layers.Click("L1");
        Pump(panel);
        Assert.That(panel.MixedTitleHint.Visibility, Is.EqualTo(System.Windows.Visibility.Collapsed));
        Assert.That(panel.SeqBox.IsEnabled, Is.True);
    }

    [Test]
    public async Task PreferencesWindow_Instantiates()
    {
        var vm = new StudioViewModel(new Fake());
        await vm.LoadAsync();
        var window = new PreferencesWindow(vm.NewPreferences());
        Pump(window);
        Assert.That(window.TitleBlockA3.Text, Is.EqualTo("PUPR_KOP_A3"));
        window.Close();
    }
}
