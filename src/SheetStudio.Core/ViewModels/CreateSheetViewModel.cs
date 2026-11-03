using System.Windows.Input;
using SheetStudio.Core.Create;

namespace SheetStudio.Core.ViewModels;

/// <summary>"+ Baru" dialog: name picker, paper/title block, and the view to place.</summary>
public sealed class CreateSheetViewModel : ObservableObject
{
    private readonly StudioViewModel _studio;
    private Kertas _kertas;
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
            studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).ToList());
        _kertas = studio.Snapshot.Prefs.KertasDefault;
        _titleBlock = DefaultTitleBlock();
        _levelName = studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).FirstOrDefault();
        Picker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NamePickerViewModel.SelectedKindId) && studio.Profile.FindKind(Picker.SelectedKindId) is { } k)
            {
                NewViewJenis = k.JenisView;
                if (k.SkalaDefault > 0) Skala = k.SkalaDefault;
                ViewTemplate = k.ViewTemplate;
            }
            RaiseCan();
        };
        Create = new AsyncRelayCommand(CreateAsync, CanCreate, studio.ReportError);
    }

    public NamePickerViewModel Picker { get; }

    public IReadOnlyList<Kertas> KertasOptions { get; } = [Kertas.A3, Kertas.A2, Kertas.A1];

    public Kertas Kertas
    {
        get => _kertas;
        set { if (Set(ref _kertas, value)) TitleBlockType = DefaultTitleBlock(); }
    }

    public IReadOnlyList<string> TitleBlockTypes => _studio.Snapshot.TitleBlockTypes;

    public string? TitleBlockType
    {
        get => _titleBlock;
        set => Set(ref _titleBlock, value);
    }

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
        return new CreateRequest(Picker.Result, _kertas, _titleBlock, views);
    }

    public async Task CreateAsync()
    {
        if (!CanCreate()) return;
        var report = await _studio.Gateway.CreateAsync([BuildRequest()], new Progress<(int, int, string)>(), CancellationToken.None);
        await _studio.AfterCreateAsync(report);
        Closed?.Invoke();
    }

    private bool CanCreate() => Picker.CanConfirm && (_viewMode != ViewMode.PakaiYangAda || _selectedViewId is not null);

    private string? DefaultTitleBlock() =>
        _studio.Snapshot.Prefs.TitleBlockPerKertas.TryGetValue(_kertas, out var tb) ? tb : _studio.Snapshot.TitleBlockTypes.FirstOrDefault();

    private void RaiseCan() => (Create as AsyncRelayCommand)?.RaiseCanExecuteChanged();
}
