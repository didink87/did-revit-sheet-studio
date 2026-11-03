using SheetStudio.Core.Create;

namespace SheetStudio.Core.Tests;

public class LayoutGridTests
{
    [Fact]
    public void Layout_Single_Centered()
    {
        var c = LayoutGrid.Arrange([(20, 10)], new Rect(0, 0, 100, 50), 5, out var fits);
        Assert.True(fits);
        Assert.Equal((50.0, 25.0), Assert.Single(c));
    }

    [Fact]
    public void Layout_Three_Wrap()
    {
        var c = LayoutGrid.Arrange([(20, 10), (20, 10), (20, 10)], new Rect(0, 0, 50, 50), 5, out var fits);
        Assert.True(fits);
        Assert.Equal(new[] { (12.5, 32.5), (37.5, 32.5), (25.0, 17.5) }, c);
    }

    [Fact]
    public void Layout_TooBig_FitsAllFalse()
    {
        LayoutGrid.Arrange([(120, 10)], new Rect(0, 0, 100, 50), 5, out var wide);
        Assert.False(wide);
        LayoutGrid.Arrange([(40, 30), (40, 30)], new Rect(0, 0, 50, 50), 5, out var tall);
        Assert.False(tall);
    }
}
