# Sheet Studio — Panduan Singkat

Add-in Revit 2026 untuk membuat, menomori, dan mengurutkan lembar (sheet) dengan cepat, memakai pola penomoran UDS dan profil PUPR.

## Prinsip dasar

1. **Lembar tampil sebagai daftar lapisan** (seperti layer di Photoshop). Urutan di panel = urutan nomor lembar.
2. **Perubahan ditampung dulu, belum masuk model.** Tanda `● N perubahan tertunda` muncul di bawah.
3. **TERAPKAN** menulis semua perubahan ke Revit sekaligus, sebagai **satu langkah Undo**. **Buang** membatalkan semuanya.
4. **Nomor mengikuti aturan.** Disiplin + tipe + urutan, contoh `A-110` = Arsitektur, tipe 1 (denah), urutan 10.

## Memulai

1. Buka proyek Revit, tampilkan panel **Sheet Studio**.
2. Buka **⚙ Preferensi** sekali di awal: pilih kop (title block) untuk tiap ukuran kertas A3 / A2 / A1.
3. Tekan **⟳** (atau F5) bila model berubah dari luar panel.

## Membuat lembar

### Satu lembar — `＋ Baru`

1. Pilih **disiplin** (A, S, M, E, …) dan **tipe** lembar (geser slider 0–9).
2. Nomor urut terisi otomatis. Mau manual? Pilih **Manual** atau klik sel bebas di peta nomor.
3. Ketik **judul**, misalnya `DENAH LANTAI 1`.
   - Tekan **💾 Simpan** bila judul ingin dipakai lagi (opsional).
   - Judul tersimpan muncul di daftar pilihan; **Hapus dari daftar** untuk membuangnya.
4. **Pilih kertas (A3/A2/A1) dan kop.** Wajib. Bila lupa, muncul peringatan.
5. Pilih view: **Tanpa**, **Pakai yang ada**, atau **Buat baru**.
6. Tekan **BUAT LEMBAR**.

### Banyak lembar — `⧉ Batch`

1. Centang **jenis gambar** dan **level**, lalu tekan **Isi dari matriks**. Atau pilih **Preset**, atau **Impor CSV**.
2. Tekan **Pratinjau** dan cek status tiap baris:
   - hijau = baru
   - abu = sudah ada
   - kuning = ganda
   - merah = galat
3. Tekan **JALANKAN**. **Hentikan** membatalkan di tengah jalan, dan model kembali seperti semula.
4. Setelah selesai, tombol menjadi **✓ Selesai — kembali ke Revit**.

## Mengatur lembar yang sudah ada

- **Urutkan:** seret lapisan, atau `Ctrl+]` / `Ctrl+[` (naik/turun), `Ctrl+Shift+]` / `[` (paling atas/bawah).
- **Pilih banyak:** `Ctrl` / `Shift` + klik, atau `Ctrl+A`. `Esc` membatalkan pilihan.
- **Ubah nama:** `F2`. **Buka lembar di Revit:** `Enter`.
- **Duplikat:** `Ctrl+D` (lembar saja), atau menu **Duplikat ▾** di Inspektur.
- **Hapus:** `Delete` atau **Tandai hapus**. Baru terhapus setelah TERAPKAN.
- **Inspektur** (kanan atau bawah) mengubah lembar terpilih: nomor, judul, kop, parameter kop, dan tampil di daftar gambar.
- **⇅ Rapikan nomor** menghitung ulang semua nomor dari urutan panel. Pakai hanya bila perlu.

## Mode penomoran

- **Rentang:** nomor diambil dari rentang jenis gambar (denah di A-110…139).
- **Urut:** nomor berurutan per tipe.

Ganti lewat tombol **Penomoran** di toolbar.

## Pintasan

| Tombol | Fungsi |
|---|---|
| `Ctrl+Z` / `Ctrl+Y` | Urungkan / ulangi di panel |
| `F5` | Baca ulang model |
| `F2` | Ubah nama |
| `Delete` | Tandai hapus |
| `Ctrl+D` | Duplikat lembar |

## Bila ada masalah

Kirim file log: `%LocalAppData%\SheetStudio\log\sheetstudio-yyyyMMdd.log`.

Pengaturan pengguna tersimpan di `%AppData%\SheetStudio\` (`preferensi.json`, `judul-tersimpan.json`).
