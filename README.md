# Sheet Studio — Revit 2026

Plugin Revit untuk **membuat, mengurutkan, dan mengedit lembar (sheet)** dengan penomoran UDS / US National CAD Standard (Discipline Designator + Sheet Type Designator) dan profil standar PUPR.

- Rancangan: `docs/superpowers/specs/2026-11-03-sheet-studio-design.md`
- Aturan penamaan: `docs/reference/02-penamaan-lembar-aia-ncs.md`
- Plan & keputusan: `docs/superpowers/plans/2026-11-03-sheet-studio.md`

## Instalasi

Butuh **Revit 2026** (Windows). Tutup Revit sebelum memasang atau memperbarui.

### Jalur 1 — Setup sederhana (tanpa terminal)

1. Buka halaman [Releases](https://github.com/didink87/did-revit-sheet-studio/releases), unduh `SheetStudio-<versi>-Revit2026.zip`.
2. Ekstrak **seluruh** isi ZIP ke satu folder.
3. Klik-ganda **`Setup.bat`** → pilih **[1] Pasang / perbarui**.
4. Buka Revit 2026 → tab **DID** → **Sheet Studio**. Bila muncul dialog keamanan add-in, pilih **Always Load**.

Setup menyalin add-in ke `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio\` dan menulis `SheetStudio.addin`. Copot: jalankan `Setup.bat` → **[2] Copot**.

### Jalur 2 — Terminal (dari kode sumber)

Butuh [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) dan Git.

```
git clone https://github.com/didink87/did-revit-sheet-studio.git
cd did-revit-sheet-studio
dotnet build src/SheetStudio.Revit -c Release -p:InstallAddin=true
```

Menyalin `SheetStudio.addin` ke `%AppData%\Autodesk\Revit\Addins\2026\` dan menunjuk ke `bin\Release\SheetStudio.Revit.dll`. Pakai **Release**: build Debug lebih lambat (target memberi peringatan bila dipasang dari Debug). Perbarui: `git pull` lalu ulangi perintah build. Copot: hapus file `.addin` itu.

Kedua jalur menulis manifest yang sama (`SheetStudio.addin`), jadi jalur terakhir yang dijalankan yang berlaku.

### Membuat paket rilis (pengembang)

```
powershell -ExecutionPolicy Bypass -File setup/Build-Release.ps1 -Version 1.0.0
```

Hasil: `dist/SheetStudio-1.0.0-Revit2026.zip` (isi: `bin\`, `Setup.bat`, `Install-SheetStudio.ps1`, `BACA-DULU.txt`, `PANDUAN.md`).

Panduan pemakaian ringkas: [`docs/PANDUAN.md`](docs/PANDUAN.md).

## Diagnosa

Log ditulis ke `%LocalAppData%\SheetStudio\log\sheetstudio-yyyyMMdd.log` (satu file per hari). Peristiwa yang dicatat:

| Peristiwa | Isi |
|---|---|
| `startup` | Add-in dimuat |
| `load` | Pembacaan model (dengan durasi) |
| `create` | Pembuatan lembar (＋ Baru / Batch) |
| `apply` | TERAPKAN ke model |
| `thumb` / `thumb-gagal` | Thumbnail berhasil dibuat / gagal |
| `watcher` | Pemantau perubahan model |
| `event` | ExternalEvent ditolak/tidak diterima Revit (Denied/TimedOut) |
| `reload` | Baca ulang panel |

Lampirkan file log hari itu saat melaporkan masalah.

## Tiga sistem dalam satu panel

Tab ribbon **DID → panel DID → Sheet Studio** membuka/menutup panel. (Panel DID dipakai bersama Family Browser dan Face View.)

| Bagian | Fungsi |
|---|---|
| **Lapisan** (kiri) | Urutan set kek Layers Photoshop: grup Disiplin › Tipe, multi-pilih, drag-drop, gembok, mata, thumbnail |
| **Inspektur** (kanan) | Edit lembar terpilih (satu/banyak): disiplin, tipe (slider), nomor, judul, cari-ganti, awalan/akhiran, parameter kop, title block, duplikat, hapus |
| **＋ Baru / ⧉ Batch** | Pemilih nama interaktif (kartu disiplin, slider tipe, peta nomor 10×10) dan batch (matriks jenis × level, CSV, preset) |

Semua perubahan di panel **bertahap**: nomor baru tampil biru, judul berubah ✎, lembar dihapus dicoret merah. Ctrl+Z/Ctrl+Y di panel membatalkan/mengulang langkah bertahap. **TERAPKAN** menulis semuanya ke model sebagai **satu langkah Undo** Revit. Lembar yang diubah orang lain sejak dibaca dilewati dan dilaporkan.

## Pintasan keyboard (fokus di panel)

| Tombol | Aksi |
|---|---|
| Klik / Ctrl+klik / Shift+klik / Ctrl+Shift+klik | Pilih satu / tambah-lepas / rentang / tambah rentang |
| Klik baris grup | Pilih semua lembar di grup |
| Ctrl+A / Esc | Pilih semua yang terlihat / kosongkan pilihan |
| Drag di antara baris | Pindah posisi (semua yang terpilih ikut, urutan relatif tetap) |
| Drag ke baris grup Tipe/Disiplin | Pindah grup = ganti tipe/disiplin (ditolak bila rentang penuh) |
| Drag header Disiplin | Ubah urutan set (peringatan bila keluar urutan UDS) |
| Ctrl+] / Ctrl+[ | Naik / turun satu |
| Ctrl+Shift+] / Ctrl+Shift+[ | Ke paling atas / bawah grup |
| F2 / klik-ganda judul | Ubah judul di baris |
| Enter / klik-ganda nomor | Buka lembar |
| Delete | Tandai hapus |
| Ctrl+D | Duplikat (lembar saja) |
| Ctrl+Z / Ctrl+Y | Undo / redo di panel |
| F5 | Baca ulang model |
| Alt+klik panah | Lipat/buka semua grup |

## Penomoran

Format: `{Disiplin 1–2 huruf}{'-' bila 1 huruf}{Tipe 0–9}{Urut 01–99}` → `A-111`, `EP110`, `G-000` (sampul).

- **Rentang** (bawaan): tiap jenis gambar punya rentang di profil PUPR (mis. denah `A-110…139`, plafon `A-145…154`). Urutan di panel menentukan siapa dapat nomor kecil.
- **Urut**: nomor berurutan per grup tipe, mulai 1 (G mulai 0), langkah 1/5/10 (untuk menyisakan celah).
- Hanya **grup yang Anda ubah** yang dinomori ulang. Tombol **⇅ Rapikan nomor** menghitung ulang semua grup.
- Lembar **terkunci**, lembar yang dipinjam user lain (workshared), dan nomor non-UDS tidak pernah diubah.

## Profil

- `pupr` — 21 disiplin NCS + huruf ke-2 contoh AIA (AD, EP, EL, MH, MP, QH, FA, RA; AS/AE/AI/AF/AG ditandai "cek NCS"), 10 tipe, jenis gambar PUPR dengan rentang & templat judul, daftar singkatan.
- `uds` — disiplin & tipe saja, tanpa rentang jenis.
- Impor profil JSON sendiri **belum** tersedia.

## Format CSV batch

Header (pemisah `;` atau `,`):

```
JENIS;SUBJEK;LEVEL;NOMOR;JUDUL;SKALA;KERTAS;BUAT VIEW
denah;LANTAI 1;Lantai 1;;;100;A3;YA
;;;A-701;GAMBAR KHUSUS;;A3;TIDAK
```

`JENIS` = id jenis (mis. `denah`, `rencana-plafon`) atau nama jenis; kosong = judul bebas (wajib isi `NOMOR` dan `JUDUL`). `NOMOR` kosong = otomatis. Baris yang sudah ada (jenis + subjek sama) dilewati.

## Preferensi

⚙ di panel. Urutan berlaku: bawaan < `%AppData%\SheetStudio\preferensi.json` ("Jadikan default saya") < tersimpan di proyek ("Simpan untuk proyek ini", Extensible Storage). Urutan panel, kunci, dan urutan disiplin disimpan di proyek saat **TERAPKAN**.

## Batasan yang diketahui

- **Tampak & potongan** dibuat sebagai *drafting view* sementara — garis potong/tampak harus Anda buat di denah lalu ganti view-nya.
- **Legend** tidak bisa dibuat lewat API; minimal harus ada satu legend di proyek untuk diduplikat.
- **Batch** berjalan per 5 lembar per langkah agar progres & tombol Batal hidup; satu batch = beberapa langkah Undo. Batal/galat (mode "batalkan semua") menghapus lembar yang sudah dibuat.
- Revit bisa "mencuri" Ctrl+Z bila fokus keyboard tidak di dalam panel — klik daftar lembar dulu.
- Parameter `SS_JenisGambar` dan `SS_Subjek` (Identity Data) ditambahkan ke kategori Sheets saat pembuatan pertama.
- **Thumbnail mati secara bawaan** (mahal di proyek besar) — aktifkan di ⚙ Preferensi.
- Rasa interaksi (drag, tema gelap, 1000 lembar) dan "satu Undo" diuji manual, bukan otomatis.

## Pengembangan

```
dotnet test tests/SheetStudio.Core.Tests      # xUnit, tanpa Revit
dotnet test tests/SheetStudio.Revit.Tests     # ricaun: membuka & menutup Revit 2026 sendiri
```

Tes Revit tidak pernah menyentuh Revit yang sedang Anda pakai.

**Catatan:** `dotnet test tests/SheetStudio.Revit.Tests` akan menggantung (timeout ricaun) selama `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio.addin` terpasang, karena Revit tes menampilkan dialog keamanan add-in tak bertanda tangan. Ganti nama sementara menjadi `SheetStudio.addin.disabled` selama tes berjalan, lalu kembalikan.
