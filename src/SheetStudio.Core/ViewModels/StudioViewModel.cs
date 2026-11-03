using System.Windows.Input;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.ViewModels;

/// <summary>
/// Root view model of the Sheet Studio pane. Holds the snapshot, the staged ChangeSet with panel undo,
/// and derives rows/numbers after every change. Nothing reaches Revit until <see cref="ApplyAsync"/>.
/// </summary>
public sealed class StudioViewModel : ObservableObject
{
    private readonly IStudioGateway _gateway;
    private readonly UndoStack _undo = new();
    private string _status = "";
    private bool _busy;
    private readonly Dictionary<string, string> _baseVersi = [];

    public StudioViewModel(IStudioGateway gateway)
    {
        _gateway = gateway;
        Layers = new LayersViewModel(this);
        Inspector = new InspectorViewModel(this);
        Undo = new RelayCommand(() => Restore(_undo.Undo()), () => _undo.CanUndo);
        Redo = new RelayCommand(() => Restore(_undo.Redo()), () => _undo.CanRedo);
        Apply = new AsyncRelayCommand(() => ApplyAsync(), () => !Current.IsEmpty && !_busy && !HasBlockingErrors, ReportError);
        Discard = new RelayCommand(DiscardAll, () => !Current.IsEmpty);
        Refresh = new AsyncRelayCommand(SafeLoadAsync, null, ReportError);
        NewSheet = new RelayCommand(() => CreateRequested?.Invoke(NewCreateSheet()));
        Batch = new RelayCommand(() => BatchRequested?.Invoke(NewBatch()));
        NormalizeNumbers = new RelayCommand(() => Push(Current with { NormalizeAll = true }, "Semua nomor dirapikan sesuai urutan (belum diterapkan)"));
        OpenPreferences = new RelayCommand(() => PreferencesRequested?.Invoke(NewPreferences()));
        _gateway.ModelChanged += async () =>
        {
            try { await OnModelChangedAsync(); }
            catch (Exception ex) { ReportError(ex); }
        };
    }

    public LayersViewModel Layers { get; }
    public InspectorViewModel Inspector { get; }

    public StudioSnapshot Snapshot { get; private set; } =
        new("", [], ProjectState.Empty, Prefs.Default, [], [], []);

    public NamingProfile Profile { get; private set; } = ProfileLoader.LoadBuiltIn("pupr");
    public NumberingOptions Numbering => Snapshot.Prefs.Numbering;
    public ChangeSet Current { get; private set; } = ChangeSet.Start(ProjectState.Empty);
    public SequenceOutcome Outcome { get; private set; } = new([], []);
    public IReadOnlyList<SheetSnapshot> Sheets => Snapshot.Sheets;
    public HashSet<string> NewSheetIds { get; } = [];

    public ICommand Undo { get; }
    public ICommand Redo { get; }
    public ICommand Apply { get; }
    public ICommand Discard { get; }
    public ICommand Refresh { get; }
    public ICommand NewSheet { get; }
    public ICommand Batch { get; }
    public ICommand OpenPreferences { get; }

    /// <summary>"Rapikan semua nomor": renumber every group from the panel order, not only touched groups.</summary>
    public ICommand NormalizeNumbers { get; }

    public event Action<CreateSheetViewModel>? CreateRequested;
    public event Action<BatchViewModel>? BatchRequested;
    public event Action<PreferencesViewModel>? PreferencesRequested;

    /// <summary>Shown before Apply when the preference asks for confirmation; false cancels.</summary>
    public Func<ApplyPlan, Task<bool>>? ConfirmApply { get; set; }

    public int PendingCount => Current.PendingCount;

    /// <summary>Sequencer errors (full range, taken manual number) or a title Revit would reject: Apply stays disabled.</summary>
    public bool HasBlockingErrors { get; private set; }

    public void ReportError(Exception ex) => Status = "Galat: " + ex.Message;

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public bool IsStrategyUrut
    {
        get => Snapshot.Prefs.Strategy == Strategy.Urut;
        set => _ = SavePrefsAsync(Snapshot.Prefs with { Strategy = value ? Strategy.Urut : Strategy.Rentang }, userLevel: false);
    }

    public IStudioGateway Gateway => _gateway;

    public async Task LoadAsync()
    {
        Snapshot = await _gateway.LoadAsync();
        Profile = ProfileLoader.BuiltInIds.Contains(Snapshot.Prefs.ProfileId)
            ? ProfileLoader.LoadBuiltIn(Snapshot.Prefs.ProfileId)
            : ProfileLoader.LoadBuiltIn("pupr");
        Current = ChangeSet.Start(Snapshot.State);
        _undo.Reset(Current);
        _baseVersi.Clear();
        Recompute();
        Raise(nameof(Snapshot), nameof(Profile), nameof(IsStrategyUrut));
    }

