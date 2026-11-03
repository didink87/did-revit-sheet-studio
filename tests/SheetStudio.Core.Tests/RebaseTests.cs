using SheetStudio.Core.Changes;
using SheetStudio.Core.Model;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class RebaseTests
{
    private static readonly IReadOnlyList<SheetSnapshot> Before = [S("L1", "A-110"), S("L2", "A-111"), S("L3", "A-112")];

    [Fact]
    public void Rebase_DropsChangesForDeletedSheet()
    {
        var cs = ChangeSet.Start(State())
            .Upsert(new SheetChange("L2", NewTitle: "X")).Next
            .MoveSelection(["L2"], "A|1", 0, Before, Profiles.Pupr).Next
            .ToggleLock(["L2"]).Next;
        var (next, dropped) = Rebase.Onto(cs, [Before[0], Before[2]], Profiles.Pupr);
        Assert.Equal(new[] { "L2" }, dropped);
        Assert.False(next.Changes.ContainsKey("L2"));
        Assert.DoesNotContain("L2", next.State.Locked);
        Assert.DoesNotContain("L2", next.State.OrderPerGroup["A|1"]);
    }

    [Fact]
    public void Rebase_AppendsNewSheetToGroupEnd()
    {
        var cs = ChangeSet.Start(State()).MoveSelection(["L3"], "A|1", 0, Before, Profiles.Pupr).Next; // L3, L1, L2
        var fresh = Before.Append(S("N1", "A-105")).ToList();
        var (next, dropped) = Rebase.Onto(cs, fresh, Profiles.Pupr);
        Assert.Empty(dropped);
        Assert.Equal(new[] { "L3", "L1", "L2", "N1" }, next.GroupOrder("A|1", fresh, Profiles.Pupr));
    }

    [Fact]
    public void Rebase_KeepsValidChanges()
    {
        var cs = ChangeSet.Start(State()).Upsert(new SheetChange("L1", NewTitle: "DENAH")).Next;
        var (next, dropped) = Rebase.Onto(cs, Before, Profiles.Pupr);
        Assert.Empty(dropped);
        Assert.Equal("DENAH", next.Changes["L1"].NewTitle);
    }
}
