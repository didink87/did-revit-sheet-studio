using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Edit;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;

namespace SheetStudio.Core.ViewModels;

public sealed class ParamField : ObservableObject
{
    private string _value;

    public ParamField(string name, string value, bool isMixed)
    {
        Name = name;
        _value = value;
        IsMixed = isMixed;
    }

    public string Name { get; }
    public bool IsMixed { get; }

    public string Value
    {
        get => _value;
        set => Set(ref _value, value ?? "");
    }
}

/// <summary>Edits the selected sheet(s). Mixed values show as <see cref="MixedText"/>; setting a value applies to all.</summary>
public sealed class InspectorViewModel : ObservableObject
{
    public const string MixedText = "<berbeda>";

    private readonly StudioViewModel _studio;
    private IReadOnlyList<SheetSnapshot> _sel = [];
    private IReadOnlyList<SheetSnapshot> _display = [];
    private string? _title;
    private string _find = "", _replace = "", _prefix = "", _suffix = "";
    private bool _wholeWord = true;
    private bool _refreshing;

    internal InspectorViewModel(StudioViewModel studio)
    {
        _studio = studio;
        ApplyTitle = new RelayCommand(() => { if (!string.IsNullOrWhiteSpace(_title)) Push(BulkEdit.SetTitle(_studio.Current, _sel, _title!)); }, HasSelection);
        ApplyFindReplace = new RelayCommand(() => Push(BulkEdit.FindReplace(_studio.Current, _sel, _find, _replace, _wholeWord)), HasSelection);
        ApplyPrefixSuffix = new RelayCommand(() => Push(BulkEdit.PrefixSuffix(_studio.Current, _sel, _prefix, _suffix)), HasSelection);
        Uppercase = new RelayCommand(() => Push(BulkEdit.Uppercase(_studio.Current, _sel)), HasSelection);
        AcceptSuggestions = new RelayCommand(() => Push(BulkEdit.ApplySuggestions(_studio.Current, _sel, _studio.Profile)), HasSelection);
        ApplyParam = new RelayCommand(o => { if (o is ParamField f) Push(BulkEdit.SetParam(_studio.Current, _sel, f.Name, f.Value)); });
        Duplicate = new AsyncRelayCommand(o => DuplicateAsync(o is DuplicateMode m ? m : DuplicateMode.LembarSaja), _ => HasSelection(), _studio.ReportError);
        Delete = new RelayCommand(() => _studio.Layers.MarkDeleteKeys(_sel.Select(s => s.UniqueId).ToList()), HasSelection);
        EditNumber = new RelayCommand(() =>
        {
            if (_sel.Count != 1) return;
            var picker = new NamePickerViewModel(_studio.Profile, _studio.Sheets, _studio.Current, _studio.Numbering,
                _studio.Snapshot.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).ToList(), _sel[0].UniqueId);
            EditNumberRequested?.Invoke(picker);
        }, () => _sel.Count == 1);
    }

    public int SelectedCount => _sel.Count;
    public bool HasSelectionValue => _sel.Count > 0;
    public bool IsSeqEnabled => _sel.Count == 1;

    public string? Discipline
    {
        get => Common(s => Parse(s)?.Discipline);
        set
        {
            if (_refreshing || value is null || value == Discipline) return;
            Push(BulkEdit.SetDisciplineType(_studio.Current, _sel, value, null, _studio.Profile));
        }
    }

    public int? SheetType
    {
        get => Common(s => Parse(s)?.Type);
        set
        {
            if (_refreshing || value is null || value == SheetType) return;
            Push(BulkEdit.SetDisciplineType(_studio.Current, _sel, null, value, _studio.Profile));
        }
    }

    public int? ManualSeq
    {
        get => _display.Count == 1 ? Parse(_display[0])?.Seq : null;
        set
        {
            if (_refreshing || value is null || _sel.Count != 1 || value == ManualSeq) return;
            _studio.Push(_studio.Current.Upsert(new SheetChange(_sel[0].UniqueId, ManualSeq: value)));
        }
    }

    /// <summary>Description of the common sheet type, or the mixed marker.</summary>
    public string TypeDescription => SheetType is int t && _studio.Profile.Tipe.FirstOrDefault(x => x.Digit == t) is { } d
        ? $"{d.Nama} — {d.Keterangan}" : _sel.Count > 1 ? MixedText : "";

    public bool TitleIsMixed => _display.Select(s => s.Title).Distinct().Count() > 1;
    public string TitleDisplay => TitleIsMixed ? MixedText : _title ?? "";

    public string? Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Find { get => _find; set => Set(ref _find, value ?? ""); }
    public string Replace { get => _replace; set => Set(ref _replace, value ?? ""); }
    public string Prefix { get => _prefix; set => Set(ref _prefix, value ?? ""); }
    public string Suffix { get => _suffix; set => Set(ref _suffix, value ?? ""); }
    public bool WholeWord { get => _wholeWord; set => Set(ref _wholeWord, value); }

    public ObservableCollection<ParamField> Params { get; } = [];

    public string? TitleBlockType
    {
        get => Common(s => EffectiveTitleBlock(s));
        set
        {
            if (_refreshing || value is null || value == TitleBlockType) return;
            Push(BulkEdit.SetTitleBlock(_studio.Current, _sel, value));
        }
    }

    public IReadOnlyList<string> TitleBlockTypes => _studio.Snapshot.TitleBlockTypes;

    public bool? InSheetList
    {
        get => _display.Count == 0 ? null : Common(s => (bool?)s.InSheetList);
        set
        {
            if (_refreshing || value is null) return;
            _studio.Push(_studio.Current.SetInSheetList(_sel.Select(s => s.UniqueId).ToList(), value.Value));
        }
    }

    public IReadOnlyList<string> DisciplineCodes => _studio.Profile.Disiplin.OrderBy(d => d.Order).Select(d => d.Code).ToList();

    public ICommand ApplyTitle { get; }
    public ICommand ApplyFindReplace { get; }
    public ICommand ApplyPrefixSuffix { get; }
    public ICommand Uppercase { get; }
    public ICommand AcceptSuggestions { get; }
    public ICommand ApplyParam { get; }
    public ICommand Duplicate { get; }
    public ICommand Delete { get; }

    /// <summary>Raised by <see cref="EditNumber"/>: the view shows a NamePicker in edit mode and calls <see cref="ApplyName"/> on OK.</summary>
    public event Action<NamePickerViewModel>? EditNumberRequested;

    public ICommand EditNumber { get; }

    public IReadOnlyList<Profiles.DisciplineDef> DisciplineDefs => _studio.Profile.Disiplin.OrderBy(d => d.Order).ToList();

    /// <summary>Stages discipline, type, sequence and title of the single selected sheet from the picker result.</summary>
    public void ApplyName(CreateName name)
    {
        if (_sel.Count != 1) return;
        var s = _sel[0];
        var cs = BulkEdit.SetDisciplineType(_studio.Current, [s], name.Id.Discipline, name.Id.Type, _studio.Profile);
        cs = cs.Upsert(new SheetChange(s.UniqueId, ManualSeq: name.Id.Seq)).Next;
        cs = BulkEdit.SetTitle(cs, [s], name.Title);
        Push(cs);
    }

    internal void Refresh()
    {
        _refreshing = true;
        try
        {
            _sel = _studio.SelectedSheets();
            var ids = _sel.Select(s => s.UniqueId).ToHashSet();
            _display = _studio.DisplaySheets().Where(s => ids.Contains(s.UniqueId)).ToList();
            _title = TitleIsMixed ? null : _display.FirstOrDefault()?.Title;

            Params.Clear();
            foreach (var name in _sel.SelectMany(s => s.Params.Keys).Distinct().OrderBy(n => n))
            {
                var values = _sel.Select(s => EffectiveParam(s, name)).Distinct().ToList();
                Params.Add(new ParamField(name, values.Count == 1 ? values[0] : "", values.Count > 1));
            }
            Raise(nameof(SelectedCount), nameof(HasSelectionValue), nameof(IsSeqEnabled), nameof(Discipline), nameof(SheetType),
                nameof(ManualSeq), nameof(TypeDescription), nameof(TitleIsMixed), nameof(TitleDisplay), nameof(Title), nameof(TitleBlockType),
                nameof(TitleBlockTypes), nameof(InSheetList), nameof(DisciplineCodes));
            foreach (var c in new[] { ApplyTitle, ApplyFindReplace, ApplyPrefixSuffix, Uppercase, AcceptSuggestions, Delete, EditNumber }.OfType<RelayCommand>())
                c.RaiseCanExecuteChanged();
            (Duplicate as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        finally { _refreshing = false; }
    }

    private bool HasSelection() => _sel.Count > 0;

    private void Push(ChangeSet next) => _studio.Push(next);

    private async Task DuplicateAsync(DuplicateMode mode)
    {
        var report = await _studio.Gateway.DuplicateAsync(_sel.Select(s => s.UniqueId).ToList(), mode);
        await _studio.AfterCreateAsync(report);
    }

    private SheetId? Parse(SheetSnapshot s) => SheetId.Parse(s.Number, _studio.Profile) is { Ok: true } r ? r.Id : null;

    private T? Common<T>(Func<SheetSnapshot, T?> pick)
    {
        if (_display.Count == 0) return default;
        var values = _display.Select(pick).Distinct().ToList();
        return values.Count == 1 ? values[0] : default;
    }

    private string EffectiveTitleBlock(SheetSnapshot s) =>
        _studio.Current.Changes.TryGetValue(s.UniqueId, out var c) && c.NewTitleBlockType is not null ? c.NewTitleBlockType : s.TitleBlockType;

    private string EffectiveParam(SheetSnapshot s, string name) =>
        _studio.Current.Changes.TryGetValue(s.UniqueId, out var c) && c.NewParams?.TryGetValue(name, out var v) == true
            ? v : s.Params.GetValueOrDefault(name, "");
}
