using System.Windows.Input;
using SheetStudio.Core.Changes;
using SheetStudio.Core.Layers;
using SheetStudio.Core.Model;
using SheetStudio.Core.Naming;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.ViewModels;

public sealed record DisciplineOption(string Code, string NamaId, string NamaEn, string Keterangan, bool IsSelected);
public sealed record SecondLetterOption(char? Huruf, string Nama, string Keterangan);
public sealed record KindOption(string? Id, string Nama);
public sealed record CreateName(SheetId Id, string Title, string? KindId, string Subjek);
public sealed record NumberCell(int Seq, string Label, CellState State);

/// <summary>Interactive sheet number + title picker: discipline chips, type slider, number map, title builder.</summary>
public sealed class NamePickerViewModel : ObservableObject
{
    public const string Bebas = "BEBAS (KETIK SENDIRI)";
    private static readonly string[] Elevations = ["TAMPAK DEPAN", "TAMPAK BELAKANG", "TAMPAK KIRI", "TAMPAK KANAN"];
    private static readonly string[] Sections = ["POTONGAN A-A", "POTONGAN B-B", "POTONGAN C-C", "POTONGAN D-D"];

    private readonly NamingProfile _p;
    private readonly IReadOnlyList<SheetSnapshot> _all;
    private readonly string? _editingId;
    private readonly ChangeSet _cs;
    private readonly NumberingOptions _o;
    private readonly IReadOnlyList<string> _levels;

    private string _discipline = "A";
    private char? _second;
    private int _type = 1;
    private bool _showAll;
    private bool _autoSeq = true;
    private int _manualSeq = 1;
    private string? _kindId;
    private string _subject = "";
    private string _freeTitle = "";

    public NamePickerViewModel(NamingProfile p, IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs, NumberingOptions o,
        IReadOnlyList<string> levelNames, string? editingUniqueId = null)
    {
        _p = p;
        _cs = cs;
        _o = o;
        _levels = levelNames;
        _all = sheets;
        _editingId = editingUniqueId;

        if (editingUniqueId is not null && sheets.FirstOrDefault(s => s.UniqueId == editingUniqueId) is { } e
            && SheetId.Parse(e.Number, p) is { Ok: true } parsed)
        {
            _discipline = parsed.Id.Discipline[..1];
            _second = parsed.Id.Discipline.Length == 2 ? parsed.Id.Discipline[1] : null;
            _type = parsed.Id.Type;
            _autoSeq = false;
            _manualSeq = parsed.Id.Seq;
            _kindId = p.FindKind(e.KindId) is { } k && k.Disiplin == parsed.Id.Discipline && k.Tipe == parsed.Id.Type ? k.Id : null;
            _subject = e.Subjek;
            _freeTitle = _kindId is null ? e.Title : "";
        }

        NextDiscipline = new RelayCommand(() => StepDiscipline(+1));
        PrevDiscipline = new RelayCommand(() => StepDiscipline(-1));
        PickCell = new RelayCommand(o => { if (o is int seq) Pick(seq); });
        PickType = new RelayCommand(o => { if (o is int t) SheetType = t; else if (o is string str && int.TryParse(str, out var d)) SheetType = d; });
        AcceptSuggestions = new RelayCommand(Accept);
    }

    // ── Disiplin ─────────────────────────────────────────────
    public IReadOnlyList<DisciplineOption> DisciplineOptions =>
        _p.Disiplin.OrderBy(d => d.Order)
            .Where(d => _showAll || d.Umum || d.Code == _discipline)
            .Select(d => new DisciplineOption(d.Code, d.NamaId, d.NamaEn, d.Keterangan, d.Code == _discipline))
            .ToList();

    public bool ShowAllDisciplines
    {
        get => _showAll;
        set { if (Set(ref _showAll, value)) Raise(nameof(DisciplineOptions)); }
    }

    public string SelectedDiscipline
    {
        get => _discipline;
        set
        {
            if (_p.FindDiscipline(value) is null || value == _discipline) return;
            _discipline = value;
            _second = null;
            DropKindIfMismatched();
            Changed(nameof(SelectedDiscipline), nameof(DisciplineOptions), nameof(SecondLetters), nameof(SelectedSecondLetter), nameof(DisciplineDescription));
        }
    }

