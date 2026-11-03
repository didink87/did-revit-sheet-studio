using SheetStudio.Core.Create;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;

namespace SheetStudio.Core.Tests;

public class PreferencesTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var d = Prefs.Default;
        Assert.Equal(("pupr", Strategy.Rentang, 1, 1, Kertas.A3), (d.ProfileId, d.Strategy, d.Mulai, d.Langkah, d.KertasDefault));
        Assert.Equal("PUPR_KOP_A2", d.TitleBlockPerKertas[Kertas.A2]);
        Assert.False(d.Thumbnail);
        Assert.True(d.KonfirmasiTerapkan && d.BatchLewatiGagal);
    }

    [Fact]
    public void RoundTrip()
    {
        var p = Prefs.Default with { Langkah = 10, KertasDefault = Kertas.A1 };
        Assert.True(PreferencesStore.TryDeserialize(PreferencesStore.Serialize(p), out var back, out _));
        Assert.Equal(p.Langkah, back.Langkah);
        Assert.Equal(p.KertasDefault, back.KertasDefault);
        Assert.Equal(p.TitleBlockPerKertas[Kertas.A3], back.TitleBlockPerKertas[Kertas.A3]);
    }

    [Fact]
    public void Merge_ProjectOverridesUserOverridesDefault()
    {
        var m = PreferencesStore.Merge(Prefs.Default, """{"Langkah":5,"Strategy":"Urut"}""", """{"Strategy":"Rentang"}""");
        Assert.Equal((5, Strategy.Rentang), (m.Langkah, m.Strategy));
        Assert.Equal("pupr", m.ProfileId);
    }

    [Fact]
    public void Corrupt_TryDeserializeFalse_MergeIgnoresLayer()
    {
        Assert.False(PreferencesStore.TryDeserialize("{not json", out var p, out var err));
        Assert.StartsWith("Preferensi rusak", err);
        Assert.Equal(Prefs.Default.Langkah, p.Langkah);
        Assert.Equal(1, PreferencesStore.Merge(Prefs.Default, "{oops", null).Langkah);
    }
}
