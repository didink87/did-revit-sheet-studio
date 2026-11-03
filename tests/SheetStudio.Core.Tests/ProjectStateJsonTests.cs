using SheetStudio.Core.Model;

namespace SheetStudio.Core.Tests;

public class ProjectStateJsonTests
{
    [Fact]
    public void RoundTrip()
    {
        var s = new ProjectState(new Dictionary<string, IReadOnlyList<string>> { ["A|1"] = ["x", "y"] }, ["A", "G"], new HashSet<string> { "y" });
        var back = ProjectStateJson.Deserialize(ProjectStateJson.Serialize(s));
        Assert.Equal(new[] { "x", "y" }, back.OrderPerGroup["A|1"]);
        Assert.Equal(new[] { "A", "G" }, back.DisciplineOrder);
        Assert.Contains("y", back.Locked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{rusak")]
    public void Bad_ReturnsEmpty(string? json)
    {
        var s = ProjectStateJson.Deserialize(json);
        Assert.Empty(s.OrderPerGroup);
        Assert.Empty(s.Locked);
    }
}
