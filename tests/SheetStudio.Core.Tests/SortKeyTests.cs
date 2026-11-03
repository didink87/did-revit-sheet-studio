using SheetStudio.Core.Naming;

namespace SheetStudio.Core.Tests;

public class SortKeyTests
{
    [Fact]
    public void For_A111_Pos3() =>
        Assert.Equal("08.A.1.0003", SortKey.For(new SheetId("A", 1, 11), Profiles.Pupr, 3));

    [Fact]
    public void For_TwoLetter_UsesPrimaryOrder() =>
        Assert.Equal("15.EP.1.0000", SortKey.For(new SheetId("EP", 1, 10), Profiles.Pupr, 0));

    [Fact]
    public void Keys_SortInNcsOrder()
    {
        var keys = new[] { new SheetId("E", 1, 1), new SheetId("A", 0, 1), new SheetId("G", 0, 1), new SheetId("S", 1, 1) }
            .Select(id => (id, key: SortKey.For(id, Profiles.Pupr, 0))).OrderBy(x => x.key, StringComparer.Ordinal).Select(x => x.id.Format());
        Assert.Equal(new[] { "G-001", "S-101", "A-001", "E-101" }, keys);
    }
}
