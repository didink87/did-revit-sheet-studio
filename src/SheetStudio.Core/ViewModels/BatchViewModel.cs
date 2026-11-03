using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetStudio.Core.Create;

namespace SheetStudio.Core.ViewModels;

public sealed class Choice(string id, string label, string group) : ObservableObject
{
    private bool _isChecked;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public string Group { get; } = group;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

/// <summary>Editable batch line for the grid, plus its planned number/status.</summary>
public sealed class BatchRowItem : ObservableObject
{
    private string? _kindId, _levelName, _nomor, _judul, _plannedNumber, _plannedTitle, _pesan;
    private string _subjek = "";
    private int _skala;
    private Kertas _kertas;
    private bool _buatView;
    private PlanStatus? _status;

    public BatchRowItem(BatchRow r)
    {
        _kindId = r.KindId; _subjek = r.Subjek; _levelName = r.LevelName; _nomor = r.NomorDiminta;
        _judul = r.Judul; _skala = r.Skala; _kertas = r.Kertas; _buatView = r.BuatView;
    }

    public string? KindId { get => _kindId; set => Set(ref _kindId, value); }
    public string Subjek { get => _subjek; set => Set(ref _subjek, value ?? ""); }
    public string? LevelName { get => _levelName; set => Set(ref _levelName, value); }
    public string? Nomor { get => _nomor; set => Set(ref _nomor, string.IsNullOrWhiteSpace(value) ? null : value); }
    public string? Judul { get => _judul; set => Set(ref _judul, string.IsNullOrWhiteSpace(value) ? null : value); }
    public int Skala { get => _skala; set => Set(ref _skala, value); }
    public Kertas Kertas { get => _kertas; set => Set(ref _kertas, value); }
    public bool BuatView { get => _buatView; set => Set(ref _buatView, value); }

    public PlanStatus? Status { get => _status; set => Set(ref _status, value); }
    public string? PlannedNumber { get => _plannedNumber; set => Set(ref _plannedNumber, value); }
    public string? PlannedTitle { get => _plannedTitle; set => Set(ref _plannedTitle, value); }
    public string? Pesan { get => _pesan; set => Set(ref _pesan, value); }

    public BatchRow ToRow() => new(_kindId, _subjek, _levelName, _nomor, _judul, _skala, _kertas, _buatView);
}

/// <summary>"⧉ Batch" window: matrix (kinds × levels), CSV, presets, preview, run with progress and cancel.</summary>
public sealed class BatchViewModel : ObservableObject
{
    private readonly StudioViewModel _studio;
    private IReadOnlyList<PlannedSheet> _plan = [];
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private double _progress;
    private string _progressText = "";
    private Kertas _kertas;

    internal BatchViewModel(StudioViewModel studio)
    {
        _studio = studio;
        _kertas = studio.Snapshot.Prefs.KertasDefault;
        Kinds = new(studio.Profile.Jenis.Select(k => new Choice(k.Id, k.Nama, GroupLabel(k.Disiplin, k.Tipe))));
        Levels = new(studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => new Choice(l.Name, l.Name, "")));
        BuildFromMatrix = new RelayCommand(BuildMatrix);
        PreviewCommand = new RelayCommand(Preview, () => !_isRunning);
        Run = new RelayCommand(async () => await RunAsync(), () => !_isRunning && _plan.Any(p => p.Status == PlanStatus.Baru));
        Cancel = new RelayCommand(() => _cts?.Cancel(), () => _isRunning);
        Clear = new RelayCommand(() => { Rows.Clear(); _plan = []; RaiseCommands(); });
    }

    public ObservableCollection<Choice> Kinds { get; }
    public ObservableCollection<Choice> Levels { get; }
    public ObservableCollection<BatchRowItem> Rows { get; } = [];
    public IReadOnlyList<string> PresetNames => Presets.Names;
    public IReadOnlyList<Kertas> KertasOptions { get; } = [Kertas.A3, Kertas.A2, Kertas.A1];

