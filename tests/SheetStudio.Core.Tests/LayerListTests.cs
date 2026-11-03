using System.Diagnostics;
using SheetStudio.Core.Layers;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class LayerListTests
{
    private static string Keys(IEnumerable<LayerRow> rows) =>
        string.Join(",", rows.Select(r => r.Kind == RowKind.Lembar ? r.Number : r.Key));

    [Fact]
    public void Build_GroupsByDisciplineThenType_InNcsOrder()
    {
        var rows = LayerListBuilder.Build([S("1", "G-001"), S("2", "A-111"), S("3", "S-101"), S("4", "A-201")], State(), Profiles.Pupr, None, null);
        Assert.Equal("G,G|0,G-001,S,S|1,S-101,A,A|1,A-111,A|2,A-201", Keys(rows));
        Assert.Equal(new[] { 0, 1, 2 }, rows.Take(3).Select(r => r.Indent));
        Assert.Equal("A|1", rows.Single(r => r.Number == "A-111").GroupKey);
        Assert.Equal("ARSITEKTUR", rows.Single(r => r.Key == "A").Label);
        Assert.Equal("DENAH", rows.Single(r => r.Key == "A|1").Label);
        Assert.Equal(2, rows.Single(r => r.Key == "A").Count);
    }

    [Fact]
    public void Build_UsesSavedOrderWithinGroup()
    {
        var order = new Dictionary<string, IReadOnlyList<string>> { ["A|1"] = ["L2", "L1"] };
        var rows = LayerListBuilder.Build([S("L1", "A-111"), S("L2", "A-112"), S("L3", "A-110")], State(order), Profiles.Pupr, None, null);
        Assert.Equal("A,A|1,A-112,A-111,A-110", Keys(rows));
    }

    [Fact]
    public void Build_UnknownNumbersGoToQuestionGroupLocked()
    {
        var rows = LayerListBuilder.Build([S("1", "K-12"), S("2", "A-111")], State(), Profiles.Pupr, None, null);
        Assert.Equal("A,A|1,A-111,?,?|?,K-12", Keys(rows));
        Assert.True(rows.Single(r => r.Number == "K-12").Locked);
    }

    [Fact]
    public void Snapshot_NotEditableSheetsLocked()
    {
        var rows = LayerListBuilder.Build([S("1", "A-111", editable: false), S("2", "A-112")], State(), Profiles.Pupr, None, null);
        Assert.True(rows.Single(r => r.SheetUniqueId == "1").Locked);
        Assert.False(rows.Single(r => r.SheetUniqueId == "1").Editable);
        Assert.False(rows.Single(r => r.SheetUniqueId == "2").Locked);
    }

    [Fact]
    public void Build_StateLockedSheetIsLocked()
    {
        var rows = LayerListBuilder.Build([S("1", "A-111")], State(locked: ["1"]), Profiles.Pupr, None, null);
        Assert.True(rows.Single(r => r.SheetUniqueId == "1").Locked);
    }

    [Fact]
    public void Build_CollapsedGroupHidesChildren()
    {
        var rows = LayerListBuilder.Build([S("1", "A-111"), S("2", "A-201"), S("3", "S-101")], State(), Profiles.Pupr, new HashSet<string> { "A|1", "S" }, null);
        Assert.Equal("S,A,A|1,A|2,A-201", Keys(rows));
        Assert.False(rows.Single(r => r.Key == "A|1").Expanded);
    }

    [Fact]
    public void Build_FilterHidesEmptyGroups()
    {
        var rows = LayerListBuilder.Build([S("1", "A-111", "DENAH LANTAI 1"), S("2", "A-201", "TAMPAK DEPAN"), S("3", "S-101", "DENAH PONDASI")], State(), Profiles.Pupr, None, "denah");
        Assert.Equal("S,S|1,S-101,A,A|1,A-111", Keys(rows));
        var byNumber = LayerListBuilder.Build([S("1", "A-111"), S("2", "A-201")], State(), Profiles.Pupr, None, "a-2");
        Assert.Equal("A,A|2,A-201", Keys(byNumber));
    }

    [Fact]
    public void OrderedSheets_FlattensInPanelOrder()
    {
        var order = new Dictionary<string, IReadOnlyList<string>> { ["A|1"] = ["L2", "L1"] };
        var list = LayerListBuilder.OrderedSheets([S("L1", "A-111"), S("L2", "A-112"), S("G", "G-001")], State(order), Profiles.Pupr);
        Assert.Equal(new[] { "G", "L2", "L1" }, list.Select(s => s.UniqueId));
    }

    [Fact]
    public void Build_1000Sheets_Under100ms()
    {
        var sheets = Enumerable.Range(0, 1000).Select(i => S($"id{i}", $"{"GSAEP"[i % 5]}-{1 + i % 9}{1 + i % 99:00}")).ToList();
        LayerListBuilder.Build(sheets, State(), Profiles.Pupr, None, null); // warm-up
        var sw = Stopwatch.StartNew();
        var rows = LayerListBuilder.Build(sheets, State(), Profiles.Pupr, None, null);
        sw.Stop();
        Assert.Equal(1000, rows.Count(r => r.Kind == RowKind.Lembar));
        Assert.True(sw.ElapsedMilliseconds < 100, $"{sw.ElapsedMilliseconds} ms");
    }
}