    /// <summary>LoadAsync for UI triggers: a missing document or Revit error becomes a status line, never a crash.</summary>
    public async Task SafeLoadAsync()
    {
        try { await LoadAsync(); }
        catch (Exception ex) { Status = ex.Message; }
    }

    /// <summary>Applies a staged operation result: pushes it on the undo stack and refreshes everything.</summary>
    public void Push(OpResult r) => Push(r.Next, r.Pesan);

    public void Push(ChangeSet next, string? pesan = null)
    {
        var rejected = Edit.BulkEdit.TakeRejected();
        if (!ReferenceEquals(next, Current))
        {
            Current = next;
            _undo.Push(next);
            RememberBaseVersions();
            Recompute();
        }
        Status = rejected.Count > 0 ? string.Join("; ", rejected) : pesan ?? StatusFromOutcome();
    }

    /// <summary>The version a sheet had when the user first staged something on it; Apply skips it if Revit's copy
    /// changed since then, even after a refresh replaced the snapshot.</summary>
    private void RememberBaseVersions()
    {
        var byId = Sheets.ToDictionary(s => s.UniqueId, s => s.Versi);
        foreach (var id in Current.Changes.Keys.Concat(Current.Touched).Concat(Current.Relocked))
            if (!_baseVersi.ContainsKey(id) && byId.TryGetValue(id, out var v)) _baseVersi[id] = v;
    }

    public async Task<bool> ApplyAsync()
    {
        if (Current.IsEmpty || _busy) return false;
        var plan = BuildPlan();
        if (Snapshot.Prefs.KonfirmasiTerapkan && ConfirmApply is not null && !await ConfirmApply(plan))
        {
            Status = "Terapkan dibatalkan";
            return false;
        }
        _busy = true;
        try
        {
            var report = await _gateway.ApplyAsync(plan);
            await LoadAsync();
            Status = report.Skipped.Count == 0
                ? $"{report.Applied} perubahan diterapkan"
                : $"{report.Applied} diterapkan, {report.Skipped.Count} dilewati: " + string.Join("; ", report.Skipped.Select(s => s.Pesan).Distinct());
            return true;
        }
        finally { _busy = false; }
    }

