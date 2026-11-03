using SheetStudio.Core.ViewModels;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class ReloadTests
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

    [Fact]
    public async Task Reload_ConcurrentTriggers_CoalesceToTwoLoads()
    {
        var (vm, gw) = await Loaded();
        gw.LoadGate = new();
        var loadsBefore = gw.Loads;
        var tasks = new[]
        {
            vm.ReloadAsync(ReloadReason.ModelBerubah),
            vm.ReloadAsync(ReloadReason.ModelBerubah),
            vm.ReloadAsync(ReloadReason.ModelBerubah),
        };
        gw.LoadGate.SetResult();
        await Task.WhenAll(tasks);
        Assert.Equal(2, gw.Loads - loadsBefore);
    }

    [Fact]
    public async Task Reload_EditDuringLoad_IsKept()
    {
        var (vm, gw) = await Loaded();
        gw.LoadGate = new();
        var t = vm.ReloadAsync(ReloadReason.ModelBerubah);
        vm.Layers.Click("L1");
        vm.Layers.MarkDelete();
        gw.Sheets.Add(S("L3", "A-112", "DENAH LANTAI 3", kind: "denah", subjek: "LANTAI 3"));
        gw.LoadGate.SetResult();
        await t;
        Assert.Equal(1, vm.PendingCount);
        Assert.Contains(vm.Layers.Rows, r => r.Key == "L3");
    }

    [Fact]
    public async Task ModelChanged_WhileHidden_LoadsOnShow()
    {
        var (vm, gw) = await Loaded();
        vm.IsPaneVisible = false;
        gw.Sheets.Add(S("L3", "A-112", "DENAH LANTAI 3", kind: "denah", subjek: "LANTAI 3"));
        var loads = gw.Loads;
        gw.RaiseModelChanged();
        Assert.Equal(loads, gw.Loads);
        Assert.True(vm.IsStale);
        vm.IsPaneVisible = true;
        await vm.OnShownAsync();
        Assert.Contains(vm.Layers.Rows, r => r.Key == "L3");
        Assert.False(vm.IsStale);
    }

    [Fact]
    public async Task ModelChanged_HiddenDuringLoad_StaysStale()
    {
        var (vm, gw) = await Loaded();
        gw.LoadGate = new();
        var t = vm.ReloadAsync(ReloadReason.ModelBerubah);
        vm.IsPaneVisible = false;
        gw.RaiseModelChanged();
        gw.LoadGate.SetResult();
        await t;
        Assert.True(vm.IsStale);
    }

    [Fact]
    public async Task Show_NotStale_DoesNotLoad()
    {
        var (vm, gw) = await Loaded();
        var loads = gw.Loads;
        await vm.OnShownAsync();
        Assert.Equal(loads, gw.Loads);
    }

    [Fact]
    public async Task Show_NeverLoaded_Loads()
    {
        var gw = new FakeGateway { Sheets = [S("L1", "A-110", "DENAH LANTAI 1", kind: "denah", subjek: "LANTAI 1")] };
        var vm = new StudioViewModel(gw);
        await vm.OnShownAsync();
        Assert.Equal(1, gw.Loads);
        Assert.Contains(vm.Layers.Rows, r => r.Key == "L1");
    }

    [Fact]
    public async Task Reload_SameDocumentNewPath_AdoptsKey()
    {
        var (vm, gw) = await Loaded();
        gw.DocumentId = "P1";
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        vm.Layers.Click("L1");
        vm.Layers.MarkDelete();
        gw.DocumentKey = "C:\\baru.rvt";
        gw.Sheets.Add(S("L3", "A-112", "DENAH LANTAI 3", kind: "denah", subjek: "LANTAI 3"));
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        Assert.Equal("C:\\baru.rvt", vm.Snapshot.DocumentKey);
        Assert.Equal(1, vm.PendingCount);
        Assert.Contains(vm.Layers.Rows, r => r.Key == "L3");
        Assert.Equal("C:\\baru.rvt", vm.BuildPlan().DocumentKey);
    }

    [Fact]
    public async Task Reload_DifferentDocument_StillKeepsPendingAndWarns()
    {
        var (vm, gw) = await Loaded();
        gw.DocumentId = "P1";
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        vm.Layers.Click("L1");
        vm.Layers.MarkDelete();
        gw.DocumentKey = "C:\\lain.rvt";
        gw.DocumentId = "P2";
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        Assert.Equal("doc1", vm.Snapshot.DocumentKey);
        Assert.Equal(1, vm.PendingCount);
        Assert.Contains("milik dokumen lain", vm.Status);
    }

    [Fact]
    public async Task Reload_SameIdButOldDocStillOpen_DoesNotAdopt()
    {
        var (vm, gw) = await Loaded();
        gw.DocumentId = "P1";
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        vm.Layers.Click("L1");
        vm.Layers.MarkDelete();
        gw.DocumentKey = "C:\\salinan.rvt";
        gw.OpenKeys = ["doc1", "C:\\salinan.rvt"];
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        Assert.Equal("doc1", vm.Snapshot.DocumentKey);
        Assert.Equal(1, vm.PendingCount);
        Assert.Contains("milik dokumen lain", vm.Status);
    }

    [Fact]
    public async Task Reload_EmptyDocumentId_DoesNotAdopt()
    {
        var (vm, gw) = await Loaded();
        vm.Layers.Click("L1");
        vm.Layers.MarkDelete();
        gw.DocumentKey = "C:\\lain.rvt";
        await vm.ReloadAsync(ReloadReason.ModelBerubah);
        Assert.Equal("doc1", vm.Snapshot.DocumentKey);
        Assert.Equal(1, vm.PendingCount);
        Assert.Contains("milik dokumen lain", vm.Status);
    }

    [Fact]
    public async Task Create_GatewayThrows_StillReloads()
    {
        var (vm, gw) = await Loaded();
        gw.CreateError = "putus";
        gw.Sheets.Add(S("L3", "A-112", "DENAH LANTAI 3", kind: "denah", subjek: "LANTAI 3"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            vm.CreateThenReloadAsync(() => gw.CreateAsync([], new Progress<(int, int, string)>(), default)));
        Assert.Contains(vm.Layers.Rows, r => r.Key == "L3");
    }
}
