using System.Diagnostics;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class SequencerTests
{
    private static readonly NumberingOptions Rentang = new(Strategy.Rentang, 1, 1);

    private static ChangeSet Norm(Model.ProjectState st) => ChangeSet.Start(st) with { NormalizeAll = true };

    [Fact]
    public void Compute_UntouchedGroupsKeepNumbers()
    {
        var sheets = new[] { S("t", "A-201", kind: "tampak-bangunan"), S("a", "A-110", kind: "denah"), S("b", "A-111", kind: "denah") };
        var cs = ChangeSet.Start(State()).MoveSelection(["b"], "A|1", 0, sheets, Profiles.Pupr).Next;
        var n = Numbers(Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang));
        Assert.Equal(("A-201", "A-110", "A-111"), (n["t"], n["b"], n["a"]));
        Assert.All(Sequencer.Compute(sheets, ChangeSet.Start(State()), Profiles.Pupr, Rentang).Results, r => Assert.False(r.Changed));
    }

    [Fact]
    public void Compute_NormalizeAll_RenumbersEverything()
    {
        var sheets = new[] { S("t", "A-201", kind: "tampak-bangunan") };
        Assert.Equal("A-210", Numbers(Sequencer.Compute(sheets, Norm(State()), Profiles.Pupr, Rentang))["t"]);
    }

    [Fact]
    public void LockToggle_DoesNotRenumberGroup()
    {
        var sheets = new[] { S("t", "A-201", kind: "tampak-bangunan"), S("u", "A-205", kind: "tampak-bangunan") };
        var cs = ChangeSet.Start(State()).ToggleLock(["t"]).Next;
        Assert.Equal(1, cs.PendingCount);
        Assert.All(Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang).Results, r => Assert.False(r.Changed));
        Assert.Equal(0, cs.ToggleLock(["t"]).Next.PendingCount);
    }

    private static Dictionary<string, string> Numbers(SequenceOutcome o) => o.Results.ToDictionary(r => r.UniqueId, r => r.NewNumber);

    [Fact]
    public void Rentang_FollowsPanelOrder()
    {
        var sheets = new[] { S("L1", "A-110", kind: "denah"), S("L2", "A-111", kind: "denah") };
        var cs = Norm(State(new() { ["A|1"] = ["L2", "L1"] }));
        var o = Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang);
        Assert.Empty(o.Errors);
        Assert.Equal("A-110", Numbers(o)["L2"]);
        Assert.Equal("A-111", Numbers(o)["L1"]);
        Assert.True(o.Results.All(r => r.Changed));
    }

    [Fact]
    public void Rentang_KindsUseOwnRanges_NoKindUsesWholeType()
    {
        var sheets = new[] { S("D", "A-150", kind: "denah"), S("P", "A-112", kind: "rencana-plafon"), S("X", "A-160") };
        var n = Numbers(Sequencer.Compute(sheets, Norm(State()), Profiles.Pupr, Rentang));
        Assert.Equal(("A-110", "A-145"), (n["D"], n["P"]));
        Assert.Equal("A-101", n["X"]);
    }

    [Fact]
    public void Urut_Step10()
    {
        var sheets = Enumerable.Range(0, 5).Select(i => S($"s{i}", $"A-1{10 + i}")).ToArray();
        var o = Sequencer.Compute(sheets, Norm(State()), Profiles.Pupr, new NumberingOptions(Strategy.Urut, 1, 10));
        Assert.Equal(new[] { "A-101", "A-111", "A-121", "A-131", "A-141" }, o.Results.Select(r => r.NewNumber));
    }

    [Fact]
    public void Urut_GStartsAtZero()
    {
        var o = Sequencer.Compute([S("a", "G-003"), S("b", "G-007")], Norm(State()), Profiles.Pupr, new NumberingOptions(Strategy.Urut, 1, 1));
        Assert.Equal(new[] { "G-000", "G-001" }, o.Results.Select(r => r.NewNumber));
    }

    [Fact]
    public void LockedKeepsNumberOthersFlowAround()
    {
        var sheets = new[] { S("a", "A-110", kind: "denah"), S("b", "A-111", kind: "denah"), S("c", "A-115", kind: "denah"), S("d", "A-120", kind: "denah") };
        var n = Numbers(Sequencer.Compute(sheets, Norm(State(locked: ["b"])), Profiles.Pupr, Rentang));
        Assert.Equal(new[] { "A-110", "A-111", "A-112", "A-113" }, new[] { n["a"], n["b"], n["c"], n["d"] });
        var moved = Norm(State(new() { ["A|1"] = ["c", "d", "a", "b"] }, locked: ["b"]));
        var m = Numbers(Sequencer.Compute(sheets, moved, Profiles.Pupr, Rentang));
        Assert.Equal(("A-110", "A-112", "A-113", "A-111"), (m["c"], m["d"], m["a"], m["b"]));
    }

    [Fact]
    public void NonUdsNumbersKeptAndUnchanged()
    {
        var o = Sequencer.Compute([S("x", "K-12"), S("a", "A-115")], ChangeSet.Start(State()), Profiles.Pupr, Rentang);
        var x = o.Results.Single(r => r.UniqueId == "x");
        Assert.Equal(("K-12", false), (x.NewNumber, x.Changed));
    }

    [Fact]
    public void NotEditableSheetIsOccupied()
    {
        var n = Numbers(Sequencer.Compute([S("a", "A-115", kind: "denah"), S("b", "A-110", kind: "denah", editable: false)],
            Norm(State()), Profiles.Pupr, Rentang));
        Assert.Equal(("A-111", "A-110"), (n["a"], n["b"]));
    }

    [Fact]
    public void ManualSeq_Honored_And_Taken_ReportsError()
    {
        var sheets = new[] { S("a", "A-110", kind: "denah"), S("b", "A-111", kind: "denah") };
        var cs = ChangeSet.Start(State()).Upsert(new SheetChange("b", ManualSeq: 30)).Next;
        Assert.Equal("A-130", Numbers(Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang))["b"]);

        var clash = ChangeSet.Start(State(locked: ["a"])).Upsert(new SheetChange("b", ManualSeq: 10)).Next;
        var o = Sequencer.Compute(sheets, clash, Profiles.Pupr, Rentang);
        Assert.Contains(o.Errors, e => e.Contains("A-110") && e.Contains("in use"));
        Assert.Equal("A-111", Numbers(o)["b"]);
    }

    [Fact]
    public void MovedToOtherType_GetsNumberInNewGroup()
    {
        var sheets = new[] { S("a", "A-110", kind: "denah"), S("b", "A-111", kind: "denah"), S("t", "A-201") };
        var cs = ChangeSet.Start(State()).MoveSelection(["b"], "A|2", 1, sheets, Profiles.Pupr).Next;
        var n = Numbers(Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang));
        Assert.Equal(("A-201", "A-202"), (n["t"], n["b"]));
    }

    [Fact]
    public void DropIntoFullGroup_Rejected()
    {
        var full = Enumerable.Range(1, 99).Select(i => S($"t{i}", $"A-2{i:00}")).ToList();
        var sheets = full.Append(S("b", "A-111")).ToList();
        var ok = Sequencer.CanAccept(["b"], "A|2", sheets, ChangeSet.Start(State()), Profiles.Pupr, Rentang, out var reason);
        Assert.False(ok);
        Assert.Contains("full", reason);
        Assert.True(Sequencer.CanAccept(["b"], "A|3", sheets, ChangeSet.Start(State()), Profiles.Pupr, Rentang, out _));
    }

    [Fact]
    public void FullGroup_ReportsErrorAndKeepsNumbers()
    {
        var sheets = Enumerable.Range(10, 31).Select(i => S($"d{i}", i <= 39 ? $"A-1{i}" : "A-150", kind: "denah")).ToList(); // 31 denah, range 10–39 holds 30
        var o = Sequencer.Compute(sheets, Norm(State()), Profiles.Pupr, Rentang);
        Assert.Contains(o.Errors, e => e.Contains("full") && e.Contains("A-1"));
        Assert.All(o.Results, r => Assert.False(r.Changed));
    }

    [Fact]
    public void DeleteMarked_FreesNumberForOthers()
    {
        var sheets = new[] { S("a", "A-110", kind: "denah"), S("b", "A-111", kind: "denah") };
        var cs = ChangeSet.Start(State()).MarkDelete(["a"], true).Next;
        var o = Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang);
        Assert.Equal("A-110", Numbers(o)["b"]);
        Assert.DoesNotContain(o.Results, r => r.UniqueId == "a");
    }

    [Fact]
    public void SortKey_FollowsPanelPosition()
    {
        var sheets = new[] { S("L1", "A-110", kind: "denah"), S("L2", "A-111", kind: "denah") };
        var cs = Norm(State(new() { ["A|1"] = ["L2", "L1"] }));
        var r = Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang).Results.ToDictionary(x => x.UniqueId);
        Assert.Equal("08.A.1.0000", r["L2"].SortKey);
        Assert.Equal("08.A.1.0001", r["L1"].SortKey);
    }

    [Fact]
    public void TwoPhase_TempsUnique()
    {
        var steps = TwoPhasePlan.Build([new("a", "A-110", "A-111", "", true), new("b", "A-111", "A-110", "", true), new("c", "A-112", "A-112", "", false)]);
        Assert.Equal(2, steps.Count);
        Assert.Equal(2, steps.Select(s => s.Temp).Distinct().Count());
        Assert.All(steps, s => Assert.Matches(@"^SSTMP-[0-9A-F]{6}-\d{3}$", s.Temp));
        Assert.All(steps, s => Assert.DoesNotContain(s.Temp, c => "{}[]|;<>?`~".Contains(c))); // Revit-prohibited
        Assert.All(steps, s => Assert.False(SheetId.Parse(s.Temp, Profiles.Pupr).Ok));
    }

    [Fact]
    public void Compute_1000Sheets_Under100ms()
    {
        var sheets = Enumerable.Range(0, 1000).Select(i => S($"id{i}", $"{"GSAEP"[i % 5]}-{1 + i % 9}{1 + i % 99:00}")).ToList();
        var cs = ChangeSet.Start(State());
        Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang);
        var sw = Stopwatch.StartNew();
        var o = Sequencer.Compute(sheets, cs, Profiles.Pupr, Rentang);
        sw.Stop();
        Assert.Equal(1000, o.Results.Count);
        Assert.True(sw.ElapsedMilliseconds < 100, $"{sw.ElapsedMilliseconds} ms");
    }
}
