using SheetStudio.Core.Create;
using Xunit;

namespace SheetStudio.Core.Tests;

public class ViewKindLabelsTests
{
    [Theory]
    [InlineData("Denah", "Plan")]
    [InlineData("Plafon", "Ceiling plan")]
    [InlineData("Drafting", "Drafting")]
    [InlineData("Legend", "Legend")]
    [InlineData("TigaD", "3D")]
    [InlineData("Tampak", "Elevation")]
    [InlineData("Potongan", "Section")]
    [InlineData("Schedule", "Schedule")]
    [InlineData("Tanpa", "None")]
    [InlineData("Whatever", "Whatever")]
    [InlineData(null, "")]
    public void KindLabel_MapsAndFallsBack(string? id, string expected) => Assert.Equal(expected, ViewKindLabels.Label(id));

    [Theory]
    [InlineData(PlanStatus.Baru, "New")]
    [InlineData(PlanStatus.SudahAda, "Exists")]
    [InlineData(PlanStatus.Duplikat, "Duplicate")]
    [InlineData(PlanStatus.Galat, "Error")]
    public void StatusLabel_Maps(PlanStatus s, string expected) => Assert.Equal(expected, ViewKindLabels.Label(s));

    [Fact]
    public void StatusLabel_Null_IsEmpty() => Assert.Equal("", ViewKindLabels.Label((PlanStatus?)null));
}
