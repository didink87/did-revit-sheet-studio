namespace SheetStudio.Core.Profiles;

public sealed record SecondLetterDef(char Huruf, string Nama, string Keterangan);

public sealed record DisciplineDef(
    string Code, string NamaId, string NamaEn, string Keterangan, int Order, bool Umum,
    IReadOnlyList<SecondLetterDef> HurufKedua);

public sealed record SheetTypeDef(int Digit, string Nama, string Keterangan);

public sealed record DrawingKindDef(
    string Id, string Nama, string Disiplin, int Tipe, int SeqFrom, int SeqTo, string TemplatJudul,
    bool PerLevel, string JenisView, int SkalaDefault, string? ViewTemplate);

public sealed record NamingProfile(
    string Id, string Nama,
    IReadOnlyList<DisciplineDef> Disiplin,
    IReadOnlyList<SheetTypeDef> Tipe,
    IReadOnlyList<DrawingKindDef> Jenis,
    IReadOnlyDictionary<string, string> Singkatan)
{
    /// <summary>Primary discipline by its first letter; null when unknown.</summary>
    public DisciplineDef? FindDiscipline(string code) =>
        string.IsNullOrEmpty(code) ? null : Disiplin.FirstOrDefault(d => d.Code == code[..1]);

    public DrawingKindDef? FindKind(string? id) => id is null ? null : Jenis.FirstOrDefault(k => k.Id == id);

    /// <summary>Kinds of a discipline group + sheet type, ordered by their sequence range.</summary>
    public IEnumerable<DrawingKindDef> KindsFor(string disc, int type) =>
        Jenis.Where(k => k.Disiplin == disc && k.Tipe == type).OrderBy(k => k.SeqFrom);
}