    public IReadOnlyList<SecondLetterOption> SecondLetters =>
        new[] { new SecondLetterOption(null, "— (tanpa)", "Satu huruf; tanda '-' mengisi posisi huruf kedua") }
            .Concat(_p.FindDiscipline(_discipline)!.HurufKedua.Select(h => new SecondLetterOption(h.Huruf, $"{_discipline}{h.Huruf} {h.Nama}", h.Keterangan)))
            .ToList();

    public char? SelectedSecondLetter
    {
        get => _second;
        set
        {
            if (_second == value) return;
            _second = value;
            DropKindIfMismatched();
            Changed(nameof(SelectedSecondLetter));
        }
    }

    public string DisciplineDescription
    {
        get
        {
            var d = _p.FindDiscipline(_discipline)!;
            return $"{d.NamaId} ({d.NamaEn}) — {d.Keterangan}";
        }
    }

    public ICommand NextDiscipline { get; }
    public ICommand PrevDiscipline { get; }

    // ── Tipe ─────────────────────────────────────────────────
    public int SheetType
    {
        get => _type;
        set
        {
            value = Math.Clamp(value, 0, 9);
            if (value == _type) return;
            _type = value;
            DropKindIfMismatched();
            Changed(nameof(SheetType), nameof(TypeName), nameof(TypeDescription));
        }
    }

    public IReadOnlyList<string> TypeTickLabels =>
        _p.Tipe.OrderBy(t => t.Digit).Select(t => $"{t.Digit} {t.Keterangan.Split(':')[0].TrimEnd('.')}").ToList();

    public string TypeName => _p.Tipe.First(t => t.Digit == _type).Nama;
    public string TypeDescription => _p.Tipe.First(t => t.Digit == _type).Keterangan;

    // ── Urut ─────────────────────────────────────────────────
    public bool AutoSeq
    {
        get => _autoSeq;
        set { if (Set(ref _autoSeq, value)) Changed(); }
    }

    public int ManualSeq
    {
        get => _manualSeq;
        set { if (Set(ref _manualSeq, Math.Clamp(value, 0, 99))) Changed(); }
    }

    public int EffectiveSeq => _autoSeq ? FirstFree() : _manualSeq;

    public IReadOnlyList<CellState> NumberMapCells
    {
        get
        {
            var cells = NumberMap.For(Disc, _type, _kindId, _all, _cs, _p, _o, EffectiveSeq).ToArray();
            // the sheet being edited does not block its own (old) number
            if (OwnSeq() is int own && own is >= 1 and <= 99 && cells[own - 1] != CellState.Dipilih) cells[own - 1] = CellState.Bebas;
            return cells;
        }
    }

    public ICommand PickCell { get; }

    /// <summary>The 10×10 map as bindable cells (01–99).</summary>
    public IReadOnlyList<NumberCell> NumberCells =>
        NumberMapCells.Select((state, i) => new NumberCell(i + 1, (i + 1).ToString("00"), state)).ToList();

    /// <summary>Clicking a tick label under the slider.</summary>
    public ICommand PickType { get; }

    // ── Judul ────────────────────────────────────────────────
    public IReadOnlyList<KindOption> KindOptions =>
        _p.KindsFor(Disc, _type).Select(k => new KindOption(k.Id, k.Nama)).Append(new KindOption(null, Bebas)).ToList();

    public string? SelectedKindId
    {
        get => _kindId;
        set
        {
            if (_kindId == value) return;
            var kind = _p.FindKind(value);
            _kindId = kind?.Id;
            if (kind is not null)
            {
                _discipline = kind.Disiplin[..1];
                _second = kind.Disiplin.Length == 2 ? kind.Disiplin[1] : null;
                _type = kind.Tipe;
            }
            Changed(nameof(SelectedKindId), nameof(SelectedDiscipline), nameof(SelectedSecondLetter), nameof(DisciplineOptions),
                nameof(SecondLetters), nameof(DisciplineDescription), nameof(SheetType), nameof(TypeName), nameof(TypeDescription));
        }
    }

    public IReadOnlyList<string> SubjectOptions =>
        _levels.Select(TitleRules.Normalize).Concat(Elevations).Concat(Sections).ToList();

    public string Subject
    {
        get => _subject;
        set { if (Set(ref _subject, value ?? "")) Changed(); }
    }

    public string FreeTitle
    {
        get => _freeTitle;
        set { if (Set(ref _freeTitle, value ?? "")) Changed(); }
    }

