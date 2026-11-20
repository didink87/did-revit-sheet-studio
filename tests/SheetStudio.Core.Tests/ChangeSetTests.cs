using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class ChangeSetTests
{
    private static readonly IReadOnlyList<SheetSnapshot> Sheets =
        [S("L1", "A-110"), S("L2", "A-111"), S("L3", "A-112"), S("T1", "A-201"), S("G1", "G-001")];

    private static IReadOnlyList<string> Order(ChangeSet cs, string group, IReadOnlyList<SheetSnapshot>? sheets = null) =>
        cs.GroupOrder(group, sheets ?? Sheets, Profiles.Pupr);

    [Fact]
    public void MoveSelection_KeepsRelativeOrder()
    {
        var r = ChangeSet.Start(State()).MoveSelection(["L3", "L1"], "A|1", 0, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "L1", "L3", "L2" }, Order(r.Next, "A|1"));
        Assert.Null(r.Pesan);
        Assert.Equal(2, r.Next.PendingCount);
    }

    [Fact]
    public void MoveSelection_IndexCountsMovedItems()
    {
        // drop L1 after L2 (index 2 in current list L1,L2,L3) → L2, L1, L3
        var r = ChangeSet.Start(State()).MoveSelection(["L1"], "A|1", 2, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "L2", "L1", "L3" }, Order(r.Next, "A|1"));
    }

    [Fact]
    public void MoveSelection_SkipsHeadersAndLocked()
    {
        var r = ChangeSet.Start(State(locked: ["L2"])).MoveSelection(["A|1", "L2", "L3"], "A|1", 0, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "L3", "L1", "L2" }, Order(r.Next, "A|1"));
        Assert.Equal("1 locked sheet not moved", r.Pesan);
    }

    [Fact]
    public void MoveSelection_IntoOtherTypeGroup_SetsNewType()
    {
        var r = ChangeSet.Start(State()).MoveSelection(["L2"], "A|2", 0, Sheets, Profiles.Pupr);
        var c = r.Next.Changes["L2"];
        Assert.Equal(("A", 2), (c.NewDiscipline, c.NewType));
        Assert.Equal(new[] { "L2", "T1" }, Order(r.Next, "A|2"));
        Assert.Equal(new[] { "L1", "L3" }, Order(r.Next, "A|1"));
        Assert.Equal("A|2", r.Next.EffectiveGroup(Sheets[1], Profiles.Pupr));
    }

    [Fact]
    public void MoveSelection_BackToOriginalGroup_ClearsGroupChange()
    {
        var cs = ChangeSet.Start(State()).MoveSelection(["L2"], "A|2", 0, Sheets, Profiles.Pupr).Next;
        var back = cs.MoveSelection(["L2"], "A|1", 1, Sheets, Profiles.Pupr).Next;
        Assert.False(back.Changes.ContainsKey("L2"));
    }

    [Fact]
    public void MoveSelection_OntoDisciplineHeader_KeepsOwnType()
    {
        var r = ChangeSet.Start(State()).MoveSelection(["L2"], "S", 0, Sheets, Profiles.Pupr);
        Assert.Equal(("S", 1), (r.Next.Changes["L2"].NewDiscipline, r.Next.Changes["L2"].NewType));
    }

    [Fact]
    public void MoveSelection_IntoUnknownGroup_Rejected()
    {
        var start = ChangeSet.Start(State());
        var r = start.MoveSelection(["L2"], LayerListBuilder.UnknownType, 0, Sheets, Profiles.Pupr);
        Assert.Same(start, r.Next);
        Assert.NotNull(r.Pesan);
    }

    [Fact]
    public void MoveBy_AtTop_NoOp()
    {
        var r = ChangeSet.Start(State()).MoveBy(["L1"], -1, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "L1", "L2", "L3" }, Order(r.Next, "A|1"));
        Assert.True(r.Next.IsEmpty);
    }

    [Fact]
    public void MoveBy_BlockMovesTogether()
    {
        var r = ChangeSet.Start(State()).MoveBy(["L2", "L3"], -1, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "L2", "L3", "L1" }, Order(r.Next, "A|1"));
    }

    [Fact]
    public void MoveToEdge_Bottom()
    {
        var r = ChangeSet.Start(State()).MoveToEdge(["L1", "L2"], top: false, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "L3", "L1", "L2" }, Order(r.Next, "A|1"));
    }

    [Fact]
    public void MoveDisciplineGroup_OutOfNcsOrder_Warns()
    {
        var r = ChangeSet.Start(State()).MoveDisciplineGroup("A", 0, Sheets, Profiles.Pupr);
        Assert.Equal(new[] { "A", "G" }, r.Next.State.DisciplineOrder);
        Assert.Contains("UDS", r.Pesan);
        var ok = r.Next.MoveDisciplineGroup("A", 1, Sheets, Profiles.Pupr);
        Assert.Null(ok.Pesan);
    }

    [Fact]
    public void Upsert_MergesFields()
    {
        var cs = ChangeSet.Start(State())
            .Upsert(new SheetChange("L1", NewTitle: "DENAH LANTAI 1")).Next
            .Upsert(new SheetChange("L1", InSheetList: false)).Next;
        var c = cs.Changes["L1"];
        Assert.Equal(("DENAH LANTAI 1", (bool?)false), (c.NewTitle, c.InSheetList));
    }

    [Fact]
    public void ToggleLock_AllLockedUnlocks()
    {
        var cs = ChangeSet.Start(State()).ToggleLock(["L1", "L2"]).Next;
        Assert.True(cs.State.Locked.SetEquals(["L1", "L2"]));
        cs = cs.ToggleLock(["L1", "L2"]).Next;
        Assert.Empty(cs.State.Locked);
    }

    [Fact]
    public void MarkDelete_ThenUndo_Restores()
    {
        var start = ChangeSet.Start(State());
        var undo = new UndoStack();
        undo.Reset(start);
        var del = start.MarkDelete(["L1"], true).Next;
        undo.Push(del);
        Assert.True(del.Changes["L1"].Delete);
        Assert.Same(start, undo.Undo());
        Assert.Same(del, undo.Redo());
        Assert.Empty(del.MarkDelete(["L1"], false).Next.Changes);
    }

    [Fact]
    public void Undo_Redo_Sequence()
    {
        var s0 = ChangeSet.Start(State());
        var undo = new UndoStack();
        undo.Reset(s0);
        var s1 = s0.MarkDelete(["L1"], true).Next; undo.Push(s1);
        var s2 = s1.MarkDelete(["L2"], true).Next; undo.Push(s2);
        var s3 = s2.MarkDelete(["L3"], true).Next; undo.Push(s3);
        undo.Undo(); undo.Undo();
        Assert.Same(s2, undo.Redo());
        Assert.True(undo.CanRedo);
        var s4 = s2.ToggleLock(["T1"]).Next; undo.Push(s4);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void Undo_Limit100()
    {
        var s = ChangeSet.Start(State());
        var undo = new UndoStack();
        undo.Reset(s);
        for (var i = 0; i < 150; i++) { s = s.ToggleLock(["L1"]).Next; undo.Push(s); }
        var n = 0;
        while (undo.CanUndo) { undo.Undo(); n++; }
        Assert.Equal(100, n);
    }
}
