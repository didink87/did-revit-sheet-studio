using System.Text;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Create;

/// <summary>CSV import/export of batch rows. Separator ';' (Excel with a comma decimal locale) or ','; quotes supported.</summary>
public static class BatchCsv
{
    public static readonly string[] Header = ["KIND", "SUBJECT", "LEVEL", "NUMBER", "TITLE", "SCALE", "PAPER", "CREATE VIEW"];

    /// <summary>Pre-1.1 Indonesian header; still accepted on import so existing files load.</summary>
    private static readonly string[] LegacyHeader = ["JENIS", "SUBJEK", "LEVEL", "NOMOR", "JUDUL", "SKALA", "KERTAS", "BUAT VIEW"];

    public static string Write(IEnumerable<BatchRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Header));
        foreach (var r in rows)
            sb.AppendLine(string.Join(';', new[]
            {
                r.KindId ?? "", r.Subjek, r.LevelName ?? "", r.NomorDiminta ?? "", r.Judul ?? "",
                r.Skala == 0 ? "" : r.Skala.ToString(), r.Kertas.ToString(), r.BuatView ? "YES" : "NO",
            }.Select(Quote)));
        return sb.ToString();
    }

    public static IReadOnlyList<BatchRow> Read(string text, NamingProfile p, out IReadOnlyList<string> errors)
    {
        var errs = new List<string>();
        var rows = new List<BatchRow>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
        {
            errors = ["Row 1: header is empty"];
            return rows;
        }
        var sep = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        var header = Split(lines[0], sep).Select(h => h.Trim().ToUpperInvariant()).ToList();
        if (!header.SequenceEqual(Header) && !header.SequenceEqual(LegacyHeader))
        {
            errors = [$"Row 1: header must be {string.Join(sep, Header)}"];
            return rows;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = Split(lines[i], sep).Concat(Enumerable.Repeat("", 8)).Take(8).Select(x => x.Trim()).ToArray();
            var line = i + 1;
            string? kind = null;
            if (f[0].Length > 0)
            {
                kind = (p.FindKind(f[0].ToLowerInvariant()) ?? p.Jenis.FirstOrDefault(k => string.Equals(k.Nama, f[0], StringComparison.OrdinalIgnoreCase)))?.Id;
                if (kind is null) { errs.Add($"Row {line}: kind '{f[0]}' not recognized"); continue; }
            }
            var skala = 0;
            if (f[5].Length > 0 && !int.TryParse(f[5], out skala)) { errs.Add($"Row {line}: scale '{f[5]}' is not a number"); continue; }
            var kertas = Kertas.A3;
            if (f[6].Length > 0 && !Enum.TryParse(f[6], true, out kertas)) { errs.Add($"Row {line}: paper '{f[6]}' must be A3, A2, or A1"); continue; }
            var view = f[7].Length == 0 || f[7].Equals("YES", StringComparison.OrdinalIgnoreCase) || f[7].Equals("YA", StringComparison.OrdinalIgnoreCase);
            rows.Add(new BatchRow(kind, f[1], Null(f[2]), Null(f[3]), Null(f[4]), skala, kertas, view));
        }
        errors = errs;
        return rows;
    }

    private static string? Null(string s) => s.Length == 0 ? null : s;

    private static string Quote(string s) =>
        s.IndexOfAny([';', ',', '"', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static List<string> Split(string line, char sep)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == sep) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }
}
