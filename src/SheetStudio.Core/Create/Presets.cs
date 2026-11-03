using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Create;

/// <summary>Built-in batch presets. "DED PUPR LENGKAP" follows the PUPR set checklist (spek 01 bab 4).</summary>
public static class Presets
{
    public static IReadOnlyList<string> Names { get; } = ["DED PUPR LENGKAP", "DENAH SEMUA LANTAI", "TAMPAK & POTONGAN"];

    private static readonly (string Kind, string Subjek)[] Ded =
    [
        ("sampul", ""), ("daftar-gambar", ""), ("legenda-simbol", ""), ("catatan-umum", ""), ("data-proyek", ""),
        ("legenda-arsitektur", ""), ("blockplan", ""), ("siteplan", ""), ("sanitasi-tapak", ""),
        ("denah", "*"), ("rencana-atap", ""), ("rencana-plafon", "*"), ("rencana-pola-lantai", "*"),
        ("rencana-pintu-jendela", "*"), ("rencana-titik-lampu", "*"), ("rencana-sanitasi", "*"),
        ("tampak-tapak", ""), ("tampak-bangunan", "DEPAN DAN BELAKANG"), ("tampak-bangunan", "KIRI DAN KANAN"),
        ("potongan-tapak", ""), ("potongan-bangunan", "A-A DAN B-B"), ("potongan-prinsip", ""),
        ("detail-toilet", ""), ("detail-pintu-jendela", ""), ("detail-tangga-ramp", "TANGGA"),
        ("schedule-pintu-jendela", ""), ("perspektif", "TAPAK"), ("perspektif", "EKSTERIOR"), ("perspektif", "INTERIOR"),
    ];

    private static readonly (string Kind, string Subjek)[] TampakPotongan =
    [
        ("tampak-bangunan", "DEPAN DAN BELAKANG"), ("tampak-bangunan", "KIRI DAN KANAN"),
        ("potongan-bangunan", "A-A"), ("potongan-bangunan", "B-B"),
    ];

    public static IReadOnlyList<BatchRow> Get(string name, IEnumerable<(string Name, double Elevation)> levels, NamingProfile p, Kertas k)
    {
        var lv = levels.ToList();
        return name switch
        {
            "DED PUPR LENGKAP" => Expand(Ded, lv, p, k),
            "DENAH SEMUA LANTAI" => BatchMatrix.Build(["denah"], lv, p, k, true),
            "TAMPAK & POTONGAN" => Expand(TampakPotongan, lv, p, k),
            _ => throw new ArgumentException($"Preset '{name}' tidak ada", nameof(name)),
        };
    }

    private static IReadOnlyList<BatchRow> Expand((string Kind, string Subjek)[] items, List<(string, double)> levels, NamingProfile p, Kertas k) =>
        items.SelectMany(i => i.Subjek == "*"
            ? BatchMatrix.Build([i.Kind], levels, p, k, true)
            : [new BatchRow(i.Kind, i.Subjek, null, null, null, 0, k, true)]).ToList();
}