    public string TitlePreview => TitleCheck.Normalized;
    public IReadOnlyList<AbbrevHint> Hints => TitleCheck.Hints;
    public ICommand AcceptSuggestions { get; }

    // ── Hasil ────────────────────────────────────────────────
    public string NumberPreview => new SheetId(Disc, _type, EffectiveSeq).Format();

    public Severity Status => NumberError is not null ? Severity.Galat : TitleCheck.Severity;

    public string StatusText => NumberError ?? TitleCheck.Pesan ?? "✓ valid";

    public bool CanConfirm => Status != Severity.Galat;

    public CreateName Result =>
        new(new SheetId(Disc, _type, EffectiveSeq), TitlePreview, _kindId, TitleRules.Normalize(_subject));

    // ── internals ────────────────────────────────────────────
    private string Disc => _second is char c ? $"{_discipline}{c}" : _discipline;

    private TitleCheck TitleCheck => TitleRules.Check(RawTitle, _p);

    private string RawTitle =>
        _p.FindKind(_kindId) is { } k ? k.TemplatJudul.Replace("{subjek}", _subject).Trim() : _freeTitle;

    private HashSet<int> UsedSeqs()
    {
        var group = LayerListBuilder.TypeKey(Disc, _type);
        var used = new HashSet<int>();
        foreach (var r in Sequencer.Compute(_all, _cs, _p, _o).Results)
            if (r.UniqueId != _editingId && SheetId.Parse(r.NewNumber, _p) is { Ok: true } id && LayerListBuilder.TypeKey(id.Id.Discipline, id.Id.Type) == group)
                used.Add(id.Id.Seq);
        return used;
    }

    private int? OwnSeq()
    {
        if (_editingId is null) return null;
        var r = Sequencer.Compute(_all, _cs, _p, _o).Results.FirstOrDefault(x => x.UniqueId == _editingId);
        return r is not null && SheetId.Parse(r.NewNumber, _p) is { Ok: true } id
               && LayerListBuilder.TypeKey(id.Id.Discipline, id.Id.Type) == LayerListBuilder.TypeKey(Disc, _type) ? id.Id.Seq : null;
    }

    private int FirstFree()
    {
        var (from, to) = Sequencer.RangeFor(Disc, _type, _kindId, _p, _o);
        var used = UsedSeqs();
        for (var s = from; s <= to; s++) if (used.Contains(s) == false) return s;
        return to;
    }

    private string? NumberError
    {
        get
        {
            var seq = EffectiveSeq;
            var number = new SheetId(Disc, _type, seq).Format();
            if (seq == 0 && !(Disc == "G" && _type == 0)) return "Sequence 00 hanya untuk G-000";
            if (!UsedSeqs().Contains(seq)) return null;
            return _autoSeq ? $"Rentang {number[..^2]}xx penuh — pilih nomor manual atau jenis lain" : $"Nomor {number} sudah dipakai";
        }
    }

    private void Pick(int seq)
    {
        if (seq is < 1 or > 99 || NumberMapCells[seq - 1] != CellState.Bebas) return;
        _autoSeq = false;
        _manualSeq = seq;
        Changed(nameof(AutoSeq), nameof(ManualSeq));
    }

    private void Accept()
    {
        var fixedTitle = TitleRules.ApplySuggestions(TitlePreview, _p);
        _kindId = null;
        _freeTitle = fixedTitle;
        Changed(nameof(SelectedKindId), nameof(FreeTitle));
    }

    private void StepDiscipline(int delta)
    {
        var codes = DisciplineOptions.Select(d => d.Code).ToList();
        var i = codes.IndexOf(_discipline);
        SelectedDiscipline = codes[((i + delta) % codes.Count + codes.Count) % codes.Count];
    }

    private void DropKindIfMismatched()
    {
        if (_kindId is null) return;
        var k = _p.FindKind(_kindId)!;
        if (k.Disiplin != Disc || k.Tipe != _type)
        {
            _kindId = null;
            Raise(nameof(SelectedKindId));
        }
    }

    /// <summary>Re-raises every derived property after any input changed.</summary>
    private void Changed(params string[] extra)
    {
        Raise(extra);
        Raise(nameof(KindOptions), nameof(EffectiveSeq), nameof(NumberMapCells), nameof(NumberCells), nameof(NumberPreview), nameof(TitlePreview),
            nameof(Hints), nameof(Status), nameof(StatusText), nameof(CanConfirm), nameof(Result));
    }
}
