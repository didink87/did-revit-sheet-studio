using SheetStudio.Core.Naming;

namespace SheetStudio.Core.Tests;

public class SheetIdTests
{
    [Theory]
    [InlineData("A", 1, 2, "A-102")]
    [InlineData("EP", 1, 10, "EP110")]
    [InlineData("G", 0, 0, "G-000")]
    [InlineData("RA", 1, 2, "RA102")]
    public void Format_FollowsUds(string d, int t, int s, string expected) =>
        Assert.Equal(expected, new SheetId(d, t, s).Format());

    [Theory]
    [InlineData("A-102", "A", 1, 2)]
    [InlineData(" EL103 ", "EL", 1, 3)]
    [InlineData("QH601", "QH", 6, 1)]
    [InlineData("G-000", "G", 0, 0)]
    public void Parse_Valid(string txt, string d, int t, int s)
    {
        var r = SheetId.Parse(txt, Profiles.Pupr);
        Assert.True(r.Ok, r.Error);
        Assert.Equal(new SheetId(d, t, s), r.Id);
    }

    [Fact]
    public void Parse_Lowercase_NormalizedSuggested()
    {
        var r = SheetId.Parse("a-101", Profiles.Pupr);
        Assert.True(r.Ok);
        Assert.Equal("A-101", r.Normalized);
    }

    [Theory]
    [InlineData("A101", "One-letter discipline requires '-'")]
    [InlineData("A 101", "Wrong format")]
    [InlineData("A-1011", "Wrong format")]
    [InlineData("K-101", "Discipline 'K' not recognized")]
    [InlineData("A-100", "Sequence 00 is only for G-000")]
    [InlineData("", "Number is empty")]
    [InlineData(null, "Number is empty")]
    public void Parse_Invalid_GivesIndonesianError(string? txt, string msgStart)
    {
        var r = SheetId.Parse(txt, Profiles.Pupr);
        Assert.False(r.Ok);
        Assert.StartsWith(msgStart, r.Error);
    }
}
