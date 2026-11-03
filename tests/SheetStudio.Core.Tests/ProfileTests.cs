using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.Tests;

public class ProfileTests
{
    [Fact]
    public void Pupr_Has21DisciplinesInNcsOrder()
    {
        var p = ProfileLoader.LoadBuiltIn("pupr");
        var codes = string.Concat(p.Disiplin.OrderBy(d => d.Order).Select(d => d.Code));
        Assert.Equal("GHVBCLSAIQFPDMEWTRXZO", codes);
    }

    [Fact]
    public void Pupr_Has10Types()
    {
        var p = ProfileLoader.LoadBuiltIn("pupr");
        Assert.Equal(Enumerable.Range(0, 10), p.Tipe.Select(t => t.Digit));
        Assert.Equal("Large-Scale Views", p.Tipe[4].Nama);
    }

    [Fact]
    public void Pupr_KindsFor_A1_ContainsDenahFirst()
    {
        var p = ProfileLoader.LoadBuiltIn("pupr");
        var kinds = p.KindsFor("A", 1).ToList();
        Assert.Equal("blockplan", kinds.First().Id);
        var denah = kinds.Single(k => k.Id == "denah");
        Assert.Equal((10, 39), (denah.SeqFrom, denah.SeqTo));
    }

    [Fact]
    public void Pupr_KindRangesDoNotOverlap()
    {
        var p = ProfileLoader.LoadBuiltIn("pupr");
        foreach (var g in p.Jenis.GroupBy(k => (k.Disiplin, k.Tipe)))
        {
            var list = g.OrderBy(k => k.SeqFrom).ToList();
            for (var i = 1; i < list.Count; i++)
                Assert.True(list[i].SeqFrom > list[i - 1].SeqTo, $"{list[i - 1].Id} beririsan dengan {list[i].Id}");
        }
    }

    [Fact]
    public void Pupr_FindDiscipline_And_SecondLetters()
    {
        var p = ProfileLoader.LoadBuiltIn("pupr");
        Assert.Equal("Arsitektur", p.FindDiscipline("A")!.NamaId);
        Assert.Null(p.FindDiscipline("K"));
        Assert.Contains(p.FindDiscipline("E")!.HurufKedua, h => h.Huruf == 'P');
    }

    [Fact]
    public void Uds_HasNoKinds()
    {
        var p = ProfileLoader.LoadBuiltIn("uds");
        Assert.Empty(p.Jenis);
        Assert.Equal(21, p.Disiplin.Count);
        Assert.Equal(new[] { "pupr", "uds" }, ProfileLoader.BuiltInIds);
    }

    [Fact]
    public void LoadFromJson_MissingField_ThrowsWithFieldName()
    {
        var ex = Assert.Throws<InvalidDataException>(() => ProfileLoader.LoadFromJson("""{"Id":"x","Nama":"X","Tipe":[],"Jenis":[],"Singkatan":{}}"""));
        Assert.Contains("Disiplin", ex.Message);
    }
}
