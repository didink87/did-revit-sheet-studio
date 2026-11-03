using Autodesk.Revit.DB;
using SheetStudio.Core.Create;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.Profiles;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

/// <summary>Creates and duplicates sheets. One TransactionGroup per call, one sub-transaction per sheet.</summary>
public static class CreateExecutor
{
    public const string GroupName = "Sheet Studio: Buat Lembar";

    public static CreateReport Create(Document doc, IReadOnlyList<CreateRequest> reqs, Prefs prefs, bool lewatiGagal,
        IProgress<(int, int, string)>? progress, CancellationToken ct)
    {
        var rows = new List<(string Title, bool Ok, string? Pesan, string? UniqueId)>();
        using var group = new TransactionGroup(doc, GroupName);
        group.Start();
        using (var t = new Transaction(doc, "Sheet Studio: Parameter"))
        {
            t.Start();
            ProjectParams.Ensure(doc);
            t.Commit();
        }

        for (var i = 0; i < reqs.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                group.RollBack();
                return new CreateReport(rows.Select(r => (r.Title, false, (string?)"Dibatalkan", (string?)null)).ToList(), true);
            }
            var req = reqs[i];
            using (var t = new Transaction(doc, $"Buat lembar {req.Name.Id.Format()}"))
            {
                t.Start();
                try
                {
                    var (sheet, warning) = CreateOne(doc, req, prefs);
                    t.Commit();
                    rows.Add((req.Name.Title, true, warning, sheet.UniqueId));
                }
                catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException or ArgumentException)
                {
                    if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                    rows.Add((req.Name.Title, false, $"{req.Name.Id.Format()}: {ex.Message}", null));
                    if (!lewatiGagal)
                    {
                        group.RollBack();
                        return new CreateReport(rows.Select(r => (r.Title, false, r.Ok ? "Dibatalkan karena baris lain gagal" : r.Pesan, (string?)null)).ToList(), false);
                    }
                }
            }
            progress?.Report((i + 1, reqs.Count, req.Name.Title));
        }
        group.Assimilate();
        return new CreateReport(rows, false);
    }

    private static (ViewSheet Sheet, string? Warning) CreateOne(Document doc, CreateRequest req, Prefs prefs)
    {
        var warnings = new List<string>();
        var tbName = req.TitleBlockType ?? prefs.TitleBlockPerKertas.GetValueOrDefault(req.Kertas);
        var tb = TitleBlocks.Find(doc, tbName) ?? throw new InvalidOperationException($"Title block '{tbName}' tidak ditemukan");
        if (!tb.IsActive) tb.Activate();

        var sheet = ViewSheet.Create(doc, tb.Id);
        sheet.SheetNumber = req.Name.Id.Format();
        sheet.Name = req.Name.Title;
        sheet.LookupParameter(SnapshotReader.KindParam)?.Set(req.Name.KindId ?? "");
        sheet.LookupParameter(SnapshotReader.SubjectParam)?.Set(req.Name.Subjek);

        var views = new List<View>();
        foreach (var spec in req.Views)
        {
            string? w = null;
            View? view = spec.Mode switch
            {
                ViewMode.PakaiYangAda => ExistingView(doc, sheet, spec.ExistingViewUniqueId, out w),
                ViewMode.BuatBaru => ViewCreator.Create(doc, spec, req.Name.Title, out w),
                _ => null,
            };
            if (w is not null) warnings.Add(w);
            if (view is not null) views.Add(view);
        }
        ViewportPlacer.Place(doc, sheet, views, out var fits);
        if (!fits) warnings.Add("View tidak muat di area gambar; atur skala atau ukuran kertas");
        return (sheet, warnings.Count == 0 ? null : string.Join("; ", warnings));
    }

    private static View? ExistingView(Document doc, ViewSheet sheet, string? uid, out string? warning)
    {
        warning = null;
        if (uid is null || doc.GetElement(uid) is not View view) { warning = "View yang dipilih tidak ditemukan"; return null; }
        if (Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id)) return view;
        var owner = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>()
            .Where(v => v.ViewId == view.Id).Select(v => (doc.GetElement(v.SheetId) as ViewSheet)?.SheetNumber).FirstOrDefault();
        warning = $"View '{view.Name}' sudah dipakai di lembar {owner ?? "lain"}";
        return null;
    }

    /// <summary>Duplicates sheets into the next free number of the same group (kind range when known).</summary>
    public static CreateReport Duplicate(Document doc, IReadOnlyList<string> ids, DuplicateMode mode, NamingProfile p, NumberingOptions o)
    {
        var rows = new List<(string, bool, string?, string?)>();
        var option = mode switch
        {
            DuplicateMode.DenganView => SheetDuplicateOption.DuplicateSheetWithViewsOnly,
            DuplicateMode.DenganViewDetailing => SheetDuplicateOption.DuplicateSheetWithViewsAndDetailing,
            _ => SheetDuplicateOption.DuplicateEmptySheet,
        };
        using var group = new TransactionGroup(doc, "Sheet Studio: Duplikat");
        group.Start();
        foreach (var uid in ids)
        {
            if (doc.GetElement(uid) is not ViewSheet sheet) { rows.Add((uid, false, "Lembar tidak ada lagi", null)); continue; }
            using var t = new Transaction(doc, $"Duplikat {sheet.SheetNumber}");
            t.Start();
            try
            {
                var copy = (ViewSheet)doc.GetElement(sheet.Duplicate(option));
                copy.SheetNumber = NextFree(doc, sheet, p, o);
                copy.Name = sheet.Name;
                foreach (var name in new[] { SnapshotReader.KindParam, SnapshotReader.SubjectParam })
                    if (sheet.LookupParameter(name)?.AsString() is { } v) copy.LookupParameter(name)?.Set(v);
                t.Commit();
                rows.Add((sheet.Name, true, null, copy.UniqueId));
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
            {
                if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                rows.Add((sheet.Name, false, $"{sheet.SheetNumber}: {ex.Message}", null));
            }
        }
        group.Assimilate();
        return new CreateReport(rows, false);
    }

    /// <summary>Removes sheets created earlier in a cancelled or failed batch.</summary>
    public static void DeleteCreated(Document doc, IReadOnlyList<string> uniqueIds)
    {
        using var t = new Transaction(doc, "Sheet Studio: Batalkan pembuatan");
        t.Start();
        foreach (var uid in uniqueIds)
            if (doc.GetElement(uid) is ViewSheet s) doc.Delete(s.Id);
        t.Commit();
    }

    private static string NextFree(Document doc, ViewSheet original, NamingProfile p, NumberingOptions o)
    {
        var parsed = SheetId.Parse(original.SheetNumber, p);
        if (!parsed.Ok) throw new InvalidOperationException("Nomor asli bukan format UDS; beri nomor manual");
        var group = LayerListBuilder.TypeKey(parsed.Id.Discipline, parsed.Id.Type);
        var used = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
            .Select(s => SheetId.Parse(s.SheetNumber, p)).Where(r => r.Ok && LayerListBuilder.TypeKey(r.Id.Discipline, r.Id.Type) == group)
            .Select(r => r.Id.Seq).ToHashSet();
        var (from, to) = Sequencer.RangeFor(parsed.Id.Discipline, parsed.Id.Type, original.LookupParameter(SnapshotReader.KindParam)?.AsString(), p, o);
        for (var s = Math.Max(from, parsed.Id.Seq); s <= to; s++)
            if (!used.Contains(s)) return new SheetId(parsed.Id.Discipline, parsed.Id.Type, s).Format();
        for (var s = from; s <= to; s++)
            if (!used.Contains(s)) return new SheetId(parsed.Id.Discipline, parsed.Id.Type, s).Format();
        throw new InvalidOperationException($"Rentang {original.SheetNumber[..^2]}xx penuh");
    }
}
