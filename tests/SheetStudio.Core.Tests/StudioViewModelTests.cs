using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.ViewModels;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class StudioViewModelTests
{
    private static async Task<(StudioViewModel Vm, FakeGateway Gw)> Loaded()
    {
        var gw = new FakeGateway
        {
            Sheets =
            [
                S("L1", "A-110", "DENAH LANTAI 1", kind: "denah", subjek: "LANTAI 1"),
                S("L2", "A-111", "DENAH LANTAI 2", kind: "denah", subjek: "LANTAI 2"),
                S("T1", "A-201", "TAMPAK DEPAN", kind: "tampak-bangunan"),
            ],
        };
        var vm = new StudioViewModel(gw);
        await vm.LoadAsync();
        return (vm, gw);
    }

    private static LayerRowViewModel Row(StudioViewModel vm, string key) => vm.Layers.Rows.Single(r => r.Key == key);

    [Fact]
    public async Task Load_BuildsRows()
    {
        var (vm, _) = await Loaded();
        Assert.Equal(new[] { "A", "A|1", "L1", "L2", "A|2", "T1" }, vm.Layers.Rows.Select(r => r.Key));
        Assert.Equal(0, vm.PendingCount);
    }

    [Fact]
    public async Task Drop_UpdatesNewNumbersWithoutApply()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.Click("L2");
        Assert.True(vm.Layers.CanDrop(["L2"], "L1", DropPosition.Sebelum, out _));
        vm.Layers.Drop(["L2"], "L1", DropPosition.Sebelum);
        Assert.Empty(gw.Applied);
        Assert.True(vm.PendingCount > 0);
        Assert.Equal(new[] { "L2", "L1" }, vm.Layers.Rows.Where(r => r.Kind == RowKind.Lembar && r.Key.StartsWith('L')).Select(r => r.Key));
        Assert.Equal(("A-111", "A-110", true), (Row(vm, "L2").Number, Row(vm, "L2").NewNumber, Row(vm, "L2").NumberChanged));
        Assert.True(Row(vm, "L2").IsSelected);
    }

    [Fact]
    public async Task Undo_RestoresRowsAndNumbers()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Drop(["L2"], "L1", DropPosition.Sebelum);
        vm.Undo.Execute(null);
        Assert.Equal("A-111", Row(vm, "L2").NewNumber);
        Assert.False(Row(vm, "L2").NumberChanged);
        Assert.Equal(0, vm.PendingCount);
        vm.Redo.Execute(null);
        Assert.Equal("A-110", Row(vm, "L2").NewNumber);
    }

    [Fact]
    public async Task Apply_SendsTwoPhasePlanAndExpectedVersi()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.Drop(["L2"], "L1", DropPosition.Sebelum);
        vm.Layers.RenameInline("T1", "tampak depan baru");
        Assert.True(await vm.ApplyAsync());
        var plan = Assert.Single(gw.Applied);
        Assert.Equal(2, plan.Renumber.Count);
        Assert.All(plan.Renumber, s => Assert.StartsWith("SSTMP-", s.Temp));
        Assert.Equal("TAMPAK DEPAN BARU", plan.Edits.Single(e => e.UniqueId == "T1").NewTitle);
        Assert.Equal("A-111|DENAH LANTAI 2", plan.ExpectedVersi["L2"]);
        Assert.Equal("A-201|TAMPAK DEPAN", plan.ExpectedVersi["T1"]);
        Assert.Equal(new[] { "L2", "L1" }, plan.NewState.OrderPerGroup["A|1"]);
    }

    [Fact]
    public async Task Apply_ClearsUndoAndReloads()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.MarkDeleteKeys(["T1"]);
        var loadsBefore = gw.Loads;
        await vm.ApplyAsync();
        Assert.Equal(new[] { "T1" }, gw.Applied.Single().Deletes);
        Assert.Equal(loadsBefore + 1, gw.Loads);
        Assert.False(vm.Undo.CanExecute(null));
        Assert.Equal(0, vm.PendingCount);
    }

    [Fact]
    public async Task Apply_AskConfirmWhenPreferenceOn()
    {
        var (vm, gw) = await Loaded();
        gw.Prefs = gw.Prefs with { KonfirmasiTerapkan = true };
        await vm.LoadAsync();
        vm.Layers.MarkDeleteKeys(["T1"]);
        vm.ConfirmApply = _ => Task.FromResult(false);
        Assert.False(await vm.ApplyAsync());
        Assert.Empty(gw.Applied);
    }

    [Fact]
    public async Task ModelChanged_RebasesAndReportsDropped()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.RenameInline("T1", "X");
        vm.Layers.RenameInline("L1", "DENAH SATU");
        gw.Sheets.RemoveAll(s => s.UniqueId == "T1");
        await vm.OnModelChangedAsync();
        Assert.Contains("1 perubahan dibuang", vm.Status);
        Assert.Equal(1, vm.PendingCount);
        Assert.DoesNotContain(vm.Layers.Rows, r => r.Key == "T1");
    }

    [Fact]
    public async Task Inspector_MixedValuesShowBerbeda()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Click("L1");
        vm.Layers.CtrlClick("T1");
        var ins = vm.Inspector;
        Assert.Equal(2, ins.SelectedCount);
        Assert.Equal("A", ins.Discipline);
        Assert.Null(ins.SheetType);
        Assert.True(ins.TitleIsMixed);
        Assert.Equal(InspectorViewModel.MixedText, ins.TitleDisplay);
        ins.Prefix = "RENCANA";
        ins.ApplyPrefixSuffix.Execute(null);
        Assert.Equal("RENCANA DENAH LANTAI 1", Row(vm, "L1").Label);
        Assert.True(Row(vm, "L1").Edited);
    }

    [Fact]
    public async Task Inspector_SeqDisabledForMulti()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Click("A|1");
        Assert.False(vm.Inspector.IsSeqEnabled);
        vm.Layers.Click("L1");
        Assert.True(vm.Inspector.IsSeqEnabled);
        Assert.Equal(10, vm.Inspector.ManualSeq);
        vm.Inspector.SheetType = 4;
        Assert.StartsWith("A-4", Row(vm, "L1").NewNumber);
    }

    [Fact]
    public async Task CanDrop_LockedOrFull_FalseWithReason()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.ToggleLock("L1");
        Assert.False(vm.Layers.CanDrop(["L1"], "T1", DropPosition.Sebelum, out var reason));
        Assert.Contains("terkunci", reason);

        gw.Sheets.AddRange(Enumerable.Range(2, 98).Select(i => S($"t{i}", $"A-2{i:00}")));
        await vm.LoadAsync();
        Assert.False(vm.Layers.CanDrop(["L2"], "A|2", DropPosition.Ke, out var full));
        Assert.Contains("penuh", full);
    }

    [Fact]
    public async Task ToggleCollapse_AndFilter()
    {
        var (vm, _) = await Loaded();
        vm.Layers.ToggleCollapse("A|1", all: false);
        Assert.DoesNotContain(vm.Layers.Rows, r => r.Key == "L1");
        vm.Layers.Filter = "tampak";
        Assert.Equal(new[] { "A", "A|2", "T1" }, vm.Layers.Rows.Select(r => r.Key));
    }

    [Fact]
    public async Task Batch_RunCancel_ReportsDibatalkan()
    {
        var (vm, gw) = await Loaded();
        gw.CreateGate = new TaskCompletionSource();
        var batch = vm.NewBatch();
        batch.LoadPreset("DENAH SEMUA LANTAI");
        batch.Preview();
        Assert.True(batch.Rows.Count(r => r.Status == Create.PlanStatus.Baru) >= 1);
        var run = batch.RunAsync();
        Assert.True(batch.IsRunning);
        batch.Cancel.Execute(null);
        await run;
        Assert.True(batch.Report!.Dibatalkan);
        Assert.False(batch.IsRunning);
    }

    [Fact]
    public async Task Create_InvalidName_CommandDisabled()
    {
        var (vm, gw) = await Loaded();
        var create = vm.NewCreateSheet();
        Assert.False(create.Create.CanExecute(null)); // empty title
        create.Picker.SelectedKindId = "denah";
        create.Picker.Subject = "LANTAI 3";
        create.ViewMode = Create.ViewMode.PakaiYangAda;
        Assert.False(create.Create.CanExecute(null)); // no view chosen
        Assert.Equal(new[] { "v1" }, create.AvailableViews.Select(v => v.UniqueId));
        create.SelectedViewId = "v1";
        Assert.True(create.Create.CanExecute(null));
        await create.CreateAsync();
        var req = gw.Created.Single().Single();
        Assert.Equal(("A-112", "PUPR_KOP_A3", "v1"), (req.Name.Id.Format(), req.TitleBlockType, req.Views.Single().ExistingViewUniqueId));
    }

    [Fact]
    public async Task Refresh_NoDocument_ShowsStatusInsteadOfThrowing()
    {
        var (vm, gw) = await Loaded();
        gw.LoadError = "Tidak ada dokumen Revit yang aktif";
        await vm.SafeLoadAsync();
        Assert.Equal("Tidak ada dokumen Revit yang aktif", vm.Status);
    }

    [Fact]
    public async Task Inspector_EditNumber_OpensPickerInEditMode_AndApplyNameStages()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Click("L1");
        NamePickerViewModel? opened = null;
        vm.Inspector.EditNumberRequested += p => opened = p;
        vm.Inspector.EditNumber.Execute(null);
        Assert.NotNull(opened);
        Assert.Equal("A-110", opened!.NumberPreview);
        vm.Inspector.ApplyName(new CreateName(new Naming.SheetId("A", 3, 5), "potongan x", null, ""));
        Assert.Equal(("A-305", "POTONGAN X"), (Row(vm, "L1").NewNumber, Row(vm, "L1").Label));
        vm.Layers.Click("A"); // whole discipline = several sheets
        Assert.False(vm.Inspector.EditNumber.CanExecute(null));
    }

    [Fact]
    public async Task Preferences_SaveProject_CallsGatewayAndUpdatesNumbering()
    {
        var (vm, gw) = await Loaded();
        var prefs = new PreferencesViewModel(vm, vm.Snapshot.Prefs) { StrategyUrut = true, Langkah = 10 };
        prefs.SaveProject.Execute(null);
        await Task.Yield();
        Assert.Equal(10, gw.SavedPrefs!.Langkah);
        Assert.True(vm.IsStrategyUrut);
        Assert.Contains("PUPR_KOP_A3", prefs.TitleBlockTypes);
    }
}
