using SheetStudio.Core.Create;
using SheetStudio.Core.Model;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.ViewModels;

namespace SheetStudio.Core.Tests;

internal sealed class FakeGateway : IStudioGateway
{
    public List<SheetSnapshot> Sheets { get; set; } = [];
    public ProjectState State { get; set; } = ProjectState.Empty;
    public Prefs Prefs { get; set; } = Prefs.Default with { KonfirmasiTerapkan = false };
    public List<ApplyPlan> Applied { get; } = [];
    public List<IReadOnlyList<CreateRequest>> Created { get; } = [];
    public int Loads { get; private set; }
    public TaskCompletionSource? CreateGate { get; set; }

    public event Action? ModelChanged;
    public void RaiseModelChanged() => ModelChanged?.Invoke();

    public Task<StudioSnapshot> LoadAsync()
    {
        Loads++;
        return Task.FromResult(new StudioSnapshot(Sheets.ToList(), State, Prefs, [("Lantai 1", 0), ("Lantai 2", 4), ("Lantai 3", 8)],
            ["PUPR_KOP_A3", "PUPR_KOP_A2"], [new ViewInfo("v1", "DENAH LANTAI 1", "Denah", false), new ViewInfo("v2", "TAMPAK", "Tampak", true)]));
    }

    public Task<ApplyReport> ApplyAsync(ApplyPlan plan)
    {
        Applied.Add(plan);
        State = plan.NewState;
        return Task.FromResult(new ApplyReport(plan.Renumber.Count + plan.Edits.Count + plan.Deletes.Count, []));
    }

    public async Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done, int Total, string Title)> progress, CancellationToken ct)
    {
        Created.Add(reqs);
        if (CreateGate is not null)
        {
            progress.Report((0, reqs.Count, reqs[0].Name.Title));
            try { await CreateGate.Task.WaitAsync(ct); }
            catch (OperationCanceledException) { return new CreateReport([], true); }
        }
        return new CreateReport(reqs.Select(r => (r.Name.Title, true, (string?)null, (string?)("new-" + r.Name.Id.Format()))).ToList(), false);
    }

    public Task<CreateReport> DuplicateAsync(IReadOnlyList<string> ids, DuplicateMode mode) =>
        Task.FromResult(new CreateReport([], false));

    public Task OpenSheetAsync(string uniqueId) => Task.CompletedTask;
    public Task<byte[]?> ThumbnailAsync(string uniqueId) => Task.FromResult<byte[]?>(null);

    public Prefs? SavedPrefs { get; private set; }
    public Task SaveAsync(Prefs? prefs, ProjectState? state, bool userLevel)
    {
        SavedPrefs = prefs;
        if (prefs is not null) Prefs = prefs;
        return Task.CompletedTask;
    }
}
