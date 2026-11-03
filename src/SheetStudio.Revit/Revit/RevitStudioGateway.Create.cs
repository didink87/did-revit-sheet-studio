using SheetStudio.Core.Create;
using SheetStudio.Core.Diagnostics;
using SheetStudio.Core.Profiles;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Revit.Revit;

public sealed partial class RevitStudioGateway
{
    /// <summary>Rows per ExternalEvent. Revit's UI thread runs the event, so smaller chunks let the progress bar
    /// repaint and the Batal button respond between chunks.</summary>
    public const int ChunkSize = 5;

    public async Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done, int Total, string Title)> progress, CancellationToken ct)
    {
        using var _ = StudioLog.Time("create", $"{reqs.Count} permintaan");
        var prefs = await runner.Run(doc => Load(doc).Prefs);
        var rows = new List<(string Title, bool Ok, string? Pesan, string? UniqueId)>();
        var created = new List<string>();
        var views = new List<string>();
        for (var start = 0; start < reqs.Count; start += ChunkSize)
        {
            if (ct.IsCancellationRequested)
            {
                await runner.Run(doc => { CreateExecutor.DeleteCreated(doc, created, views); return true; });
                return new CreateReport(rows.Select(r => (r.Title, false, (string?)"Dibatalkan", (string?)null)).ToList(), true);
            }
            var chunk = reqs.Skip(start).Take(ChunkSize).ToList();
            var offset = start;
            var inner = new Progress<(int, int, string)>(x => progress.Report((offset + x.Item1, reqs.Count, x.Item3)));
            var report = await runner.Run(doc => CreateExecutor.Create(doc, chunk, prefs, prefs.BatchLewatiGagal, inner, CancellationToken.None));
            rows.AddRange(report.Rows);
            created.AddRange(report.Rows.Where(r => r.UniqueId is not null).Select(r => r.UniqueId!));
            views.AddRange(report.CreatedViews ?? []);
            if (!prefs.BatchLewatiGagal && report.Rows.Any(r => !r.Ok))
            {
                await runner.Run(doc => { CreateExecutor.DeleteCreated(doc, created, views); return true; });
                return new CreateReport(rows.Select(r => (r.Title, false, r.Ok ? "Dibatalkan karena baris lain gagal" : r.Pesan, (string?)null)).ToList(), false);
            }
        }
        return new CreateReport(rows, false, views);
    }

    public Task<CreateReport> DuplicateAsync(IReadOnlyList<string> ids, DuplicateMode mode) => runner.Run(doc =>
    {
        var prefs = Load(doc).Prefs;
        var profile = ProfileLoader.LoadBuiltIn(ProfileLoader.BuiltInIds.Contains(prefs.ProfileId) ? prefs.ProfileId : "pupr");
        return CreateExecutor.Duplicate(doc, ids, mode, profile, prefs.Numbering);
    });

    public async Task<byte[]?> ThumbnailAsync(string uniqueId)
    {
        using var _ = StudioLog.Time("thumb", uniqueId);
        return await runner.Run(doc => ThumbnailService.Render(doc, uniqueId, ThumbnailService.CacheDir(doc)));
    }
}