    public Kertas Kertas { get => _kertas; set => Set(ref _kertas, value); }
    public bool IsRunning { get => _isRunning; private set { if (Set(ref _isRunning, value)) RaiseCommands(); } }
    public double ProgressPercent { get => _progress; private set => Set(ref _progress, value); }
    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }
    public CreateReport? Report { get; private set; }
    public string Summary => _plan.Count == 0 ? "" :
        $"{_plan.Count(p => p.Status == PlanStatus.Baru)} baru · {_plan.Count(p => p.Status == PlanStatus.SudahAda)} sudah ada · " +
        $"{_plan.Count(p => p.Status == PlanStatus.Duplikat)} ganda · {_plan.Count(p => p.Status == PlanStatus.Galat)} galat";

    public ICommand BuildFromMatrix { get; }
    public ICommand PreviewCommand { get; }
    public ICommand Run { get; }
    public ICommand Cancel { get; }
    public ICommand Clear { get; }

    public void LoadPreset(string name) =>
        Replace(Presets.Get(name, _studio.Snapshot.Levels, _studio.Profile, _kertas));

    public IReadOnlyList<string> ImportCsv(string text)
    {
        var rows = BatchCsv.Read(text, _studio.Profile, out var errors);
        Replace(rows);
        return errors;
    }

    public string ExportCsv() => BatchCsv.Write(Rows.Select(r => r.ToRow()));

    public void Preview()
    {
        _plan = BatchPlanner.Plan(Rows.Select(r => r.ToRow()).ToList(), _studio.Sheets, _studio.Current, _studio.Profile, _studio.Numbering);
        for (var i = 0; i < Rows.Count; i++)
        {
            var p = _plan[i];
            Rows[i].Status = p.Status;
            Rows[i].PlannedNumber = p.Request?.Name.Id.Format();
            Rows[i].PlannedTitle = p.Request?.Name.Title;
            Rows[i].Pesan = p.Pesan;
        }
        Raise(nameof(Summary));
        RaiseCommands();
    }

    public async Task RunAsync()
    {
        if (_plan.Count != Rows.Count) Preview();
        var reqs = _plan.Where(p => p.Status == PlanStatus.Baru).Select(p => p.Request! with
        {
            TitleBlockType = _studio.Snapshot.Prefs.TitleBlockPerKertas.GetValueOrDefault(p.Request!.Kertas),
        }).ToList();
        if (reqs.Count == 0) return;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        try
        {
            var progress = new Progress<(int Done, int Total, string Title)>(x =>
            {
                ProgressPercent = x.Total == 0 ? 0 : 100.0 * x.Done / x.Total;
                ProgressText = $"{x.Done}/{x.Total} · {x.Title}";
            });
            Report = await _studio.Gateway.CreateAsync(reqs, progress, _cts.Token);
            Raise(nameof(Report));
            await _studio.AfterCreateAsync(Report);
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void BuildMatrix()
    {
        var kinds = Kinds.Where(k => k.IsChecked).Select(k => k.Id).ToList();
        var levels = _studio.Snapshot.Levels.Where(l => Levels.Any(c => c.IsChecked && c.Id == l.Name)).ToList();
        Replace(BatchMatrix.Build(kinds, levels, _studio.Profile, _kertas, true));
    }

    private void Replace(IEnumerable<BatchRow> rows)
    {
        Rows.Clear();
        foreach (var r in rows) Rows.Add(new BatchRowItem(r));
        _plan = [];
        Raise(nameof(Summary));
        RaiseCommands();
    }

    private void RaiseCommands()
    {
        foreach (var c in new[] { PreviewCommand, Run, Cancel }.OfType<RelayCommand>()) c.RaiseCanExecuteChanged();
    }

    private static string GroupLabel(string disc, int type) => disc.Length == 1 ? $"{disc}-{type}" : $"{disc}{type}";
}