    public ApplyPlan BuildPlan()
    {
        var byId = Sheets.ToDictionary(s => s.UniqueId);
        var renumber = TwoPhasePlan.Build(Outcome.Results);
        var edits = Current.Changes.Values
            .Where(c => !c.Delete && (c.NewTitle is not null || c.InSheetList is not null || c.NewTitleBlockType is not null || c.NewParams is { Count: > 0 }))
            .ToList();
        var deletes = Current.Changes.Values.Where(c => c.Delete).Select(c => c.UniqueId).ToList();
        var deleteSet = deletes.ToHashSet();
        var ids = renumber.Select(r => r.UniqueId).Concat(edits.Select(e => e.UniqueId)).Concat(deletes).Distinct();
        var expected = ids.Where(byId.ContainsKey).ToDictionary(id => id, id => _baseVersi.GetValueOrDefault(id, byId[id].Versi));

        // Persist the full panel order under the final group keys.
        var display = LayerListBuilder.OrderedSheets(DisplaySheets(), Current.State, Profile);
        var order = display.Where(s => !deleteSet.Contains(s.UniqueId))
            .GroupBy(s => LayerListBuilder.GroupOf(s.Number, Profile).Type)
            .Where(g => g.Key != LayerListBuilder.UnknownType)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(s => s.UniqueId).ToList());
        var state = Current.State with
        {
            OrderPerGroup = order,
            Locked = Current.State.Locked.Where(id => !deleteSet.Contains(id)).ToHashSet(),
        };
        return new ApplyPlan(Snapshot.DocumentKey, renumber, edits, deletes, expected, state);
    }

    public Task OnModelChangedAsync() => ReloadKeepingChangesAsync();

    /// <summary>Pane shown / view activated: first load, or reload keeping staged work (document switch aware).</summary>
    public async Task OnShownAsync()
    {
        try
        {
            if (Snapshot.DocumentKey.Length == 0) await LoadAsync();
            else await ReloadKeepingChangesAsync();
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    /// <summary>Re-reads the model but keeps staged work (rebased). A different active document resets everything.</summary>
    private async Task ReloadKeepingChangesAsync(string? statusIfOk = null)
    {
        var pending = Current;
        var fresh = await _gateway.LoadAsync();
        if (fresh.DocumentKey != Snapshot.DocumentKey)
        {
            if (pending.PendingCount > 0)
            {
                // Keep the staged work for its own document; Apply is refused elsewhere (Revit-side document check).
                Status = $"{pending.PendingCount} perubahan tertunda milik dokumen lain — kembali ke dokumen itu untuk menerapkan, atau Buang";
                return;
            }
            await LoadAsync();
            Status = "Dokumen aktif berganti";
            return;
        }
        Snapshot = fresh;
        var (next, dropped) = Rebase.Onto(pending, Snapshot.Sheets, Profile);
        Current = next;
        foreach (var id in dropped) _baseVersi.Remove(id);
        _undo.Reset(Current);
        Recompute();
        Status = dropped.Count > 0 ? $"{dropped.Count} perubahan dibuang karena lembarnya terhapus di model" : statusIfOk ?? Status;
    }

    public CreateSheetViewModel NewCreateSheet() => new(this);
    public BatchViewModel NewBatch() => new(this);
    public PreferencesViewModel NewPreferences() => new(this, Snapshot.Prefs);

    public async Task AfterCreateAsync(CreateReport report)
    {
        foreach (var r in report.Rows) if (r.UniqueId is not null) NewSheetIds.Add(r.UniqueId);
        var ok = report.Rows.Count(r => r.Ok);
        await ReloadKeepingChangesAsync(report.Dibatalkan ? "Pembuatan dibatalkan; model dikembalikan" : $"{ok} lembar dibuat" +
            (report.Rows.Count > ok ? $", {report.Rows.Count - ok} gagal" : ""));
    }

    public async Task SavePrefsAsync(Prefs prefs, bool userLevel)
    {
        await _gateway.SaveAsync(prefs, null, userLevel);
        Snapshot = Snapshot with { Prefs = prefs };
        Recompute();
        Raise(nameof(Snapshot), nameof(IsStrategyUrut));
    }

    /// <summary>Snapshots as the panel shows them: new numbers, staged titles and visibility.</summary>
    public IReadOnlyList<SheetSnapshot> DisplaySheets()
    {
        var numbers = Outcome.Results.ToDictionary(r => r.UniqueId, r => r.NewNumber);
        return Current.EffectiveSheets(Sheets, Profile).Select(s =>
        {
            Current.Changes.TryGetValue(s.UniqueId, out var c);
            return s with
            {
                Number = numbers.TryGetValue(s.UniqueId, out var n) ? n : s.Number,
                Title = c?.NewTitle ?? s.Title,
                InSheetList = c?.InSheetList ?? s.InSheetList,
            };
        }).ToList();
    }

    public IReadOnlyList<SheetSnapshot> SelectedSheets()
    {
        var ids = Layers.SelectedSheetIds().ToHashSet();
        return Sheets.Where(s => ids.Contains(s.UniqueId)).ToList();
    }

    private void Recompute()
    {
        Outcome = Sequencer.Compute(Sheets, Current, Profile, Numbering);
        HasBlockingErrors = Outcome.Errors.Count > 0 ||
            DisplaySheets().Any(s => Current.Changes.ContainsKey(s.UniqueId) && TitleRules.Check(s.Title, Profile).Severity == Severity.Galat);
        Layers.Rebuild();
        Inspector.Refresh();
        Raise(nameof(PendingCount), nameof(Current), nameof(Outcome), nameof(HasBlockingErrors));
        foreach (var c in new[] { Undo, Redo, Discard }.OfType<RelayCommand>()) c.RaiseCanExecuteChanged();
        (Apply as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Restore(ChangeSet? state)
    {
        if (state is null) return;
        Current = state;
        Recompute();
        Status = StatusFromOutcome();
    }

    private void DiscardAll()
    {
        Current = ChangeSet.Start(Snapshot.State);
        _undo.Reset(Current);
        _baseVersi.Clear();
        Recompute();
        Status = "Perubahan dibuang";
    }

    private string StatusFromOutcome() => Outcome.Errors.Count > 0 ? string.Join("; ", Outcome.Errors) : "";

    internal string? WarningFor(SheetSnapshot display)
    {
        if (!SheetId.Parse(display.Number, Profile).Ok) return "Nomor tidak standar UDS";
        var check = TitleRules.Check(display.Title, Profile);
        return check.Severity == Severity.Ok ? null : check.Pesan + (check.Hints.Count > 0 ? ": " + string.Join(", ", check.Hints.Select(h => h.Found)) : "");
    }
}
