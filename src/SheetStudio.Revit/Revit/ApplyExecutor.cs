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

    /// <summary>Commits a transaction. Replaceable in tests to simulate a commit that Revit rolls back.</summary>
    internal static Func<Transaction, TransactionStatus> CommitFn = t => t.Commit();

    public static ApplyReport Apply(Document doc, ApplyPlan plan)
    {
        var skipped = new List<(string, string)>();
        if (plan.DocumentKey != SnapshotReader.DocumentKey(doc))
            return new ApplyReport(0, [("", "Dokumen aktif berbeda dari yang dibaca panel; tidak ada yang diterapkan. Tekan ⟳ lalu ulangi.")]);

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

        ApplyReport Abort(string why)
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            return new ApplyReport(0, skipped.Concat(plan.ExpectedVersi.Keys.Where(sheets.ContainsKey).Select(id => (id, why))).ToList());
        }

        // 1. deletions first (they free their numbers), then temporary numbers
        var deletes = plan.Deletes.Where(sheets.ContainsKey).ToList();
        var steps = plan.Renumber.Where(r => sheets.ContainsKey(r.UniqueId)).ToList();
        using (var t = new Transaction(doc, "Sheet Studio: Hapus & nomor sementara"))
        {
            t.Start();
            foreach (var uid in deletes)
            {
                doc.Delete(sheets[uid].Id);
                applied.Add(uid);
            }
            foreach (var r in steps) sheets[r.UniqueId].SheetNumber = r.Temp;
            if (CommitFn(t) != TransactionStatus.Committed) return Abort("Revit membatalkan transaksi (hapus/nomor sementara); tidak ada yang diterapkan");
        }

        // 2. final numbers and edits
        using (var t = new Transaction(doc, "Sheet Studio: Terapkan perubahan lembar"))
        {
            t.Start();
            foreach (var r in steps)
            {
                try { sheets[r.UniqueId].SheetNumber = r.Final; applied.Add(r.UniqueId); }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    t.RollBack();
                    return Abort($"Nomor {r.Final} sudah dipakai lembar lain di luar rencana; tidak ada yang diterapkan");
                }
            }

            foreach (var e in plan.Edits.Where(e => sheets.ContainsKey(e.UniqueId) && !deletes.Contains(e.UniqueId)))
            {
                string? problem;
                try { problem = ApplyEdit(doc, sheets[e.UniqueId], e); }
                catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
                {
                    problem = $"Lembar {sheets[e.UniqueId].SheetNumber}: {ex.Message}";
                }
                if (problem is null) applied.Add(e.UniqueId); else skipped.Add((e.UniqueId, problem));
            }
            // a commit Revit rolls back (e.g. a failure posted at commit) must not leave temporary numbers behind
            if (CommitFn(t) != TransactionStatus.Committed) return Abort("Revit membatalkan transaksi saat menerapkan; tidak ada yang diterapkan");
        }

        // 3. panel state
        using (var t = new Transaction(doc, "Sheet Studio: Simpan urutan lembar"))
        {
            t.Start();
            StorageAdapter.Write(doc, null, ProjectStateJson.Serialize(plan.NewState));
            if (CommitFn(t) != TransactionStatus.Committed) return Abort("Revit membatalkan penyimpanan urutan; tidak ada yang diterapkan");
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
