using SheetStudio.Core.Layers;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class SelectionTests
{
    // rows: A, A|1, s1(A-110), s2(A-111), s3(A-112), A|2, s4(A-201)
    private static readonly IReadOnlyList<LayerRow> Rows = LayerListBuilder.Build(
        [S("s1", "A-110"), S("s2", "A-111"), S("s3", "A-112"), S("s4", "A-201")], State(), Profiles.Pupr, None, null);

    [Fact]
    public void Click_SelectsOne_SetsAnchor()
    {
        var sel = new SelectionModel();
        sel.Click("s1"); sel.Click("s2");
        Assert.Equal(new[] { "s2" }, sel.Selected);
        Assert.Equal("s2", sel.Anchor);
    }

    [Fact]
    public void CtrlClick_Toggles()
    {
        var sel = new SelectionModel();
        var changes = 0; sel.Changed += () => changes++;
        sel.Click("s1"); sel.CtrlClick("s3"); sel.CtrlClick("s1");
        Assert.Equal(new[] { "s3" }, sel.Selected);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void ShiftClick_SelectsRangeFromAnchor()
    {
        var sel = new SelectionModel();
        sel.Click("s3"); sel.ShiftClick("s1", Rows);
        Assert.Equal(new[] { "s1", "s2", "s3" }, sel.SelectedSheetIds(Rows));
        Assert.Equal("s3", sel.Anchor);
    }

    [Fact]
    public void ShiftClick_AcrossExpandedHeader_DoesNotSelectWholeGroup()
    {
        // rows A|1: s1 s2 s3, A|2: s4, s5 — range s3..s4 crosses the open A|2 header
        var rows = LayerListBuilder.Build([S("s1", "A-110"), S("s2", "A-111"), S("s3", "A-112"), S("s4", "A-201"), S("s5", "A-202")],
            State(), Profiles.Pupr, None, null);
        var sel = new SelectionModel();
        sel.Click("s3"); sel.ShiftClick("s4", rows);
        Assert.Equal(new[] { "s3", "s4" }, sel.SelectedSheetIds(rows));
    }

    [Fact]
    public void CtrlShiftClick_AddsRange()
    {
        var sel = new SelectionModel();
        sel.Click("s4"); sel.CtrlClick("s1"); sel.CtrlShiftClick("s2", Rows);
        Assert.Equal(new[] { "s1", "s2", "s4" }, sel.SelectedSheetIds(Rows));
    }

    [Fact]
    public void ClickGroup_SelectsChildren()
    {
        var sel = new SelectionModel();
        sel.Click("A|1");
        Assert.Equal(new[] { "s1", "s2", "s3" }, sel.SelectedSheetIds(Rows));
        sel.Click("A");
        Assert.Equal(new[] { "s1", "s2", "s3", "s4" }, sel.SelectedSheetIds(Rows));
    }

    [Fact]
    public void SelectAll_OnlyVisibleSheets()
    {
        var visible = LayerListBuilder.Build([S("s1", "A-110"), S("s4", "A-201")], State(), Profiles.Pupr, new HashSet<string> { "A|2" }, null);
        var sel = new SelectionModel();
        sel.SelectAll(visible);
        Assert.Equal(new[] { "s1" }, sel.Selected);
    }

    [Fact]
    public void SelectedSheetIds_DedupesGroupAndChild()
    {
        var sel = new SelectionModel();
        sel.Click("s2"); sel.CtrlClick("A|1");
        Assert.Equal(new[] { "s1", "s2", "s3" }, sel.SelectedSheetIds(Rows));
    }

    [Fact]
    public void Clear_Empties()
    {
        var sel = new SelectionModel();
        sel.Click("s2"); sel.Clear();
        Assert.Empty(sel.Selected);
        Assert.Null(sel.Anchor);
    }
}
