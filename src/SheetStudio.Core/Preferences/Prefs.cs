using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SheetStudio.Core.Create;
using SheetStudio.Core.Numbering;

namespace SheetStudio.Core.Preferences;

public sealed record Prefs(
    string ProfileId,
    Strategy Strategy,
    int Mulai,
    int Langkah,
    IReadOnlyDictionary<Kertas, string> TitleBlockPerKertas,
    Kertas KertasDefault,
    bool Thumbnail,
    bool KonfirmasiTerapkan,
    bool BatchLewatiGagal)
{
    public static Prefs Default { get; } = new("pupr", Strategy.Rentang, 1, 1,
        new Dictionary<Kertas, string> { [Kertas.A3] = "PUPR_KOP_A3", [Kertas.A2] = "PUPR_KOP_A2", [Kertas.A1] = "PUPR_KOP_A1" },
        Kertas.A3, false, true, true);

    public NumberingOptions Numbering => new(Strategy, Mulai, Langkah);
}

/// <summary>JSON persistence: built-in &lt; user file &lt; project storage, merged key by key.</summary>
public static class PreferencesStore
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() }, WriteIndented = true };

    public static string Serialize(Prefs p) => JsonSerializer.Serialize(p, Options);

    public static bool TryDeserialize(string json, out Prefs p, out string? err)
    {
        try
        {
            p = JsonSerializer.Deserialize<Prefs>(json, Options) ?? Prefs.Default;
            err = null;
            return true;
        }
        catch (JsonException ex)
        {
            p = Prefs.Default;
            err = "Preferensi rusak: " + ex.Message;
            return false;
        }
    }

    public static Prefs Merge(Prefs bawaan, string? userJson, string? projectJson)
    {
        var node = JsonNode.Parse(Serialize(bawaan))!.AsObject();
        foreach (var layer in new[] { userJson, projectJson })
        {
            if (string.IsNullOrWhiteSpace(layer)) continue;
            JsonObject? obj;
            try { obj = JsonNode.Parse(layer) as JsonObject; }
            catch (JsonException) { continue; }
            if (obj is null) continue;
            foreach (var (key, value) in obj) node[key] = value?.DeepClone();
        }
        return TryDeserialize(node.ToJsonString(), out var merged, out _) ? merged : bawaan;
    }
}
