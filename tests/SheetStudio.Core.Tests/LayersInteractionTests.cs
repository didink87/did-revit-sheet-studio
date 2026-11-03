using SheetStudio.Core.ViewModels;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class LayersInteractionTests
{
    private static async Task<(StudioViewModel Vm, FakeGateway Gw)> Loaded()
    {
        var gw = new FakeGateway
        {
            Sheets = [S("L1", "A-110", "DENAH LANTAI 1", kind: "denah"), S("L2", "A-111", "DENAH LANTAI 2", kind: "denah"), S("T1", "A-201", "TAMPAK")],
        };
        var vm = new StudioViewModel(gw);
        await vm.LoadAsync();
        return (vm, gw);
    }

    private static LayerRowViewModel Row(StudioViewModel vm, string key) => vm.Layers.Rows.Single(r => r.Key == key);

    [Fact]
    public async Task Evaluate_SheetRow_UpperHalfBefore_LowerHalfAfter()
    {
        var (vm, _) = await Loaded();
        Assert.Equal((DropPosition.Sebelum, true), Pick(vm.Layers.Evaluate(["L2"], "L1", 0.2)));
        Assert.Equal((DropPosition.Sesudah, true), Pick(vm.Layers.Evaluate(["L1"], "L2", 0.8)));
        Assert.Equal(DropPosition.Ke, vm.Layers.Evaluate(["L1"], "A|2", 0.5).Pos);
    }

    [Fact]
    public async Task Evaluate_SetsAndClearsDropHint()
    {
        var (vm, _) = await Loaded();
        vm.Layers.ShowDropHint(["L2"], "L1", 0.2);
        Assert.Equal(DropHint.Atas, Row(vm, "L1").DropHint);
        vm.Layers.ShowDropHint(["L2"], "A|2", 0.5);
        Assert.Equal(DropHint.None, Row(vm, "L1").DropHint);
        Assert.Equal(DropHint.Ke, Row(vm, "A|2").DropHint);
        vm.Layers.ToggleLock("L2");
        var e = vm.Layers.ShowDropHint(["L2"], "L1", 0.2);
        Assert.False(e.Allowed);
        Assert.Equal(DropHint.Ditolak, Row(vm, "L1").DropHint);
        vm.Layers.ClearDropHint();
        Assert.All(vm.Layers.Rows, r => Assert.Equal(DropHint.None, r.DropHint));
    }

    [Fact]
    public async Task InlineRename_Commit_And_Cancel()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Click("L1");
        vm.Layers.BeginRename.Execute(null);
        var row = Row(vm, "L1");
        Assert.True(row.IsEditing);
        row.EditText = "denah lantai satu";
        vm.Layers.CommitRename(row);
        Assert.Equal("DENAH LANTAI SATU", Row(vm, "L1").Label);

        vm.Layers.Click("L2");
        vm.Layers.BeginRename.Execute(null);
        Row(vm, "L2").EditText = "X";
        vm.Layers.CancelRename(Row(vm, "L2"));
        Assert.Equal("DENAH LANTAI 2", Row(vm, "L2").Label);
        Assert.False(Row(vm, "L2").IsEditing);
    }

    [Fact]
    public async Task BeginRename_NeedsSingleSheet()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Click("A|1");
        Assert.False(vm.Layers.BeginRename.CanExecute(null));
    }

    [Fact]
    public async Task LockAndEyeSelected()
    {
        var (vm, _) = await Loaded();
        vm.Layers.Click("L1");
        vm.Layers.CtrlClick("T1");
        vm.Layers.ToggleLockSelected.Execute(null);
        Assert.True(Row(vm, "L1").Locked && Row(vm, "T1").Locked);
        vm.Layers.ToggleEyeSelected.Execute(null);
        Assert.False(Row(vm, "L1").InSheetList);
    }

    [Fact]
    public async Task Thumbnails_LoadAndSurviveRebuild()
    {
        var (vm, gw) = await Loaded();
        gw.Thumb = [1, 2, 3];
        await vm.Layers.LoadThumbnailsAsync(["L1", "A|1"]);
        Assert.Equal(new byte[] { 1, 2, 3 }, Row(vm, "L1").Thumbnail);
        vm.Layers.Filter = "denah";
        Assert.Equal(new byte[] { 1, 2, 3 }, Row(vm, "L1").Thumbnail);
        Assert.Equal(1, gw.ThumbCalls);
        await vm.Layers.LoadThumbnailsAsync(["L1"]);
        Assert.Equal(1, gw.ThumbCalls); // cached
    }

    private static (DropPosition, bool) Pick(DropEvaluation e) => (e.Pos, e.Allowed);
}
