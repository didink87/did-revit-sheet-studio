using System.Text.Json;

namespace SheetStudio.Core.Profiles;

public static class ProfileLoader
{
    private static readonly string[] Required = ["Id", "Nama", "Disiplin", "Tipe", "Jenis", "Singkatan"];

    public static IReadOnlyList<string> BuiltInIds { get; } = ["pupr", "uds"];

    public static NamingProfile LoadBuiltIn(string id)
    {
        if (!BuiltInIds.Contains(id)) throw new ArgumentException($"Built-in profile '{id}' does not exist.", nameof(id));
        using var s = typeof(ProfileLoader).Assembly.GetManifestResourceStream($"SheetStudio.Core.Profiles.{id}.json")
            ?? throw new InvalidOperationException($"Profile resource '{id}' not found.");
        using var r = new StreamReader(s);
        return LoadFromJson(r.ReadToEnd());
    }

    public static NamingProfile LoadFromJson(string json)
    {
        using (var doc = JsonDocument.Parse(json))
        {
            foreach (var key in Required)
                if (!doc.RootElement.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
                    throw new InvalidDataException($"Invalid profile: field '{key}' is missing.");
        }
        return JsonSerializer.Deserialize<NamingProfile>(json)
            ?? throw new InvalidDataException("Invalid profile: empty.");
    }
}
