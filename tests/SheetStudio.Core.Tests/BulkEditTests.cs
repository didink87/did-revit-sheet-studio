using SheetStudio.Core.Changes;
using SheetStudio.Core.Edit;
using SheetStudio.Core.Model;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class BulkEditTests
{
    private static readonly IReadOnlyList<SheetSnapshot> Sel =
        [S("a", "A-110", "DENAH LT 1"), S("b", "A-111", "DENAH ALTAR LT 2")];

    [Fact]
    public void FindReplace_WholeWord()
    {
        var cs = BulkEdit.FindReplace(ChangeSet.Start(State()), Sel, "lt", "lantai", wholeWord: true);
        Assert.Equal("DENAH LANTAI 1", cs.Changes["a"].NewTitle);
        Assert.Equal("DENAH ALTAR LANTAI 2", cs.Changes["b"].NewTitle);
        var partial = BulkEdit.FindReplace(ChangeSet.Start(State()), Sel, "LT", "X", wholeWord: false);
        Assert.Equal("DENAH AXAR X 2", partial.Changes["b"].NewTitle);
    }

    [Fact]
    public void FindReplace_UsesStagedTitle_And_SkipsUnchanged()
    {
        var cs = ChangeSet.Start(State()).Upsert(new SheetChange("a", NewTitle: "RENCANA LT 1")).Next;
        cs = BulkEdit.FindReplace(cs, Sel, "RENCANA", "DENAH BARU", true);
        Assert.Equal("DENAH BARU LT 1", cs.Changes["a"].NewTitle);
        var back = BulkEdit.FindReplace(cs, Sel, "DENAH BARU", "DENAH", true);
        Assert.False(back.Changes.ContainsKey("a")); // back to the original title = no change
        Assert.False(cs.Changes.ContainsKey("b"));
    }

    [Fact]
    public void PrefixSuffix()
    {
        var cs = BulkEdit.PrefixSuffix(ChangeSet.Start(State()), Sel, "rencana", " (revisi)");
        Assert.Equal("RENCANA DENAH LT 1 (REVISI)", cs.Changes["a"].NewTitle);
    }

    [Fact]
    public void ApplySuggestions_ExpandsAbbrev() =>
        Assert.Equal("DENAH LANTAI 1", BulkEdit.ApplySuggestions(ChangeSet.Start(State()), Sel, Profiles.Pupr).Changes["a"].NewTitle);

    [Fact]
    public void SetDisciplineType_Multi()
    {
        var cs = BulkEdit.SetDisciplineType(ChangeSet.Start(State(locked: ["b"])), Sel, "A", 4, Profiles.Pupr);
        Assert.Equal(("A", (int?)4), (cs.Changes["a"].NewDiscipline, cs.Changes["a"].NewType));
        Assert.False(cs.Changes.ContainsKey("b")); // locked
        var back = BulkEdit.SetDisciplineType(cs, Sel, null, 1, Profiles.Pupr);
        Assert.False(back.Changes.ContainsKey("a"));
    }

    [Fact]
    public void SetParam_And_TitleBlock()
    {
        var cs = BulkEdit.SetParam(ChangeSet.Start(State()), Sel, "Digambar", "BUDI");
        cs = BulkEdit.SetTitleBlock(cs, Sel, "PUPR_KOP_A1");
        Assert.Equal("BUDI", cs.Changes["b"].NewParams!["Digambar"]);
        Assert.Equal("PUPR_KOP_A1", cs.Changes["a"].NewTitleBlockType);
    }
}
