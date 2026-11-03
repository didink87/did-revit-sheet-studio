using Autodesk.Revit.DB;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Model;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

/// <summary>
/// Writes a staged plan to the model as one TransactionGroup (= one Ctrl+Z): temp numbers, final numbers and edits,
/// deletes, then the panel state. Sheets changed since the snapshot are skipped. A final number that collides with a
/// sheet outside the plan rolls the whole group back, so no sheet is ever left with a temporary number.
/// </summary>
public static class ApplyExecutor
{
    public const string GroupName = "Sheet Studio: Terapkan";

    public static ApplyReport Apply(Document doc, ApplyPlan plan)
    {
        var skipped = new List<(string, string)>();
        var sheets = new Dictionary<string, ViewSheet>();
        foreach (var (uid, versi) in plan.ExpectedVersi)
        {
            if (doc.GetElement(uid) is not ViewSheet s) { skipped.Add((uid, "Lembar tidak ada lagi di model")); continue; }
            if (SheetSnapshot.MakeVersi(s.SheetNumber, s.Name) != versi) { skipped.Add((uid, $"Lembar {s.SheetNumber} berubah sejak dibaca")); continue; }
            var (editable, owner) = SnapshotReader.Ownership(doc, s.Id);
            if (!editable) { skipped.Add((uid, $"Lembar {s.SheetNumber} sedang dipinjam {owner}")); continue; }
            sheets[uid] = s;
        }

        var applied = new HashSet<string>();
        using var group = new TransactionGroup(doc, GroupName);
        group.Start();

        var steps = plan.Renumber.Where(r => sheets.ContainsKey(r.UniqueId)).ToList();
        using (var t = new Transaction(doc, "Sheet Studio: Nomor sementara"))
        {
            t.Start();
            foreach (var r in steps) sheets[r.UniqueId].SheetNumber = r.Temp;
            t.Commit();
        }

        using (var t = new Transaction(doc, "Sheet Studio: Terapkan perubahan lembar"))
        {
            t.Start();
            foreach (var r in steps)
            {
                try { sheets[r.UniqueId].SheetNumber = r.Final; applied.Add(r.UniqueId); }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    t.RollBack();
                    group.RollBack();
                    var msg = $"Nomor {r.Final} sudah dipakai lembar lain di luar rencana; tidak ada yang diterapkan";
                    return new ApplyReport(0, skipped.Concat(plan.ExpectedVersi.Keys.Where(sheets.ContainsKey).Select(id => (id, msg))).ToList());
                }
            }

            foreach (var e in plan.Edits.Where(e => sheets.ContainsKey(e.UniqueId)))
            {
                var problem = ApplyEdit(doc, sheets[e.UniqueId], e);
                if (problem is null) applied.Add(e.UniqueId); else skipped.Add((e.UniqueId, problem));
            }

            foreach (var uid in plan.Deletes.Where(sheets.ContainsKey))
            {
                doc.Delete(sheets[uid].Id);
                applied.Add(uid);
            }
            t.Commit();
        }

        using (var t = new Transaction(doc, "Sheet Studio: Simpan urutan lembar"))
        {
            t.Start();
            StorageAdapter.Write(doc, null, ProjectStateJson.Serialize(plan.NewState));
            t.Commit();
        }

        group.Assimilate();
        return new ApplyReport(applied.Count, skipped);
    }

    /// <summary>Returns a message when part of the edit could not be applied; null when all went in.</summary>
    private static string? ApplyEdit(Document doc, ViewSheet s, SheetChange e)
    {
        var problems = new List<string>();
        if (e.NewTitle is not null) s.Name = e.NewTitle;
        if (e.InSheetList is bool show) s.get_Parameter(BuiltInParameter.SHEET_SCHEDULED)?.Set(show ? 1 : 0);
        if (e.NewTitleBlockType is not null)
        {
            var symbol = TitleBlocks.Find(doc, e.NewTitleBlockType);
            if (symbol is null) problems.Add($"Title block '{e.NewTitleBlockType}' tidak ditemukan");
            else
            {
                if (!symbol.IsActive) symbol.Activate();
                var instance = TitleBlocks.On(s);
                if (instance is null) doc.Create.NewFamilyInstance(XYZ.Zero, symbol, s);
                else if (instance.Symbol.Id != symbol.Id) instance.ChangeTypeId(symbol.Id);
            }
        }
        foreach (var (name, value) in e.NewParams ?? new Dictionary<string, string>())
        {
            var p = s.LookupParameter(name);
            if (p is null || p.IsReadOnly) { problems.Add($"Parameter '{name}' tidak bisa diisi"); continue; }
            var ok = p.StorageType switch
            {
                StorageType.String => p.Set(value),
                StorageType.Integer => int.TryParse(value, out var i) && p.Set(i),
                StorageType.Double => double.TryParse(value, out var d) && p.Set(d),
                _ => false,
            };
            if (!ok) problems.Add($"Parameter '{name}' menolak nilai '{value}'");
        }
        return problems.Count == 0 ? null : $"Lembar {s.SheetNumber}: " + string.Join("; ", problems);
    }
}
