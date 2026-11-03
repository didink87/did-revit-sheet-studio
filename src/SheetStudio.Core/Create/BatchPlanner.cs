using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Profiles;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Core.Create;

/// <summary>Turns batch rows into create requests: numbers, titles, conflicts. Nothing touches Revit here.</summary>
public static class BatchPlanner
{
    public static IReadOnlyList<PlannedSheet> Plan(IReadOnlyList<BatchRow> rows, IReadOnlyList<SheetSnapshot> existing, ChangeSet cs,
        NamingProfile p, NumberingOptions o)
    {
        // Numbers as they will be after staged edits, plus what this batch takes row by row.
        var used = new Dictionary<string, HashSet<int>>();
        foreach (var r in Sequencer.Compute(existing, cs, p, o).Results)
            if (SheetId.Parse(r.NewNumber, p) is { Ok: true } id) Used(used, id.Id).Add(id.Id.Seq);

        var existingKeys = existing.Select(s => Key(s.KindId, s.Subjek, s.Title)).ToHashSet();
        var batchKeys = new HashSet<string>();
        var result = new List<PlannedSheet>();

        foreach (var row in rows)
        {
            var kind = p.FindKind(row.KindId);
            var title = !string.IsNullOrWhiteSpace(row.Judul) ? row.Judul! : kind?.TemplatJudul.Replace("{subjek}", row.Subjek).Trim() ?? "";
            var check = TitleRules.Check(title, p);
            var key = Key(kind?.Id, row.Subjek, check.Normalized);

            if (existingKeys.Contains(key)) { result.Add(new PlannedSheet(row, null, PlanStatus.SudahAda, "Lembar dengan jenis dan subjek ini sudah ada")); continue; }
            if (!batchKeys.Add(key)) { result.Add(new PlannedSheet(row, null, PlanStatus.Duplikat, "Baris ganda di batch ini")); continue; }
            if (check.Severity == Severity.Galat && kind is null && row.NomorDiminta is null)
            { result.Add(new PlannedSheet(row, null, PlanStatus.Galat, "Jenis atau nomor wajib diisi")); continue; }
            if (check.Severity == Severity.Galat) { result.Add(new PlannedSheet(row, null, PlanStatus.Galat, check.Pesan)); continue; }

            var notes = new List<string>();
            SheetId? number = null;
            if (row.NomorDiminta is { } wanted)
            {
                var parsed = SheetId.Parse(wanted, p);
                if (!parsed.Ok) notes.Add($"Nomor '{wanted}' tidak valid ({parsed.Error})");
                else if (Used(used, parsed.Id).Contains(parsed.Id.Seq)) notes.Add($"Nomor {parsed.Id.Format()} sudah dipakai");
                else number = parsed.Id;
            }
            if (number is null)
            {
                if (kind is null) { result.Add(new PlannedSheet(row, null, PlanStatus.Galat, string.Join("; ", notes.Append("Tanpa jenis, nomor harus diisi dengan benar")))); continue; }
                var (from, to) = Sequencer.RangeFor(kind.Disiplin, kind.Tipe, kind.Id, p, o);
                var set = Used(used, new SheetId(kind.Disiplin, kind.Tipe, 0));
                var seq = Enumerable.Range(from, to - from + 1).FirstOrDefault(s => !set.Contains(s), -1);
                if (seq < 0)
                {
                    var label = Sequencer.GroupLabel(LayerListBuilder.TypeKey(kind.Disiplin, kind.Tipe));
                    result.Add(new PlannedSheet(row, null, PlanStatus.Galat, $"Rentang {label} {from:00}–{to:00} penuh"));
                    continue;
                }
                number = new SheetId(kind.Disiplin, kind.Tipe, seq);
                if (notes.Count > 0) notes[^1] += $", diganti {number.Value.Format()}";
            }
            Used(used, number.Value).Add(number.Value.Seq);
            if (check.Severity == Severity.Peringatan) notes.Add(check.Pesan!);

            var views = new List<ViewSpec>();
            if (row.BuatView && kind is not null && kind.JenisView is not ("Tanpa" or "Schedule"))
                views.Add(new ViewSpec(ViewMode.BuatBaru, null, kind.JenisView, row.LevelName, row.Skala > 0 ? row.Skala : kind.SkalaDefault, kind.ViewTemplate));

            var request = new CreateRequest(new CreateName(number.Value, check.Normalized, kind?.Id, TitleRules.Normalize(row.Subjek)), row.Kertas, null, views);
            result.Add(new PlannedSheet(row, request, PlanStatus.Baru, notes.Count == 0 ? null : string.Join("; ", notes)));
        }
        return result;
    }

    private static string Key(string? kindId, string subjek, string title) =>
        kindId is not null ? $"{kindId}|{TitleRules.Normalize(subjek)}" : $"|{TitleRules.Normalize(title)}";

    private static HashSet<int> Used(Dictionary<string, HashSet<int>> used, SheetId id)
    {
        var k = LayerListBuilder.TypeKey(id.Discipline, id.Type);
        if (!used.TryGetValue(k, out var set)) used[k] = set = [];
        return set;
    }
}
