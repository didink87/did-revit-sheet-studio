using SheetStudio.Core.Naming;

namespace SheetStudio.Core.Tests;

public class TitleRulesTests
{
    [Fact]
    public void KnownAbbrev_WarnsWithSuggestion()
    {
        var c = TitleRules.Check("Denah Lt. 2", Profiles.Pupr);
        Assert.Equal("DENAH LT. 2", c.Normalized);
        Assert.Equal(Severity.Peringatan, c.Severity);
        var h = Assert.Single(c.Hints);
        Assert.Equal(("LT.", "LANTAI"), (h.Found, h.Suggestion));
    }

    [Fact]
    public void ApplySuggestions_ExpandsKnown() =>
        Assert.Equal("DENAH LANTAI 2", TitleRules.ApplySuggestions("DENAH LT. 2", Profiles.Pupr));

    [Theory]
    [InlineData("POTONGAN A-A")]
    [InlineData("DETAIL PINTU P1 DAN J1")]
    [InlineData("DETAIL BV1")]
    [InlineData("DENAH LANTAI 12")]
    public void Codes_AreOk(string title)
    {
        var c = TitleRules.Check(title, Profiles.Pupr);
        Assert.Equal(Severity.Ok, c.Severity);
        Assert.Empty(c.Hints);
    }

    [Fact]
    public void Lowercase_IsNormalizedAndOk()
    {
        var c = TitleRules.Check("  tampak   depan ", Profiles.Pupr);
        Assert.Equal("TAMPAK DEPAN", c.Normalized);
        Assert.Equal(Severity.Ok, c.Severity);
    }

    [Theory]
    [InlineData("DENAH LT.SBR 2", "LT.SBR")]
    [InlineData("DENAH TPKS", "TPKS")]
    public void UnknownAbbrev_WarnsWithoutSuggestion(string title, string found)
    {
        var c = TitleRules.Check(title, Profiles.Pupr);
        Assert.Equal(Severity.Peringatan, c.Severity);
        var h = Assert.Single(c.Hints);
        Assert.Equal(found, h.Found);
        Assert.Null(h.Suggestion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_IsGalat(string? title)
    {
        var c = TitleRules.Check(title, Profiles.Pupr);
        Assert.Equal(Severity.Galat, c.Severity);
        Assert.Equal("Title is empty", c.Pesan);
    }

    [Theory]
    [InlineData("DENAH [LANTAI 1]")]
    [InlineData("DENAH; TAMPAK")]
    [InlineData("DETAIL A|B")]
    public void RevitProhibitedChars_IsGalat(string title)
    {
        var c = TitleRules.Check(title, Profiles.Pupr);
        Assert.Equal(Severity.Galat, c.Severity);
        Assert.Contains("characters", c.Pesan);
    }
}
