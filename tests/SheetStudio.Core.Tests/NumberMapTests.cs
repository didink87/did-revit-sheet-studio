using SheetStudio.Core.Changes;
using SheetStudio.Core.Numbering;
using static SheetStudio.Core.Tests.Fx;

namespace SheetStudio.Core.Tests;

public class NumberMapTests
{
    [Fact]
    public void NumberMap_StatesCorrect()
    {
        var sheets = new[] { S("a", "A-110", kind: "denah"), S("b", "A-111", kind: "denah") };
        var cs = ChangeSet.Start(State(locked: ["b"]));
        var cells = NumberMap.For("A", 1, "denah", sheets, cs, Profiles.Pupr, new NumberingOptions(Strategy.Rentang, 1, 1), selectedSeq: 12);
        Assert.Equal(99, cells.Count);
        Assert.Equal(CellState.Terpakai, cells[10 - 1]);
        Assert.Equal(CellState.Terkunci, cells[11 - 1]);
        Assert.Equal(CellState.Dipilih, cells[12 - 1]);
        Assert.Equal(CellState.Bebas, cells[13 - 1]);
        Assert.Equal(CellState.DiluarRentang, cells[5 - 1]);
        Assert.Equal(CellState.DiluarRentang, cells[40 - 1]);
    }

    [Fact]
    public void NumberMap_NoKind_WholeRangeFree()
    {
        var cells = NumberMap.For("A", 2, null, [S("a", "A-110")], ChangeSet.Start(State()), Profiles.Pupr, new NumberingOptions(Strategy.Rentang, 1, 1), null);
        Assert.All(cells, c => Assert.Equal(CellState.Bebas, c));
    }
}
