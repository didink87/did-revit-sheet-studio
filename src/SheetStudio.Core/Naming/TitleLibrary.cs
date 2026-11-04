using System.Text.Json;

namespace SheetStudio.Core.Naming;

/// <summary>
/// Judul lembar yang disimpan pengguna untuk dipakai lagi (opsional, lewat tombol Simpan).
/// Disimpan per pengguna sebagai daftar JSON; path null = hanya di memori (tes).
/// </summary>
public sealed class TitleLibrary(string? filePath)
{
    private List<string>? _items;

    public IReadOnlyList<string> Items => Loaded();

    public bool Contains(string title) => Loaded().Contains(Clean(title), StringComparer.OrdinalIgnoreCase);

    /// <returns>true bila judul baru ditambahkan.</returns>
    public bool Add(string title)
    {
        var t = Clean(title);
        if (t.Length == 0 || Contains(t)) return false;
        Loaded().Add(t);
        Loaded().Sort(StringComparer.OrdinalIgnoreCase);
        Save();
        return true;
    }

    public bool Remove(string title)
    {
        var t = Clean(title);
        var removed = Loaded().RemoveAll(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) Save();
        return removed;
    }

    private static string Clean(string? s) => (s ?? "").Trim().ToUpperInvariant();

    private List<string> Loaded()
    {
        if (_items is not null) return _items;
        _items = [];
        try
        {
            if (filePath is not null && File.Exists(filePath))
                _items = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(filePath)) ?? [];
        }
        catch (Exception) { _items = []; } // file rusak: mulai kosong, jangan gagalkan dialog
        return _items;
    }

    private void Save()
    {
        if (filePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { /* gagal simpan tidak boleh menggagalkan pembuatan lembar */ }
    }
}
