using System.Text.RegularExpressions;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Naming;

public sealed record ParseResult(bool Ok, SheetId Id, string? Error, string? Normalized);

/// <summary>UDS sheet number: discipline (1–2 letters), type digit, two-digit sequence.</summary>
public readonly partial record struct SheetId(string Discipline, int Type, int Seq)
{
    public string Format() =>
        Discipline.Length == 1 ? $"{Discipline}-{Type}{Seq:00}" : $"{Discipline}{Type}{Seq:00}";

    public override string ToString() => Format();

    public static ParseResult Parse(string? text, NamingProfile p)
    {
        if (string.IsNullOrWhiteSpace(text)) return Fail("Nomor kosong");
        var t = text.Trim().ToUpperInvariant();
        if (OneLetterNoHyphen().IsMatch(t)) return Fail("Disiplin satu huruf wajib tanda '-' (mis. A-101)");
        var m = Uds().Match(t);
        if (!m.Success) return Fail("Format salah: pakai AA-NNN, mis. A-101 atau EP110");
        var first = m.Groups[1].Value;
        if (p.FindDiscipline(first) is null) return Fail($"Disiplin '{first}' tidak dikenal di profil {p.Id}");
        var disc = m.Groups[2].Value == "-" ? first : first + m.Groups[2].Value;
        var id = new SheetId(disc, m.Groups[3].Value[0] - '0', int.Parse(m.Groups[4].Value));
        if (id.Seq == 0 && !(disc == "G" && id.Type == 0)) return Fail("Sequence 00 hanya untuk G-000");
        return new ParseResult(true, id, null, id.Format());
    }

    private static ParseResult Fail(string error) => new(false, default, error, null);

    [GeneratedRegex(@"^([A-Z])(-|[A-Z])(\d)(\d{2})$")]
    private static partial Regex Uds();

    [GeneratedRegex(@"^[A-Z]\d{3}$")]
    private static partial Regex OneLetterNoHyphen();
}
