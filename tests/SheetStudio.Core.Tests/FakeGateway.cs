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
    public TaskCompletionSource? LoadGate { get; set; }
    public string? CreateError { get; set; }

    public event Action? ModelChanged;
    public void RaiseModelChanged() => ModelChanged?.Invoke();

    public string? LoadError { get; set; }
    public string DocumentKey { get; set; } = "doc1";
    public string DocumentId { get; set; } = "";
    public HashSet<string> OpenKeys { get; set; } = [];
    public string? ApplyError { get; set; }

    public async Task<StudioSnapshot> LoadAsync()
    {
        Loads++;
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        if (LoadGate is not null) await LoadGate.Task;
        return new StudioSnapshot(DocumentKey, Sheets.ToList(), State, Prefs, [("Lantai 1", 0), ("Lantai 2", 4), ("Lantai 3", 8)],
            ["PUPR_KOP_A3", "PUPR_KOP_A2"], [new ViewInfo("v1", "DENAH LANTAI 1", "Denah", false), new ViewInfo("v2", "TAMPAK", "Tampak", true)])
            { DocumentId = DocumentId, OpenDocumentKeys = OpenKeys };
    }

    public Task<ApplyReport> ApplyAsync(ApplyPlan plan)
    {
        if (ApplyError is not null) return Task.FromException<ApplyReport>(new Exception(ApplyError));
        Applied.Add(plan);
        State = plan.NewState;
        return Task.FromResult(new ApplyReport(plan.Renumber.Count + plan.Edits.Count + plan.Deletes.Count, []));
    }

    public async Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done, int Total, string Title)> progress, CancellationToken ct)
    {
        Created.Add(reqs);
        if (CreateError is not null) throw new InvalidOperationException(CreateError);
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
    public byte[]? Thumb { get; set; }
    public int ThumbCalls { get; private set; }
    public TaskCompletionSource? ThumbGate { get; set; }
    public string? ThumbError { get; set; }
    public List<string> ThumbRequests { get; } = [];
    public async Task<byte[]?> ThumbnailAsync(string uniqueId)
    {
        ThumbCalls++;
        ThumbRequests.Add(uniqueId);
        if (ThumbError is { } err) throw new InvalidOperationException(err);
        if (ThumbGate is { } gate) await gate.Task;
        return Thumb;
    }

    public Prefs? SavedPrefs { get; private set; }
    public Task SaveAsync(Prefs? prefs, ProjectState? state, bool userLevel)
    {
        SavedPrefs = prefs;
        if (prefs is not null) Prefs = prefs;
        return Task.CompletedTask;
    }
}
