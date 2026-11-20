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
        Assert.Equal("0 pending changes", vm.PendingText);
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
        Assert.Contains("1 change discarded", vm.Status);
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
        Assert.Contains("locked", reason);

        gw.Sheets.AddRange(Enumerable.Range(2, 98).Select(i => S($"t{i}", $"A-2{i:00}")));
        await vm.LoadAsync();
        Assert.False(vm.Layers.CanDrop(["L2"], "A|2", DropPosition.Ke, out var full));
        Assert.Contains("full", full);
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
    public async Task Batch_ReportSetBeforeReload()
    {
        var (vm, gw) = await Loaded();
        gw.CreateGate = new TaskCompletionSource();
        var batch = vm.NewBatch();
        batch.LoadPreset("DENAH SEMUA LANTAI");
        batch.Preview();
        var run = batch.RunAsync();
        gw.LoadError = "putus"; // the post-create reload will fail
        gw.CreateGate.SetResult();
        await run; // reload failure is reported via Status, not thrown
        Assert.StartsWith("Error", vm.Status);
        Assert.NotNull(batch.Report);
        Assert.False(batch.Report!.Dibatalkan);
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
        create.Kertas = Create.Kertas.A3; // kertas & kop wajib dipilih; kop A3 diisi dari preferensi
        await create.CreateAsync();
        var req = gw.Created.Single().Single();
        Assert.Equal(("A-112", "PUPR_KOP_A3", "v1"), (req.Name.Id.Format(), req.TitleBlockType, req.Views.Single().ExistingViewUniqueId));
    }

    [Fact]
    public async Task Create_WithoutKertasOrKop_WarnsAndDoesNotCreate()
    {
        var (vm, gw) = await Loaded();
        var create = vm.NewCreateSheet();
        create.Picker.FreeTitle = "DENAH LANTAI 3";
        Assert.Null(create.Kertas);
        Assert.Null(create.TitleBlockType);
        Assert.Contains("paper", create.Peringatan);
        Assert.Contains("title block", create.Peringatan);
        string? shown = null;
        create.WarningRequested += m => shown = m;
        await create.CreateAsync();
        Assert.NotNull(shown);
        Assert.Empty(gw.Created);

        create.Kertas = Create.Kertas.A1; // kop A1 tidak ada di model (fake hanya punya A3, A2)
        Assert.Null(create.TitleBlockType);
        Assert.Contains("title block", create.Peringatan);
        create.TitleBlockType = "PUPR_KOP_A2";
        Assert.Null(create.Peringatan);
        await create.CreateAsync();
        Assert.Equal("PUPR_KOP_A2", gw.Created.Single().Single().TitleBlockType);
    }

    [Fact]
    public async Task Create_TypedTitle_GuessesViewJenis()
    {
        var (vm, _) = await Loaded();
        var create = vm.NewCreateSheet();
        create.Picker.FreeTitle = "TAMPAK DEPAN";
        Assert.Equal("Tampak", create.NewViewJenis);
        Assert.Equal("Potongan", CreateSheetViewModel.GuessViewJenis("potongan a-a"));
        Assert.Null(CreateSheetViewModel.GuessViewJenis("DETAIL TANGGA"));
    }

    [Fact]
    public async Task Create_SavedTitles_OptionalSaveAndRemove()
    {
        var (vm, _) = await Loaded();
        var create = vm.NewCreateSheet();
        create.Picker.FreeTitle = "denah lantai 3";
        Assert.False(create.Picker.IsTitleSaved);
        Assert.True(create.Picker.SaveTitle.CanExecute(null));
        create.Picker.SaveTitle.Execute(null);
        Assert.Equal(new[] { "DENAH LANTAI 3" }, create.Picker.SavedTitles);
        Assert.True(create.Picker.IsTitleSaved);
        Assert.False(create.Picker.SaveTitle.CanExecute(null));
        Assert.Equal(new[] { "DENAH LANTAI 3" }, vm.NewCreateSheet().Picker.SavedTitles); // dipakai lagi di dialog berikutnya
        create.Picker.RemoveSavedTitle.Execute(null);
        Assert.Empty(create.Picker.SavedTitles);
    }

    [Fact]
    public async Task Batch_MissingKop_WarnsAndDoesNotRun()
    {
        var (vm, gw) = await Loaded();
        var batch = vm.NewBatch();
        batch.Kertas = Create.Kertas.A1; // kop A1 tidak ada di model
        batch.LoadPreset("DENAH SEMUA LANTAI");
        batch.Preview();
        string? shown = null;
        batch.WarningRequested += m => shown = m;
        await batch.RunAsync();
        Assert.Contains("A1", shown);
        Assert.Empty(gw.Created);
        Assert.Equal("Close", batch.CloseLabel);
    }

    [Fact]
    public async Task Batch_AfterRun_CloseLabelSaysSelesai()
    {
        var (vm, _) = await Loaded();
        var batch = vm.NewBatch();
        Assert.Equal("Close", batch.CloseLabel);
        batch.LoadPreset("DENAH SEMUA LANTAI");
        batch.Preview();
        await batch.RunAsync();
        Assert.True(batch.IsDone);
        Assert.StartsWith("✓ Done", batch.CloseLabel);
    }

    [Fact]
    public async Task Refresh_NoDocument_ShowsStatusInsteadOfThrowing()
    {
        var (vm, gw) = await Loaded();
        gw.LoadError = "No active Revit document";
        await vm.SafeLoadAsync();
        Assert.Equal("No active Revit document", vm.Status);
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

    // ── final-review fixes ──────────────────────────────────────
    [Fact]
    public async Task C3_ApplyGatewayThrows_StatusNotCrash()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.RenameInline("T1", "TAMPAK BARU");
        gw.ApplyError = "Revit menolak";
        await ((AsyncRelayCommand)vm.Apply).ExecuteAsync(null);
        Assert.Contains("Revit menolak", vm.Status);
        Assert.Equal(1, vm.PendingCount);
    }

    [Fact]
    public async Task C3_ProhibitedTitle_NotStaged_AndApplyBlockedOnGalat()
    {
        var (vm, _) = await Loaded();
        vm.Layers.RenameInline("T1", "DETAIL 1: TANGGA");
        Assert.Equal(0, vm.PendingCount);
        Assert.Contains("prohibited", vm.Status);
        // a full group (sequencer error) blocks Apply
        vm.Push(vm.Current.Upsert(new SheetChange("L1", ManualSeq: 11)).Next);
        vm.Push(vm.Current.ToggleLock(["L2"]).Next);
        Assert.NotEmpty(vm.Outcome.Errors);
        Assert.False(vm.Apply.CanExecute(null));
    }

    [Fact]
    public async Task AfterCreate_RevealsNewSheet_ClearsFilterExpandsSelects()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.Filter = "TAMPAK";
        vm.Layers.ToggleCollapse("A|1", all: false);
        string? scrolled = null;
        vm.Layers.ScrollIntoViewRequested += k => scrolled = k;
        gw.Sheets.Add(S("new-A-112", "A-112", "DENAH LANTAI 3", kind: "denah"));
        await vm.AfterCreateAsync(new CreateReport([("DENAH LANTAI 3", true, null, "new-A-112")], false));
        Assert.Equal("", vm.Layers.Filter);
        Assert.Contains(vm.Layers.Rows, r => r.Key == "new-A-112");
        Assert.Equal(new[] { "new-A-112" }, vm.Layers.SelectedSheetIds());
        Assert.Equal("new-A-112", scrolled);
    }

    [Fact]
    public async Task AfterCreate_FilterStillMatches_IsKept()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.Filter = "DENAH";
        gw.Sheets.Add(S("new-A-112", "A-112", "DENAH LANTAI 3", kind: "denah"));
        await vm.AfterCreateAsync(new CreateReport([("DENAH LANTAI 3", true, null, "new-A-112")], false));
        Assert.Equal("DENAH", vm.Layers.Filter);
        Assert.Contains(vm.Layers.Rows, r => r.Key == "new-A-112");
    }

    [Fact]
    public async Task I2_CreateKeepsStagedWork()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.RenameInline("T1", "TAMPAK BARU");
        gw.Sheets.Add(S("N1", "A-112", "DENAH LANTAI 3", kind: "denah"));
        await vm.AfterCreateAsync(new CreateReport([("DENAH LANTAI 3", true, null, "N1")], false));
        Assert.Equal("TAMPAK BARU", Row(vm, "T1").Label);
        Assert.True(Row(vm, "N1").IsNew);
    }

    [Fact]
    public async Task I3_ExpectedVersiIsTheVersionWhenStaged()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.RenameInline("T1", "TAMPAK BARU");
        var i = gw.Sheets.FindIndex(s => s.UniqueId == "T1");
        gw.Sheets[i] = gw.Sheets[i] with { Number = "A-299", Versi = "A-299|TAMPAK DEPAN" }; // edited by hand in Revit
        await vm.OnModelChangedAsync();
        Assert.Equal("A-201|TAMPAK DEPAN", vm.BuildPlan().ExpectedVersi["T1"]);
    }

    [Fact]
    public async Task I4_DocumentSwitch_WithPendingWork_KeepsItForTheOriginalDocument()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.RenameInline("T1", "TAMPAK BARU");
        gw.DocumentKey = "doc2";
        gw.Sheets.Clear(); // e.g. a family document is active now
        await vm.ReloadAsync(ReloadReason.GantiDokumen);
        Assert.Equal(1, vm.PendingCount);
        Assert.Contains("another document", vm.Status);
        Assert.Equal("doc1", vm.BuildPlan().DocumentKey); // Apply would be refused by Revit-side check in doc2
        Assert.Equal("TAMPAK BARU", Row(vm, "T1").Label);
    }

    [Fact]
    public async Task I4_DocumentSwitch_WithoutPendingWork_LoadsNewDocument()
    {
        var (vm, gw) = await Loaded();
        gw.DocumentKey = "doc2";
        gw.Sheets.RemoveAll(s => s.UniqueId == "T1");
        await vm.ReloadAsync(ReloadReason.GantiDokumen);
        Assert.Equal("doc2", vm.Snapshot.DocumentKey);
        Assert.DoesNotContain(vm.Layers.Rows, r => r.Key == "T1");
        Assert.Contains("Active document changed", vm.Status);
    }

    [Fact]
    public async Task I6_DisciplineReorderIsPending()
    {
        var (vm, gw) = await Loaded();
        gw.Sheets.Add(S("G1", "G-001", "DAFTAR GAMBAR"));
        await vm.LoadAsync();
        vm.Layers.Drop(["A"], "G", DropPosition.Ke);
        Assert.Equal(1, vm.PendingCount);
        Assert.True(vm.Apply.CanExecute(null));
    }

    [Fact]
    public async Task I5_DragOverLargeSelection_IsFast()
    {
        var gw = new FakeGateway { Sheets = Enumerable.Range(0, 1000).Select(i => S($"s{i}", $"{"GSAEP"[i % 5]}-{1 + i % 9}{1 + i / 50:00}")).ToList() };
        var vm = new StudioViewModel(gw);
        await vm.LoadAsync();
        var keys = vm.Layers.Rows.Where(r => r.Kind == Layers.RowKind.Lembar).Take(200).Select(r => r.Key).ToList();
        var target = vm.Layers.Rows.Where(r => r.Kind == Layers.RowKind.Lembar).Skip(500).First().Key;
        vm.Layers.Evaluate(keys, target, 0.2); // warm-up
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) vm.Layers.Evaluate(keys, target, 0.2);
        Assert.True(sw.ElapsedMilliseconds / 5 < 150, $"{sw.ElapsedMilliseconds / 5} ms per drag-over");
    }

    [Fact]
    public async Task Rebuild_AfterRename_ReusesRowInstances()
    {
        var (vm, _) = await Loaded();
        var l2 = Row(vm, "L2");
        vm.Layers.RenameInline("L1", "DENAH BARU");
        Assert.True(ReferenceEquals(l2, Row(vm, "L2")));
        Assert.Equal("DENAH BARU", Row(vm, "L1").Label);
    }

    [Fact]
    public async Task Push_500Sheets_Under300ms()
    {
        var gw = new FakeGateway
        {
            Sheets = Enumerable.Range(1, 5).SelectMany(t => Enumerable.Range(1, 99).Select(i => S($"s{t}-{i}", $"A-{t}{i:00}", kind: "denah"))).ToList(),
        };
        var vm = new StudioViewModel(gw);
        await vm.LoadAsync();
        vm.Layers.Click("s3-50");
        vm.Layers.MoveDown.Execute(null); // warm-up
        vm.Layers.MoveUp.Execute(null);
        vm.Layers.Click("s2-50");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        vm.Layers.MoveDown.Execute(null);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 300, $"{sw.ElapsedMilliseconds} ms");
    }
}
