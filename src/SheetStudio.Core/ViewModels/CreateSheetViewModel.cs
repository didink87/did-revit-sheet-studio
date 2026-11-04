using System.Windows.Input;
using SheetStudio.Core.Create;

namespace SheetStudio.Core.ViewModels;

/// <summary>"+ Baru" dialog: name picker, paper/title block, and the view to place.</summary>
public sealed class CreateSheetViewModel : ObservableObject
{
    private readonly StudioViewModel _studio;
    private Kertas? _kertas;
    private string? _titleBlock;
    private ViewMode _viewMode = ViewMode.Tanpa;
    private string _viewSearch = "";
    private string? _selectedViewId;
    private string _newViewJenis = "Denah";
    private string? _levelName;
    private int _skala = 100;
    private string? _viewTemplate;

    internal CreateSheetViewModel(StudioViewModel studio)
    {
        _studio = studio;
        Picker = new NamePickerViewModel(studio.Profile, studio.Sheets, studio.Current, studio.Numbering,
            studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).ToList(), titles: studio.Titles);
        // Kertas & kop sengaja tidak diisi otomatis: pengguna harus memilih, dan diingatkan bila lupa.
        _levelName = studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).FirstOrDefault();
        Picker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NamePickerViewModel.SelectedKindId) && studio.Profile.FindKind(Picker.SelectedKindId) is { } k)
            {
                NewViewJenis = k.JenisView;
                if (k.SkalaDefault > 0) Skala = k.SkalaDefault;
                ViewTemplate = k.ViewTemplate;
            }
            if (e.PropertyName == nameof(NamePickerViewModel.FreeTitle) && GuessViewJenis(Picker.FreeTitle) is { } jenis)
                NewViewJenis = jenis;
            RaiseCan();
        };
        Create = new AsyncRelayCommand(CreateAsync, CanCreate, studio.ReportError);
    }

    public NamePickerViewModel Picker { get; }

    public IReadOnlyList<Kertas> KertasOptions { get; } = [global::SheetStudio.Core.Create.Kertas.A3, global::SheetStudio.Core.Create.Kertas.A2, global::SheetStudio.Core.Create.Kertas.A1];

    public Kertas? Kertas
    {
        get => _kertas;
        set
        {
            if (!Set(ref _kertas, value)) return;
            if (DefaultTitleBlock() is { } tb) TitleBlockType = tb;
            Raise(nameof(Peringatan));
        }
    }

    public IReadOnlyList<string> TitleBlockTypes => _studio.Snapshot.TitleBlockTypes;

    public string? TitleBlockType
    {
        get => _titleBlock;
        set { if (Set(ref _titleBlock, value)) Raise(nameof(Peringatan)); }
    }

    /// <summary>Teks peringatan bila kertas/kop belum dipilih; null bila lengkap.</summary>
    public string? Peringatan
    {
        get
        {
            var kurang = new List<string>();
            if (_kertas is null) kurang.Add("ukuran kertas (A3/A2/A1)");
            if (string.IsNullOrWhiteSpace(_titleBlock)) kurang.Add("kop / title block");
            else if (!TitleBlockTypes.Contains(_titleBlock)) return $"Kop '{_titleBlock}' tidak ada di model ini. Pilih kop lain.";
            return kurang.Count == 0 ? null : "Anda belum memilih " + string.Join(" dan ", kurang) + ".";
        }
    }

    /// <summary>Diminta saat BUAT ditekan tapi kertas/kop belum lengkap; UI menampilkan pesan.</summary>
    public event Action<string>? WarningRequested;

    public ViewMode ViewMode
    {
        get => _viewMode;
        set { if (Set(ref _viewMode, value)) RaiseCan(); }
    }

    public string ViewSearch
    {
        get => _viewSearch;
        set { if (Set(ref _viewSearch, value ?? "")) Raise(nameof(AvailableViews)); }
    }

    public IReadOnlyList<ViewInfo> AvailableViews =>
        _studio.Snapshot.Views.Where(v => !v.OnSheet && (_viewSearch.Length == 0 || v.Name.Contains(_viewSearch, StringComparison.OrdinalIgnoreCase))).ToList();

    public string? SelectedViewId
    {
        get => _selectedViewId;
        set { if (Set(ref _selectedViewId, value)) RaiseCan(); }
    }

    public IReadOnlyList<string> ViewJenisOptions { get; } = ["Denah", "Plafon", "Drafting", "Legend", "TigaD", "Tampak", "Potongan"];

    public string NewViewJenis
    {
        get => _newViewJenis;
        set => Set(ref _newViewJenis, value);
    }

    public IReadOnlyList<string> LevelNames => _studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).ToList();

    public string? LevelName
    {
        get => _levelName;
        set => Set(ref _levelName, value);
    }

    public int Skala
    {
        get => _skala;
        set => Set(ref _skala, Math.Max(1, value));
    }

    public string? ViewTemplate
    {
        get => _viewTemplate;
        set => Set(ref _viewTemplate, value);
    }

    public ICommand Create { get; }

    public event Action? Closed;

    public CreateRequest BuildRequest()
    {
        var views = ViewMode switch
        {
            ViewMode.PakaiYangAda => new[] { new ViewSpec(ViewMode.PakaiYangAda, _selectedViewId, null, null, 0, null) },
            ViewMode.BuatBaru => [new ViewSpec(ViewMode.BuatBaru, null, _newViewJenis, _levelName, _skala, _viewTemplate)],
            _ => [],
        };
        return new CreateRequest(Picker.Result, _kertas ?? _studio.Snapshot.Prefs.KertasDefault, _titleBlock, views);
    }

    public async Task CreateAsync()
    {
        if (!CanCreate()) return;
        if (Peringatan is { } w)
        {
            WarningRequested?.Invoke(w);
            return;
        }
        await _studio.CreateThenReloadAsync(() =>
            _studio.Gateway.CreateAsync([BuildRequest()], new Progress<(int, int, string)>(), CancellationToken.None));
        Closed?.Invoke();
    }

    private bool CanCreate() => Picker.CanConfirm && (_viewMode != ViewMode.PakaiYangAda || _selectedViewId is not null);

    /// <summary>Kop dari preferensi untuk kertas terpilih, hanya bila kop itu memang ada di model.</summary>
    private string? DefaultTitleBlock() =>
        _kertas is { } k && _studio.Snapshot.Prefs.TitleBlockPerKertas.TryGetValue(k, out var tb) && TitleBlockTypes.Contains(tb) ? tb : null;

    /// <summary>Tebak jenis view dari kata pertama judul (DENAH, TAMPAK, POTONGAN, …).</summary>
    public static string? GuessViewJenis(string? title)
    {
        var t = (title ?? "").Trim().ToUpperInvariant();
        if (t.StartsWith("DENAH")) return "Denah";
        if (t.StartsWith("RENCANA PLAFON") || t.StartsWith("PLAFON")) return "Plafon";
        if (t.StartsWith("TAMPAK")) return "Tampak";
        if (t.StartsWith("POTONGAN")) return "Potongan";
        if (t.StartsWith("PERSPEKTIF") || t.StartsWith("3D") || t.StartsWith("AKSONOMETRI")) return "TigaD";
        if (t.StartsWith("LEGENDA")) return "Legend";
        return null;
    }

    private void RaiseCan() => (Create as AsyncRelayCommand)?.RaiseCanExecuteChanged();
}
