using SheetStudio.Core.Create;

namespace SheetStudio.Core.Tests;

public class BatchCsvTests
{
    [Fact]
    public void Csv_RoundTrip()
    {
        var rows = new[]
        {
            new BatchRow("denah", "LANTAI 1", "Lantai 1", null, null, 100, Kertas.A2, true),
            new BatchRow(null, "", null, "A-701", "JUDUL; DENGAN \"KUTIP\"", 0, Kertas.A3, false),
        };
        var back = BatchCsv.Read(BatchCsv.Write(rows), Profiles.Pupr, out var errors);
        Assert.Empty(errors);
        Assert.Equal(rows, back);
    }

    [Fact]
    public void Csv_Comma()
    {
        var text = "JENIS,SUBJEK,LEVEL,NOMOR,JUDUL,SKALA,KERTAS,BUAT VIEW\nDENAH,LANTAI 2,Lantai 2,,,,A1,TIDAK\n";
        var rows = BatchCsv.Read(text, Profiles.Pupr, out var errors);
        Assert.Empty(errors);
        Assert.Equal(new BatchRow("denah", "LANTAI 2", "Lantai 2", null, null, 0, Kertas.A1, false), Assert.Single(rows));
    }

    [Fact]
    public void Csv_UnknownKind_ErrorLine()
    {
        var text = "JENIS;SUBJEK;LEVEL;NOMOR;JUDUL;SKALA;KERTAS;BUAT VIEW\ndenah;L1;;;;;;\nXYZ;L2;;;;;;\n";
        var rows = BatchCsv.Read(text, Profiles.Pupr, out var errors);
        Assert.Single(rows);
        Assert.Equal("Baris 3: jenis 'XYZ' tidak dikenal", Assert.Single(errors));
    }

    [Fact]
    public void Csv_BadHeader_Error()
    {
        BatchCsv.Read("A;B\n1;2\n", Profiles.Pupr, out var errors);
        Assert.StartsWith("Baris 1:", Assert.Single(errors));
    }
}
