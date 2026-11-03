# Penamaan Lembar Gambar — AIA / US National CAD Standard (UDS) × PUPR

Sumber: `DED AIA.pdf` (67 hlm), terutama:
- hlm 62–63 — *AIA Best Practices BP 11.02.03 "Organizing Construction Documents"* (dari *Architect's Handbook of Professional Practice* ed. 13, Okt 2005): ConDoc, NCS/UDS, urutan disiplin, sheet type designator, format nomor lembar.
- hlm 64–66 — *Archtoolbox "Construction Document Sheet Numbers and Sheet Order"* (diadaptasi dari US NCS): tabel discipline designator, sheet type, sequence, contoh nomor.
- hlm 8–18 — *Architectural Drafting: Symbols and Conventions* (judul, north arrow, section mark, sheet layout, title block, notes, schedule, garis, dimensi, simbol).
- hlm 1–7 — *Construction Documentation—Drawings* (Grigsby, AIA Handbook ed. 13): proses & praproduksi.
- hlm 19–61 — *Construction Drawing Practices* (IC PolyU, 2009): materi menggambar manual umum.

> **[PDF hlm N]** = isi dokumen. **[KEPUTUSAN]** = usulan penerapan untuk template PUPR (perlu disetujui). **[TAMBAHAN]** = di luar PDF.

---

## 1. Format Nomor Lembar (UDS) [PDF hlm 63–65]

```
 A A  -  N  N N
 │ │  │  │  └┴─ Sequence number: 2 digit, 01–99
 │ │  │  └──── Sheet type designator: 1 digit, 0–9
 │ │  └─────── Hyphen (pemisah); diganti huruf ke-2 bila disiplin 2 huruf
 └─┴────────── Discipline designator: 1 atau 2 huruf
```

| Komponen | Aturan |
|---|---|
| Discipline designator | 1 atau 2 huruf. Proyek besar/kompleks pakai 2 huruf untuk memecah disiplin (mis. EL lampu vs EP daya) |
| Hyphen | Memisah disiplin & sheet type. Jika disiplin 1 huruf, hyphen **mengisi posisi huruf ke-2** agar format tetap 2 karakter (A-101) |
| Sheet type | 1 angka (0–9) |
| Sequence | 2 angka 01–99; **tidak harus berurutan** — boleh disisakan celah untuk tambahan |
| Skala sistem | Proyek sedang: `A-102` = denah lantai 2. Proyek kompleks: `EP-102` = denah daya listrik lantai 2. Proyek sederhana: `A-1` = lembar pertama arsitektur |

Contoh dari PDF [hlm 65]:

| Nomor | Arti |
|---|---|
| AD107 | Arsitektur — denah demolisi, lembar ke-7 |
| A-204 | Arsitektur — tampak, lembar ke-4 |
| I-316 | Interior — potongan, lembar ke-16 |
| QH601 | Peralatan rumah sakit — schedule, lembar ke-1 |
| FA601 | Fire alarm — diagram |
| P-102 | Plumbing — denah lantai, lembar ke-2 |
| MH402 | HVAC — gambar skala besar, lembar ke-2 |
| MP501 | Pipa HVAC — detail, lembar ke-1 |
| EP110 | Listrik daya — denah, lembar ke-10 |
| EL103 | Listrik penerangan — denah, lembar ke-3 |
| T-505 | Telekomunikasi — detail, lembar ke-5 |
| RA102 | Resource (eksisting) arsitektur — denah bangunan eksisting, lembar ke-2 |

## 2. Discipline Designator & Urutan Set [PDF hlm 62–63, 66]

Set diurutkan: **Cover sheet → subset disiplin** sesuai urutan di bawah (urutan alami konstruksi; disiplin bertopik mirip berdekatan). Tidak semua subset dipakai.

| Urut | Kode | Disiplin (EN) | Disiplin (ID) | Keterangan |
|---|---|---|---|---|
| 0 | — | Cover Sheet | Sampul | Sebelum semua subset |
| 1 | G | General | Umum | Daftar lembar, simbol, ringkasan peraturan |
| 2 | H | Hazardous Materials | Bahan Berbahaya | Abatement, penanganan |
| 3 | V | Survey / Mapping | Survei / Pemetaan | |
| 4 | B | Geotechnical | Geoteknik | |
| 5 | C | Civil | Sipil | |
| 6 | L | Landscape | Lansekap | |
| 7 | S | Structural | Struktur | |
| 8 | A | Architectural | Arsitektur | |
| 9 | I | Interiors | Interior | |
| 10 | Q | Equipment | Peralatan | |
| 11 | F | Fire Protection | Proteksi Kebakaran | |
| 12 | P | Plumbing | Plambing | |
| 13 | D | Process | Proses | |
| 14 | M | Mechanical | Mekanikal | |
| 15 | E | Electrical | Elektrikal | |
| 16 | W | Distributed Energy | Energi Terdistribusi | |
| 17 | T | Telecommunications | Telekomunikasi | |
| 18 | R | Resource | Kondisi Eksisting | Kondisi/bangunan eksisting |
| 19 | X | Other Disciplines | Disiplin Lain | |
| 20 | Z | Contractor / Shop Drawings | Kontraktor / Shop Drawing | |
| 21 | O | Operations | Operasional | |

**Konflik di PDF:** chart AIA 2005 (hlm 63) memakai **W = Civil Works** dan meletakkannya di antara B dan C; tabel Archtoolbox (hlm 66, NCS lebih baru) memakai **W = Distributed Energy** di antara E dan T. **[KEPUTUSAN]** pakai versi NCS baru (tabel di atas); W jarang dipakai di proyek PUPR.

Huruf kedua: "dapat dilihat di US National CAD Standard" [hlm 64]. Yang muncul di PDF: **AD** (arsitektur demolisi), **EP** (listrik daya), **EL** (listrik penerangan), **MH** (HVAC), **MP** (pipa mekanikal), **QH** (peralatan RS), **FA** (fire alarm), **RA** (eksisting arsitektur).
**[TAMBAHAN — cek ke NCS sebelum dipakai]** huruf kedua arsitektur yang umum: AS (tapak arsitektur), AD (demolisi), AE (elemen), AI (interior), AF (finishing), AG (grafis/signage).

ConDoc (pendahulu UDS) [hlm 62]: G; Sitework TS, SB, SD, C, L; Major A (A000 schedule/keynote/catatan umum, A100 denah, A200 tampak & potongan melintang, A300 sirkulasi vertikal/core/detail, A400 RCP & detail, A500 selubung eksterior & detail, A600 interior), S, M, P, FP, E; Special ID, FS, SG, FF, AA. → **tidak dipakai**, hanya referensi; UDS menggantikannya.

## 3. Sheet Type Designator [PDF hlm 63, 66]

Dalam satu disiplin, lembar diurut **dari umum ke khusus**.

| Kode | Nama | Isi |
|---|---|---|
| 0 | General | Legenda simbol, singkatan, catatan umum |
| 1 | Plans | Pandangan horizontal (denah) |
| 2 | Elevations | Pandangan vertikal (tampak) |
| 3 | Sections | Potongan, wall section |
| 4 | Large-Scale Views | Denah/tampak/potongan skala besar, potongan tangga — **bukan detail** |
| 5 | Details | Detail |
| 6 | Schedules & Diagrams | Schedule & diagram |
| 7 | User Defined | Tipe lain di luar kategori, termasuk lembar detail tipikal |
| 8 | User Defined | Tipe lain |
| 9 | 3D Representations | Isometri, perspektif, foto |

## 4. Konvensi AIA lain yang relevan [PDF hlm 8–18]

| Topik | Isi AIA | Vs PUPR → **[KEPUTUSAN]** |
|---|---|---|
| Judul | Semua entitas (denah, tampak, detail, kolom catatan, legenda) wajib berjudul; skala angka di bawah judul | Sama. PUPR menambah skala batang → pakai PUPR |
| North arrow | Lingkaran ±1 inci (25 mm); di **pojok kiri atas** lembar (sheet layout) | PUPR: dekat denah. Pakai dekat denah; pojok kiri atas jika satu denah per lembar |
| Section mark | Lingkaran 5/8" (±16 mm); panah 45° bersinggungan; atas = nama potongan, bawah = no. lembar; **teks selalu horizontal**; garis potong boleh dihilangkan, cukup panah; boleh satu lingkaran + panah untuk potongan parsial | Sama dengan PUPR "C / A-9" → tambah aturan teks horizontal & varian parsial |
| Sheet layout | 3 area: area gambar, area kop, area data produksi. Gambar utama dominan, detail & catatan di sekitarnya; judul di **bawah** gambar | Sama |
| Title block | Blok: identifikasi desainer, identifikasi proyek, issue (penerbitan/revisi), manajemen, judul lembar, identifikasi lembar (nomor). Format teks horizontal (disarankan) atau vertikal | PUPR kop vertikal kanan → isi kolom PUPR + pastikan 6 blok AIA ada |
| Catatan | Blok catatan di **kolom paling kanan** area gambar. 5 jenis: catatan umum (seluruh pekerjaan, cukup sekali), catatan umum disiplin (mis. di P-001), catatan lembar, keynote referensi, keynote lembar. Hindari singkatan | Pakai: catatan umum di G-004, catatan disiplin di X-001 (A-001, S-001, …) |
| Schedule | Judul + min. 3 kolom; 4 bagian: heading, kolom mark, kolom deskripsi, kolom ciri pembeda; muat dalam modul lembar | Sama |
| Garis | Hidden: dash 1/8" spasi 1/16", tipis; property line: tebal, putus ganda 3/16" spasi 1/8", sudut properti lingkaran 3/16"; center: tipis, dash 5/64" spasi 5/64"; dimensi, arsir, match line, break line: tipis | PUPR tebal garis tetap; pola garis pakai ukuran AIA (dalam mm: 3.2/1.6, 4.8/3.2, 2/2) |
| Arrowhead | Open/closed/solid/slash; panjang = tinggi teks; rasio panjang:lebar 3:1; **jangan dicampur** | Sama (PUPR: konsisten) |
| Dimensi | Garis menerus, angka **di atas** garis (tidak di bawah); sistem aligned; garis dimensi pertama ≥ 1/2" (12.7 mm) dari objek, berikutnya ≥ 3/8" (9.5 mm); teks 3/32" (2.4 mm) | PUPR: ±10 mm & teks 3 mm → pakai PUPR (pertama 10 mm, antar baris 8 mm) |
| Simbol | Room identifier (nama+nomor), window identifier, FF&E indicator, detail indicator (panah 45°, lingkaran putus, persegi putus) — garis sedang, teks 2.5 mm | Tambah tag detail lingkaran putus & persegi putus |
| Praproduksi | Konfirmasi format klien: ukuran lembar, layout, urutan, **penomoran**, simbol, singkatan | → alasan sistem penamaan dibuat di template |

## 5. Pemetaan Gambar PUPR → Nomor UDS [KEPUTUSAN]

Mode default **satu huruf** (proyek PUPR umumnya sedang). Rentang sequence di bawah = default, bisa diubah di `spec/pupr-spec.json`. Lembar kondisional diberi nomor hanya jika dibuat.

| Jenis gambar PUPR | Disiplin | Tipe | Rentang seq | Contoh nomor | Contoh judul lembar |
|---|---|---|---|---|---|
| Sampul (pengecualian: UDS tidak memberi nomor ke cover, tapi Revit wajib nomor unik → seq 00) | G | 0 | 00 | G-000 | SAMPUL |
| Daftar gambar | G | 0 | 01 | G-001 | DAFTAR GAMBAR |
| Legenda simbol & singkatan (semua disiplin) | G | 0 | 02–03 | G-002 | LEGENDA SIMBOL DAN SINGKATAN |
| Catatan umum | G | 0 | 04 | G-004 | CATATAN UMUM |
| Data proyek & intensitas (KDB/KLB/KDH/GSB) | G | 0 | 05 | G-005 | DATA PROYEK DAN INTENSITAS BANGUNAN |
| Legenda & catatan arsitektur, legenda material | A | 0 | 01–09 | A-001 | LEGENDA DAN CATATAN UMUM ARSITEKTUR |
| Block plan | A | 1 | 01–02 | A-101 | RENCANA MASSA BANGUNAN |
| Site plan | A | 1 | 03–05 | A-103 | RENCANA TAPAK |
| Rencana sanitasi tapak | A | 1 | 06–09 | A-106 | RENCANA SANITASI TAPAK |
| Denah basement → lantai dasar → lantai n → denah atap | A | 1 | 10–39 | A-110 BASEMENT 1, A-111 LANTAI 1 | DENAH LANTAI 1 |
| Rencana atap | A | 1 | 40–44 | A-140 | RENCANA ATAP |
| Rencana plafon (per lantai) | A | 1 | 45–54 | A-145 | RENCANA PLAFON LANTAI 1 |
| Rencana lantai/pola (per lantai) | A | 1 | 55–64 | A-155 | RENCANA POLA LANTAI LANTAI 1 |
| Rencana finishing dinding | A | 1 | 65–69 | A-165 | RENCANA FINISHING DINDING LANTAI 1 |
| Rencana pintu & jendela | A | 1 | 70–79 | A-170 | RENCANA PINTU DAN JENDELA LANTAI 1 |
| Rencana titik lampu (mode MEP-di-AR) | A | 1 | 80–89 | A-180 | RENCANA TITIK LAMPU LANTAI 1 |
| Rencana sanitasi bangunan (mode MEP-di-AR) | A | 1 | 90–99 | A-190 | RENCANA SANITASI LANTAI 1 |
| Tampak tapak | A | 2 | 01–09 | A-201 | TAMPAK TAPAK |
| Tampak bangunan (4 arah) | A | 2 | 10–49 | A-210 | TAMPAK DEPAN DAN TAMPAK BELAKANG |
| Potongan tapak | A | 3 | 01–09 | A-301 | POTONGAN TAPAK |
| Potongan bangunan | A | 3 | 10–49 | A-310 | POTONGAN A-A DAN POTONGAN B-B |
| Potongan prinsip | A | 3 | 50–79 | A-350 | POTONGAN PRINSIP |
| Denah & potongan toilet skala besar | A | 4 | 01–19 | A-401 | DENAH DAN POTONGAN TOILET |
| Ruang khusus | A | 4 | 20–39 | A-420 | DETAIL RUANG AUDITORIUM |
| Tangga / ramp (denah & potongan skala besar) | A | 4 | 40–59 | A-440 | DENAH DAN POTONGAN TANGGA 1 |
| Core lift (+keyplan) | A | 4 | 60–79 | A-460 | DENAH DAN POTONGAN CORE LIFT |
| Detail pintu & jendela | A | 5 | 01–29 | A-501 | DETAIL PINTU P1 DAN P2 |
| Detail tangga / ramp (1:10–1:5) | A | 5 | 30–49 | A-530 | DETAIL TANGGA |
| Detail lainnya (fasad, ornamen, papan nama, penanaman) | A | 5 | 50–99 | A-550 | DETAIL FASAD |
| Schedule pintu & jendela | A | 6 | 01–09 | A-601 | SCHEDULE PINTU DAN JENDELA |
| Schedule finishing ruang | A | 6 | 10–19 | A-610 | SCHEDULE FINISHING RUANG |
| Wiring diagram grup lampu (mode MEP-di-AR) | A | 6 | 20–29 | A-620 | DIAGRAM PENGKABELAN PENERANGAN |
| Perspektif (siteplan, eksterior, interior, entrance, lain) | A | 9 | 01–99 | A-901 | PERSPEKTIF TAPAK |
| Landscape terpisah (opsional) | L | 1/5 | — | L-101 | RENCANA LANSEKAP |
| Kondisi eksisting (opsional) | R(A) | 1 | — | RA101 / R-101 | DENAH BANGUNAN EKSISTING |

**Mode MEP terpisah** (ada konsultan MEP, sesuai catatan PUPR hlm 87, 89): titik lampu → `E-1xx` (2 huruf: `EL1xx`), stop kontak → `EP1xx`, sanitasi → `P-1xx`, wiring → `E-6xx`. **[TAMBAHAN]** struktur: `S-001` catatan, `S-1xx` denah pondasi/balok/pelat, `S-3xx` potongan, `S-5xx` detail, `S-6xx` tabel kolom/balok.

**Mode dua huruf [TAMBAHAN]**: `AS1xx` tapak, `A-1xx` denah & rencana, `AF1xx` rencana finishing/pola lantai, `AI…` interior. Dipakai bila rentang satu huruf penuh (≥ 30 lantai atau lembar per lantai banyak).

## 6. Aturan Nama View & File [KEPUTUSAN]

| Objek | Format | Contoh |
|---|---|---|
| Sheet Number | `{D}{D2\|-}{T}{SS}` | `A-111` |
| Sheet Name | KAPITAL, tanpa singkatan (PUPR) | `DENAH LANTAI 1` |
| View Name (unik di Revit) | `{D}{T} {JUDUL} {SUBJEK} 1-{SKALA}` — tanpa karakter `\ : { } [ ] \| ; < > ? \` ~` | `A1 DENAH LANTAI 1 1-100` |
| Title on Sheet | Judul KAPITAL tanpa kode & skala (skala di view title family) | `DENAH LANTAI 1` |
| Detail number (viewport) | Urut 1…n per lembar, kiri→kanan, atas→bawah | 1, 2, 3 |
| Referensi potongan/detail | `{no detail} / {no lembar}` (otomatis dari Revit) | `3 / A-310` |
| File PDF | `{KODEPROYEK}_{SheetNumber}_{SheetName}_R{rev}.pdf` | `PRJ01_A-111_DENAH LANTAI 1_R0.pdf` |
| Urut browser & daftar gambar | `{urut disiplin 2 digit}.{D}.{T}.{SS}` | `08.A.1.11` |
