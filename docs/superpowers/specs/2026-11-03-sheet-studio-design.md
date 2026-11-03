# Sheet Studio — Rancangan Plugin Revit (Buat, Urutkan, Edit Lembar)

Status: rancangan 2026-11-03, menunggu review user.
Pemisahan proyek: **plugin ini berdiri sendiri** (repo `D:\Library\AI\Plugin Revit\Sheet Studio`). Template/standar PUPR (`PUPR-AR-2021.rte`, plan v2 Fase B) adalah proyek terpisah yang dikerjakan **setelah** plugin. Plugin tidak butuh template PUPR; aturan PUPR masuk sebagai **profil penamaan** bawaan.

Menggantikan: plan v2 Fase A (Task 1–7) dan plan `2026-11-03-pengelola-lembar.md`. Isi keduanya dipindah dan diperluas di sini.

Sumber aturan: `02-penamaan-lembar-aia-ncs.md` (designator UDS, rentang PUPR), `01-kelengkapan-gambar-pupr.md` (judul KAPITAL tanpa singkatan, kertas, skala).

---

## 1. Tiga sistem

| Sistem | Fungsi | Tempat di UI |
|---|---|---|
| **Pembuatan lembar** | Buat 1 lembar (dengan Pemilih Nama interaktif) atau banyak sekaligus (batch: matriks / CSV / preset). Title block, view, viewport otomatis. | Tombol **+ Baru**, **⧉ Batch** → dialog |
| **Pengatur urutan** | Panel **Lapisan** ala Photoshop: grup disiplin › tipe, baris lembar dengan thumbnail, multi-pilih Ctrl/Shift, drag-drop, gembok, mata, pintasan keyboard. Nomor dihitung ulang dari urutan. | Panel kiri |
| **Edit lembar** | **Inspektur** untuk lembar terpilih (satu atau banyak): ganti disiplin/tipe/nomor, judul (cari-ganti, awalan/akhiran, kapital), parameter kop, title block, duplikat, hapus. | Panel kanan |

Semua perubahan **bertahap (staged)**: tampil langsung di panel (nomor baru, coret merah, badge), punya **Undo/Redo panel** (Ctrl+Z / Ctrl+Y), dan baru masuk ke model saat **Terapkan** — satu `TransactionGroup` = satu Ctrl+Z di Revit.

## 2. Tata letak jendela

Dockable pane "Sheet Studio" (bisa juga jendela mengambang), lebar minimum 720 px.

```
┌ SHEET STUDIO ─────────────────────────────────────────────────────────────────┐
│ [+ Baru] [⧉ Batch] [⟳] [🔍 cari lembar…      ]  Penomoran: (•)Rentang ( )Urut │
├─────────────── LAPISAN ───────────────────────┬──────── INSPEKTUR ────────────┤
│ 👁 🔒 ▾ G  UMUM                          4    │ 3 lembar dipilih              │
│ 👁    ▾   0  GENERAL                     4    │ ── NOMOR ──────────────────── │
│ 👁       ▣ G-000  SAMPUL                       │ Disiplin  [ A ▾] Arsitektur  │
│ 👁       ▣ G-001  DAFTAR GAMBAR                │ Tipe  0 ─ 1 ─●─ 3 ─ … ─ 9    │
│ 👁 🔒 ▸ S  STRUKTUR                      12    │       Denah (pandangan horiz.)│
│ 👁    ▾ A  ARSITEKTUR                    38    │ Urut  [Otomatis ✓]           │
│ 👁    ▾   1  DENAH                       14    │ ── JUDUL ──────────────────── │
│ 👁  ┃    ▣ A-110 DENAH BASEMENT 1   ← A-111   │ [<berbeda>                 ] │
│ 👁  ┃    ▣ A-111 DENAH LANTAI 1     ← A-110   │ Cari [LT ] Ganti [LANTAI]    │
│ 👁 🔒┃    ▣ A-112 DENAH LANTAI 2              │ Awalan [   ] Akhiran [   ]   │
│ 👁       ▣ A-145 RENCANA PLAFON LANTAI 1  ⚠    │ ── KOP ───────────────────── │
│     ...                                       │ Digambar [ <berbeda> ]       │
│ (baris terpilih = sorot biru, ┃ = sedang drag)│ Title block [PUPR_KOP_A3 ▾]  │
│                                               │ [Duplikat ▾] [Hapus]         │
├───────────────────────────────────────────────┴──────────────────────────────┤
│ ● 5 perubahan tertunda  [↶][↷]  [Pratinjau daftar]  [Buang]  [ TERAPKAN ]    │
└──────────────────────────────────────────────────────────────────────────────┘
```

