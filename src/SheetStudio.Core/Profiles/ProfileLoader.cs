using System.Text.Json;

namespace SheetStudio.Core.Profiles;

public static class ProfileLoader
{
    private static readonly string[] Required = ["Id", "Nama", "Disiplin", "Tipe", "Jenis", "Singkatan"];

    public static IReadOnlyList<string> BuiltInIds { get; } = ["pupr", "uds"];

    public static NamingProfile LoadBuiltIn(string id)
    {
        if (!BuiltInIds.Contains(id)) throw new ArgumentException($"Profil bawaan '{id}' tidak ada.", nameof(id));
        using var s = typeof(ProfileLoader).Assembly.GetManifestResourceStream($"SheetStudio.Core.Profiles.{id}.json")
            ?? throw new InvalidOperationException($"Resource profil '{id}' tidak ditemukan.");
        using var r = new StreamReader(s);
        return LoadFromJson(r.ReadToEnd());
    }

    public static NamingProfile LoadFromJson(string json)
    {
        using (var doc = JsonDocument.Parse(json))
        {
            foreach (var key in Required)
                if (!doc.RootElement.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
                    throw new InvalidDataException($"Profil tidak valid: field '{key}' tidak ada.");
        }
        return JsonSerializer.Deserialize<NamingProfile>(json)
            ?? throw new InvalidDataException("Profil tidak valid: kosong.");
    }
}
