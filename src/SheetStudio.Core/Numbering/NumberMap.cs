using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Numbering;

public enum CellState { Bebas, Terpakai, Terkunci, Dipilih, DiluarRentang }

/// <summary>The 10×10 picker map: state of sequences 01–99 of one discipline/type after staged numbering.</summary>
public static class NumberMap
{
    public static IReadOnlyList<CellState> For(string disc, int type, string? kindId, IReadOnlyList<SheetSnapshot> sheets,
        ChangeSet cs, NamingProfile p, NumberingOptions o, int? selectedSeq)
    {
        var cells = new CellState[99];
        var range = Sequencer.RangeFor(disc, type, kindId, p, o);
        for (var seq = 1; seq <= 99; seq++)
            cells[seq - 1] = seq < range.From || seq > range.To ? CellState.DiluarRentang : CellState.Bebas;

        var byId = sheets.ToDictionary(s => s.UniqueId);
        var group = LayerListBuilder.TypeKey(disc, type);
        foreach (var r in Sequencer.Compute(sheets, cs, p, o).Results)
        {
            var id = SheetId.Parse(r.NewNumber, p);
            if (!id.Ok || LayerListBuilder.TypeKey(id.Id.Discipline, id.Id.Type) != group || id.Id.Seq is < 1 or > 99) continue;
            var s = byId[r.UniqueId];
            cells[id.Id.Seq - 1] = !s.Editable || cs.State.Locked.Contains(s.UniqueId) ? CellState.Terkunci : CellState.Terpakai;
        }
        if (selectedSeq is >= 1 and <= 99) cells[selectedSeq.Value - 1] = CellState.Dipilih;
        return cells;
    }
}
