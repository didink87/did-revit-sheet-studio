using SheetStudio.Core.Changes;
using SheetStudio.Core.Create;
using SheetStudio.Core.Model;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;

namespace SheetStudio.Core.ViewModels;

public sealed record ViewInfo(string UniqueId, string Name, string Jenis, bool OnSheet);

public sealed record StudioSnapshot(
    IReadOnlyList<SheetSnapshot> Sheets,
    ProjectState State,
    Prefs Prefs,
    IReadOnlyList<(string Name, double Elevation)> Levels,
    IReadOnlyList<string> TitleBlockTypes,
    IReadOnlyList<ViewInfo> Views);

public sealed record ApplyPlan(
    IReadOnlyList<RenumberStep> Renumber,
    IReadOnlyList<SheetChange> Edits,
    IReadOnlyList<string> Deletes,
    IReadOnlyDictionary<string, string> ExpectedVersi,
    ProjectState NewState);

public sealed record ApplyReport(int Applied, IReadOnlyList<(string UniqueId, string Pesan)> Skipped);

public enum DuplicateMode { LembarSaja, DenganView, DenganViewDetailing }

public sealed record CreateReport(IReadOnlyList<(string Title, bool Ok, string? Pesan, string? UniqueId)> Rows, bool Dibatalkan);

/// <summary>Everything the view models need from Revit. Implemented in SheetStudio.Revit via ExternalEvent.</summary>
public interface IStudioGateway
{
    Task<StudioSnapshot> LoadAsync();
    Task<ApplyReport> ApplyAsync(ApplyPlan plan);
    Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done, int Total, string Title)> progress, CancellationToken ct);
    Task<CreateReport> DuplicateAsync(IReadOnlyList<string> ids, DuplicateMode mode);
    Task OpenSheetAsync(string uniqueId);
    Task<byte[]?> ThumbnailAsync(string uniqueId);
    Task SaveAsync(Prefs? prefs, ProjectState? state, bool userLevel);
    event Action? ModelChanged;
}
