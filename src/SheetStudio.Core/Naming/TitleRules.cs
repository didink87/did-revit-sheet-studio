using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Naming;

public enum Severity { Ok, Peringatan, Galat }

public sealed record AbbrevHint(string Found, string? Suggestion);

public sealed record TitleCheck(string Normalized, Severity Severity, IReadOnlyList<AbbrevHint> Hints, string? Pesan);

/// <summary>PUPR title rules: uppercase, single spaces, no abbreviations.</summary>
public static class TitleRules
{
    private const string Vowels = "AEIOU";

    public static string Normalize(string? raw) =>
        string.Join(' ', (raw ?? "").ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static TitleCheck Check(string? raw, NamingProfile p)
    {
        var norm = Normalize(raw);
        if (norm.Length == 0) return new TitleCheck("", Severity.Galat, [], "Judul kosong");
        var hints = new List<AbbrevHint>();
        foreach (var tok in norm.Split(' '))
        {
            if (tok.Any(char.IsDigit) || tok.Contains('-')) continue;
            if (p.Singkatan.TryGetValue(tok.TrimEnd('.'), out var full)) hints.Add(new AbbrevHint(tok, full));
            else if (tok.Contains('.') || IsConsonantCluster(tok)) hints.Add(new AbbrevHint(tok, null));
        }
        return hints.Count == 0
            ? new TitleCheck(norm, Severity.Ok, hints, null)
            : new TitleCheck(norm, Severity.Peringatan, hints, "Judul tidak boleh memakai singkatan");
    }

    public static string ApplySuggestions(string title, NamingProfile p) =>
        string.Join(' ', Normalize(title).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => p.Singkatan.TryGetValue(t.TrimEnd('.'), out var full) ? full : t));

    private static bool IsConsonantCluster(string tok) =>
        tok.Length is >= 2 and <= 4 && tok.All(char.IsLetter) && !tok.Any(c => Vowels.Contains(c));
}
