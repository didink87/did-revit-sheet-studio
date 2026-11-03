using SheetStudio.Core.Changes;
using SheetStudio.Core.Create;
using SheetStudio.Core.Model;
using SheetStudio.Core.Numbering;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class CreatePlanTests
{
    private static readonly (string, double)[] Levels = [("Lantai 2", 4), ("Basement 1", -3.5), ("Lantai 1", 0)];

    private static IReadOnlyList<PlannedSheet> Plan(IReadOnlyList<BatchRow> rows, IReadOnlyList<SheetSnapshot>? existing = null) =>
        BatchPlanner.Plan(rows, existing ?? [], ChangeSet.Start(State()), Profiles.Pupr, NumberingOptions.Default);

    private static BatchRow Row(string? kind, string subjek = "", string? nomor = null, string? judul = null) =>
        new(kind, subjek, null, nomor, judul, 0, Kertas.A3, true);

    [Fact]
    public void Matrix_4KindsX3Levels_10Rows()
    {
        var rows = BatchMatrix.Build(["denah", "rencana-plafon", "rencana-pola-lantai", "potongan-bangunan"], Levels, Profiles.Pupr, Kertas.A2, true);
        Assert.Equal(10, rows.Count);
        Assert.Equal(new[] { "BASEMENT 1", "LANTAI 1", "LANTAI 2" }, rows.Where(r => r.KindId == "denah").Select(r => r.Subjek));
        Assert.Equal("Basement 1", rows[0].LevelName);
        Assert.All(rows, r => Assert.Equal(Kertas.A2, r.Kertas));
    }

    [Fact]
    public void Plan_SequentialUniqueNumbers()
    {
        var rows = BatchMatrix.Build(["denah"], Levels, Profiles.Pupr, Kertas.A3, true);
        var plan = Plan(rows, [S("x", "A-110", "DENAH LAMA", kind: "denah", subjek: "LANTAI 9")]);
        Assert.Equal(new[] { "A-111", "A-112", "A-113" }, plan.Select(p => p.Request!.Name.Id.Format()));
        Assert.Equal("DENAH BASEMENT 1", plan[0].Request!.Name.Title);
        Assert.All(plan, p => Assert.Equal(PlanStatus.Baru, p.Status));
        var view = Assert.Single(plan[0].Request!.Views);
        Assert.Equal((ViewMode.BuatBaru, "Denah", "Basement 1", 100), (view.Mode, view.JenisView, view.LevelName, view.Skala));
    }

    [Fact]
    public void Plan_ExistingAndDuplicateFlagged()
    {
        var rows = new[] { Row("denah", "LANTAI 1"), Row("denah", "Lantai 2"), Row("denah", "LANTAI 2") };
        var plan = Plan(rows, [S("x", "A-110", "DENAH LANTAI 1", kind: "denah", subjek: "LANTAI 1")]);
        Assert.Equal(new[] { PlanStatus.SudahAda, PlanStatus.Baru, PlanStatus.Duplikat }, plan.Select(p => p.Status));
        Assert.Null(plan[2].Request);
    }

    [Fact]
    public void Plan_RequestedNumberTaken_FallsBackWithPesan()
    {
        var plan = Plan([Row("denah", "LANTAI 2", nomor: "A-110"), Row("denah", "LANTAI 3", nomor: "A-125")],
            [S("x", "A-110", kind: "denah", subjek: "LANTAI 1")]);
        Assert.Equal("A-111", plan[0].Request!.Name.Id.Format());
        Assert.Contains("A-110", plan[0].Pesan);
        Assert.Equal("A-125", plan[1].Request!.Name.Id.Format());
        Assert.Null(plan[1].Pesan);
    }

    [Fact]
    public void Plan_FreeTitleAndNoKind()
    {
        var plan = Plan([Row(null, judul: "Gambar Khusus", nomor: "A-701"), Row(null)]);
        Assert.Equal(("A-701", "GAMBAR KHUSUS", PlanStatus.Baru), (plan[0].Request!.Name.Id.Format(), plan[0].Request!.Name.Title, plan[0].Status));
        Assert.Equal(PlanStatus.Galat, plan[1].Status);
    }

    [Fact]
    public void Plan_RangeFull_Galat()
    {
        var existing = Enumerable.Range(1, 2).Select(i => S($"b{i}", $"A-10{i}", kind: "blockplan", subjek: $"S{i}")).ToList();
        var plan = Plan([Row("blockplan", "BARU")], existing);
        Assert.Equal(PlanStatus.Galat, plan[0].Status);
        Assert.Contains("penuh", plan[0].Pesan);
    }

    [Fact]
    public void Plan_AbbrevTitle_GalatOnlyWhenEmpty_WarnsOtherwise()
    {
        var plan = Plan([Row(null, judul: "Denah Lt 2", nomor: "A-130")]);
        Assert.Equal(PlanStatus.Baru, plan[0].Status);
        Assert.Contains("singkatan", plan[0].Pesan);
    }

    [Fact]
    public void Preset_DedPupr_ContainsCoreNumbers()
    {
        var rows = Presets.Get("DED PUPR LENGKAP", [("Lantai 1", 0)], Profiles.Pupr, Kertas.A3);
        var numbers = Plan(rows).Where(p => p.Status == PlanStatus.Baru).Select(p => p.Request!.Name.Id.Format()).ToList();
        foreach (var n in new[] { "G-000", "G-001", "A-101", "A-110", "A-201", "A-301", "A-901" })
            Assert.Contains(n, numbers);
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Equal(new[] { "DED PUPR LENGKAP", "DENAH SEMUA LANTAI", "TAMPAK & POTONGAN" }, Presets.Names);
    }
}