Tema mengikuti Revit (`UIThemeManager.CurrentTheme`: terang/gelap). Daftar memakai virtualisasi (lancar ≥ 1000 lembar).

## 3. Panel Lapisan (pengatur urutan) — ala Photoshop

### 3.1 Struktur baris
Daftar **datar** berindentasi (bukan TreeView) supaya multi-pilih & drag banyak baris berjalan mulus:
- **Baris grup Disiplin** (level 0): mata, gembok, panah lipat, kode + nama, jumlah lembar.
- **Baris grup Tipe** (level 1): mata, gembok, panah, digit + nama tipe, jumlah.
- **Baris lembar** (level 2): mata, gembok, thumbnail 48×34 (opsional, dimuat lambat), nomor, judul, badge.

Badge lembar: `← A-111` (nomor akan berubah; tooltip lama→baru), `⚠` (judul bersingkatan/huruf kecil/nomor tidak valid), `✎` (judul/parameter diubah), coret merah (ditandai hapus), `★` (baru dibuat di sesi ini — sorotan sementara), `🔗` (dipinjam user lain — workshared, otomatis terkunci).

### 3.2 Interaksi

| Aksi | Hasil |
|---|---|
| Klik | Pilih satu baris |
| **Ctrl+klik** | Tambah/lepas baris dari pilihan |
| **Shift+klik** | Pilih rentang dari jangkar |
| Ctrl+Shift+klik | Tambah rentang ke pilihan |
| Ctrl+A | Pilih semua lembar yang terlihat (hasil filter) |
| Klik baris grup | Pilih semua lembar di grup |
| Esc | Kosongkan pilihan |
| **Drag** pilihan, lepas **di antara** baris lembar | Pindahkan semua lembar terpilih ke posisi itu, urutan relatif tetap |
| Drag, lepas **di atas** baris grup Tipe/Disiplin lain | Pindah ke grup itu = **ganti tipe/disiplin** (nomor baru; badge); ditolak dengan tooltip alasan jika rentang penuh |
| Drag baris grup Disiplin | Ubah urutan set (sort key), nomor tetap; jika keluar urutan UDS → peringatan kuning |
| Ctrl+] / Ctrl+[ | Naik/turun satu posisi |
| Ctrl+Shift+] / Ctrl+Shift+[ | Ke paling atas / bawah grupnya |
| F2 / klik-ganda teks judul | Ubah judul langsung di baris |
| Klik-ganda thumbnail/nomor | Buka lembar di Revit |
| Delete | Tandai hapus (bertahap) |
| Ctrl+D | Duplikat (lembar saja) |
| Ctrl+Z / Ctrl+Y | Undo/redo perubahan bertahap di panel |
| Klik 🔒 | Kunci: lembar tidak dinomori ulang & tidak bisa di-drag; di grup = kunci semua anak |
| Klik 👁 | "Muncul di daftar gambar" (`Appears In Sheet List`) on/off |
| Alt+klik panah | Lipat/buka semua grup |
| Klik kanan | Menu: Ubah judul, Ubah nomor…, Pindah ke disiplin ▸, Pindah ke tipe ▸, Duplikat ▸, Kunci, Sembunyikan dari daftar, Buka, Hapus |
| Ketik di 🔍 | Filter langsung (nomor/judul/parameter); grup tanpa hasil disembunyikan |

Baris terkunci & baris grup diabaikan saat ikut terpilih dalam drag; panel memberi tahu "2 lembar terkunci tidak dipindah".

### 3.3 Penomoran dari urutan
- **Rentang** (default): tiap jenis gambar punya rentang di profil (mis. Denah A-1 10–39). Urutan di panel menentukan siapa dapat nomor kecil dulu dalam rentangnya. Lembar tanpa jenis memakai rentang tipe penuh 01–99.
- **Urut**: per grup Tipe, nomor = `Mulai + indeks × Langkah` (Mulai 1; G mulai 0; Langkah 1/5/10), lewati nomor terkunci.
- Lembar terkunci dan lembar yang nomornya bukan UDS (tidak dikenali) = "terpakai", tidak disentuh.
- Nomor dihitung ulang **langsung** setiap perubahan urutan (pratinjau), diterapkan saat **Terapkan** via dua tahap (`~TMP-001` dulu, lalu final).

## 4. Pemilih Nama interaktif (dipakai saat Buat & Edit)

```
┌ NOMOR & NAMA LEMBAR ────────────────────────────────────────────────┐
│ DISIPLIN  (profil: PUPR ▾)                     [ tampilkan semua ]   │
│ ┌───┐┌───┐┌───┐┌───┐┌───┐┌───┐┌───┐┌───┐                              │
│ │ G ││ C ││ L ││ S ││▐A▌││ I ││ M ││ E │ …                            │
│ │Umum│Sipil│Lans│Strk│Arst│Intr│Mek │Elek│                             │
│ └───┘└───┘└───┘└───┘└───┘└───┘└───┘└───┘                              │
│ Arsitektur — gambar arsitektur: denah, tampak, potongan, detail.     │
│ Huruf ke-2  [ —  (tanpa) ▾ ]   AS Tapak · AD Demolisi · AF Finishing │
│                                                                      │
│ TIPE LEMBAR                                                          │
│  0     1     2     3     4     5     6     7     8     9             │
│  ●─────●═════○─────○─────○─────○─────○─────○─────○─────○             │
│ 1 · DENAH — pandangan horizontal: denah, rencana plafon, rencana…    │
│                                                                      │
│ NOMOR URUT   (•) Otomatis: 12   ( ) Manual [ 12 ]                    │
│  PETA NOMOR A-1xx   ■ terpakai  □ bebas  ▣ dipilih  ▨ rentang jenis  │
│  01 02 03 04 05 06 07 08 09 10                                       │
│  11 12 13 …                                  (klik sel = pilih seq) │
│                                                                      │
│ JUDUL   Jenis [ DENAH ▾ ]  Subjek [ LANTAI 2 ▾ ] (level/arah/kode)   │
│         Bebas [                                           ]          │
│ ─────────────────────────────────────────────────────────────────── │
│   A-112   DENAH LANTAI 2                       ✓ valid               │
│   ⚠ "LT" adalah singkatan → saran: LANTAI  [Pakai saran]             │
└──────────────────────────────────────────────────────────────────────┘
```

- **Disiplin**: kartu pilihan (chip) besar, kode + nama singkat; tooltip & baris keterangan berisi deskripsi lengkap (nama Indonesia + Inggris + isi). Default hanya disiplin "umum di profil"; "tampilkan semua" membuka 21 kode. Panah ←/→ menggeser pilihan.
- **Huruf ke-2**: combobox opsional, isi dari profil, tiap item berketerangan.
- **Tipe lembar**: **slider 0–9 berdetik**, snap ke angka, label di bawah tiap detik; kartu keterangan berubah langsung; alternatif: klik label / scroll mouse / panah keyboard.
- **Nomor urut**: Otomatis (berikutnya yang bebas di rentang jenis) atau Manual (spinner 01–99). **Peta nomor** 10×10 interaktif menampilkan terpakai/bebas/rentang jenis; klik sel bebas = pilih nomor.
- **Judul**: jenis gambar (daftar disaring sesuai disiplin+tipe dari profil, mis. tipe 1 → Denah, Rencana Plafon, …) + subjek (daftar level / arah TAMPAK DEPAN… / kode potongan A-A) atau teks bebas. Selalu dikapitalkan; singkatan dikenal diberi saran.
- **Pratinjau** besar nomor + judul + status validasi (hijau/kuning/merah). Tombol OK nonaktif bila merah (nomor dobel / format salah).
- Memilih **jenis gambar** dulu juga boleh: disiplin, tipe, dan rentang terisi otomatis dari profil (dua arah).

## 5. Pembuatan lembar

- **+ Baru** (satu lembar): Pemilih Nama + Kertas (A3/A2/A1 → title block dari preferensi, atau pilih tipe title block) + View: (a) tidak ada, (b) pilih view yang ada (daftar disaring "belum di lembar", pencarian), (c) buat view baru (denah/plafon pada level terpilih, drafting, legend, 3D) + view template + skala. Viewport di tengah area gambar title block.
- **⧉ Batch**: tabel baris (sama kolom dengan spek 03 §4) yang diisi dari matriks jenis×level, impor CSV, atau preset. Kolom tabel bisa diedit; sel nomor & judul memakai aturan Pemilih Nama (validasi per sel). Pratinjau → Jalankan (progres, Batal). Batch **langsung dieksekusi** (bukan bertahap) karena membuat elemen; setelah selesai lembar baru muncul di panel Lapisan dan disorot.
- Konflik "jenis+subjek sudah ada" → lewati (default) / perbarui.

## 6. Edit lembar (Inspektur)

Untuk 1 atau banyak lembar terpilih. Nilai yang berbeda antar lembar tampil `<berbeda>`; mengisi nilai = berlaku ke semua terpilih.

| Bagian | Kontrol | Multi-pilih |
|---|---|---|
| Nomor | Disiplin (chip ringkas/combobox), Tipe (slider mini), Urut (manual hanya jika 1 lembar) | ya (disiplin/tipe) |
| Judul | Teks; Cari–Ganti (opsi kata utuh); Awalan/Akhiran; KAPITALKAN; Terapkan saran singkatan | ya |
| Kop | Parameter lembar dari profil (Digambar, Diperiksa, Tanggal, Skala, Status, + parameter teks lain yang ada) | ya |
| Title block | Ganti tipe title block | ya |
| Tampilan | Muncul di daftar gambar | ya |
| Aksi | Duplikat ▸ (lembar saja / + view duplikat / + view duplikat dengan detailing), Hapus | ya |

Semua edit = perubahan bertahap (kecuali Duplikat yang langsung dieksekusi karena membuat elemen).

## 7. Profil penamaan & preferensi

- **Profil** (JSON, embedded + bisa impor): daftar disiplin (kode, nama ID/EN, keterangan, `Umum`), huruf ke-2 per disiplin, 10 tipe (nama, keterangan), jenis gambar (nama, disiplin, tipe, rentang, templat judul, apakah per-level, jenis view, skala default, view template), singkatan → kepanjangan. Profil bawaan: **PUPR** (spek 02 §5) dan **UDS umum** (tanpa rentang jenis).
- **Preferensi**: profil aktif, strategi penomoran, Mulai, Langkah, title block per kertas, kertas default, thumbnail on/off, konfirmasi sebelum terapkan, mode galat batch. Disimpan: bawaan < `%AppData%\SheetStudio\preferensi.json` < proyek (Extensible Storage).
- **Status proyek** (Extensible Storage): urutan kustom lembar (UniqueId berurutan per grup), urutan grup disiplin, kunci.

## 8. Arsitektur

- `SheetStudio.Core` (net8.0, tanpa Revit): profil, mesin penamaan, model panel lapisan & pilihan, change set + undo, sequencer, perencana buat/batch/edit, ViewModel (MVVM, tanpa tipe WPF).
- `SheetStudio.Revit` (net8.0-windows, WPF): adapter snapshot/terapkan/buat, Extensible Storage, ExternalEvent, XAML (View) + konverter, pintasan keyboard, drag-drop (GongSolutions.WPF.DragDrop), thumbnail.
- Alur: **Snapshot** (baca model) → ViewModel mengubah **ChangeSet** (bertahap, undo) → **Pratinjau** → **Terapkan** (ExternalEvent → TransactionGroup → hasil per lembar) → snapshot ulang.
- Konflik: setiap lembar di snapshot membawa `Versi` (nomor+judul saat dibaca). Saat Terapkan, jika lembar di model sudah berubah/hilang → lembar itu dilewati & dilaporkan. Perubahan model dari luar (DocumentChanged) memicu "segarkan" yang **mempertahankan** perubahan bertahap yang masih berlaku.

## 9. Di luar cakupan
Revisi (cloud/tabel revisi), ekspor PDF/DWG, sinkron ke Excel dua arah, banyak dokumen sekaligus, template PUPR (proyek terpisah).
