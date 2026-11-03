# Sheet Studio — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Plugin Revit 2026 "Sheet Studio" dengan tiga sistem — pembuatan lembar (tunggal & batch, Pemilih Nama interaktif berbasis Discipline Designator + slider Sheet Type), pengatur urutan lembar ala panel Layers Photoshop (multi-pilih Ctrl/Shift, drag-drop, gembok, mata), dan edit lembar (Inspektur multi-pilih) — dengan perubahan bertahap, undo panel, dan satu Undo di Revit.

**Architecture:** `SheetStudio.Core` (net8.0, tanpa Revit API) memuat profil penamaan, mesin nomor UDS, model panel lapisan + pilihan, ChangeSet bertahap dengan undo/redo, sequencer, perencana buat/batch/edit, dan semua ViewModel — diuji xUnit. `SheetStudio.Revit` (net8.0-windows, WPF) memuat adapter snapshot/terapkan/buat via `ExternalEvent`, Extensible Storage, dan XAML tipis di atas ViewModel — diuji NUnit di Revit nyata via ricaun. Satu arah alur: Snapshot → ChangeSet → Pratinjau → Terapkan (TransactionGroup) → Snapshot.

**Tech Stack:** C# 12, .NET 8, Revit 2026 API (Nice3point.Revit.Api.* 2026.*), WPF, gong-wpf-dragdrop 3.*, System.Text.Json, xUnit 2.5, NUnit 3.13 + ricaun.RevitTest.TestAdapter 1.11.

**Spec:** `docs/superpowers/specs/2026-11-03-sheet-studio-design.md` (rancangan, wajib dibaca) + `docs/reference/02-penamaan-lembar-aia-ncs.md` (designator & rentang PUPR) + `docs/reference/01-kelengkapan-gambar-pupr.md` (aturan judul/kertas).

## Global Constraints

- Repo baru `D:\Library\AI\Plugin Revit\Sheet Studio`, branch `feat/sheet-studio`; pola proyek sama dengan `Family Browser` (Directory.Build.props LangVersion 12, RevitVersion 2026; Core net8.0; Revit net8.0-windows UseWPF; tests Core xUnit, tests Revit NUnit+ricaun dengan `NUnit.Open/Close/Version` metadata).
- Install add-in opt-in: `dotnet build src/SheetStudio.Revit -p:InstallAddin=true`.
- Dua instance Revit: tes hanya lewat ricaun; jangan attach/tutup/kill Revit milik user; tanpa `Stop-Process` by name.
- Teks UI Bahasa Indonesia. Judul lembar KAPITAL, tanpa singkatan.
- Nomor UDS: `^([A-Z])(-|[A-Z])(\d)(\d{2})$`; disiplin 1 huruf wajib `-`; seq 01–99; `G-000` satu-satunya seq 00.
- Urutan disiplin: `GHVBCLSAIQFPDMEWTRXZO`. Tipe 0 General, 1 Plans, 2 Elevations, 3 Sections, 4 Large-Scale Views, 5 Details, 6 Schedules & Diagrams, 7 User Defined, 8 User Defined, 9 3D Representations.
- Penomoran: `Rentang` (default) & `Urut` (Mulai 1, G 0, Langkah 1/5/10). Nomor sementara `~TMP-{i:000}`.
- Semua aksi model lewat `ExternalEvent`; satu Terapkan = satu `TransactionGroup` ber-`Assimilate` bernama `"Sheet Studio: Terapkan"`.
- Preferensi: bawaan < `%AppData%\SheetStudio\preferensi.json` < proyek. Extensible Storage schema GUID `5B8F2E61-4C7A-4D19-A3E2-9F0B6C1D7A48`, nama `SheetStudioData`, field string `Preferensi` & `StatusUrutan`. Dockable pane GUID `A7D3C1E9-2B64-4F80-9E15-3C8B0D6F2A71`.
- Kinerja: bangun ulang baris panel untuk 1000 lembar < 100 ms; daftar WPF tervirtualisasi.

## Review Focus

1. **Model berubah dari luar saat ada perubahan bertahap** (user lain/Revit mengubah, menghapus, atau menambah lembar) — harapan: segarkan mempertahankan perubahan yang masih berlaku, membuang yang lembarnya hilang (dengan pesan); Terapkan melewati lembar yang `Versi`-nya berbeda dan melaporkannya. Test di Task 4 (`Rebase_*`) & Task 9 (`Apply_SkipsSheetChangedSinceSnapshot`).
2. **Drag multi-pilih berisi baris grup & lembar terkunci/dipinjam** — harapan: baris grup & terkunci tidak ikut pindah, sisanya pindah dengan urutan relatif tetap, status memberi tahu jumlah yang dilewati. Test di Task 4 (`MoveSelection_SkipsHeadersAndLocked`).
3. **Lepas ke grup tipe/disiplin yang rentangnya penuh atau jenisnya tak cocok** — harapan: seluruh drop ditolak (tidak sebagian), alasan di tooltip/status, ChangeSet tidak berubah. Test di Task 5 (`DropIntoFullGroup_Rejected`).
4. **Model workshared: lembar dipinjam user lain** — harapan: lembar ditandai 🔗 & terkunci otomatis, tidak bisa diedit/dipindah; Terapkan tidak gagal total. Test di Task 3 (`Snapshot_NotEditableSheetsLocked`) & Task 9 (adapter membaca `WorksharingUtils.GetCheckoutStatus`).
5. **Nomor ketikan manual/judul aneh di Pemilih Nama & Inspektur** (`a101`, nomor dobel, judul kosong, singkatan) — harapan: validasi langsung berwarna, OK/Terapkan nonaktif saat merah, saran singkatan bisa diterima satu klik. Test di Task 6 & Task 8.

---

## File Structure

```
Sheet Studio/
├─ Directory.Build.props, SheetStudio.sln, README.md
├─ docs/reference/{01-kelengkapan-gambar-pupr.md, 02-penamaan-lembar-aia-ncs.md}
├─ docs/superpowers/{specs,plans}/…
├─ src/SheetStudio.Core/
│  ├─ Profiles/{NamingProfile.cs, ProfileLoader.cs, pupr.json, uds.json}
│  ├─ Naming/{SheetId.cs, TitleRules.cs, SortKey.cs}
│  ├─ Model/{SheetSnapshot.cs, ProjectState.cs}
│  ├─ Layers/{LayerRow.cs, LayerListBuilder.cs, SelectionModel.cs}
│  ├─ Changes/{SheetChange.cs, ChangeSet.cs, UndoStack.cs, Rebase.cs}
│  ├─ Numbering/{Sequencer.cs, NumberMap.cs, TwoPhasePlan.cs}
│  ├─ Create/{CreateRequest.cs, BatchRow.cs, BatchMatrix.cs, BatchCsv.cs, BatchPlanner.cs, LayoutGrid.cs, Presets.cs}
│  ├─ Edit/{BulkEdit.cs}
│  ├─ Preferences/{Preferences.cs, PreferencesStore.cs}
│  └─ ViewModels/{ObservableObject.cs, RelayCommand.cs, IStudioGateway.cs, StudioViewModel.cs,
│                 LayersViewModel.cs, LayerRowViewModel.cs, NamePickerViewModel.cs,
│                 CreateSheetViewModel.cs, BatchViewModel.cs, InspectorViewModel.cs, PreferencesViewModel.cs}
├─ src/SheetStudio.Revit/
│  ├─ App.cs, SheetStudio.addin
│  ├─ Revit/{SnapshotReader.cs, ApplyExecutor.cs, CreateExecutor.cs, ViewCreator.cs, ViewportPlacer.cs,
│  │         StorageAdapter.cs, ExternalEventQueue.cs, RevitStudioGateway.cs, ModelWatcher.cs, ThumbnailService.cs}
│  └─ Ui/{StudioPane.xaml, LayersPanel.xaml, InspectorPanel.xaml, NamePicker.xaml, CreateSheetWindow.xaml,
│         BatchWindow.xaml, PreferencesWindow.xaml, Theme/{Light.xaml, Dark.xaml}, Converters.cs,
│         LayersDropHandler.cs, KeyboardShortcuts.cs}
├─ tests/SheetStudio.Core.Tests/
└─ tests/SheetStudio.Revit.Tests/
```

---

### Task 1: Scaffold + profil penamaan

**Files:** Create kerangka solusi/proyek/tes (pola Family Browser), `App.cs` (tab ribbon "Sheet Studio", tombol buka pane — pane kosong dulu), `Profiles/*`; salin `01-`, `02-` ke `docs/reference/`; Test `ProfileTests.cs`, Revit `SmokeTests.cs`.

**Interfaces:**
- Produces:
  - `record DisciplineDef(string Code, string NamaId, string NamaEn, string Keterangan, int Order, bool Umum, IReadOnlyList<SecondLetterDef> HurufKedua)`; `record SecondLetterDef(char Huruf, string Nama, string Keterangan)`.
  - `record SheetTypeDef(int Digit, string Nama, string Keterangan)`.
  - `record DrawingKindDef(string Id, string Nama, string Disiplin, int Tipe, int SeqFrom, int SeqTo, string TemplatJudul, bool PerLevel, string JenisView, int SkalaDefault, string? ViewTemplate)` — `TemplatJudul` memakai `{subjek}`.
  - `record NamingProfile(string Id, string Nama, IReadOnlyList<DisciplineDef> Disiplin, IReadOnlyList<SheetTypeDef> Tipe, IReadOnlyList<DrawingKindDef> Jenis, IReadOnlyDictionary<string,string> Singkatan) { DisciplineDef? FindDiscipline(string code); IEnumerable<DrawingKindDef> KindsFor(string disc, int type); }`.
  - `static class ProfileLoader { NamingProfile LoadBuiltIn(string id); IReadOnlyList<string> BuiltInIds; NamingProfile LoadFromJson(string json); }` — ID bawaan `"pupr"`, `"uds"`.
- Isi `pupr.json`: 21 disiplin (spek 02 §2; `Umum` = G,C,L,S,A,I,M,E,P,F,T), huruf ke-2 untuk A (AS, AD, AE, AI, AF, AG — keterangan "cek NCS"), E (EL Penerangan, EP Daya), M (MH HVAC, MP Pipa), F (FA Alarm), Q (QH Peralatan RS), R (RA Arsitektur eksisting); 10 tipe (spek 02 §3, keterangan Indonesia); jenis gambar = semua baris spek 02 §5 (id kebab, mis. `denah`, `rencana-plafon`); singkatan = daftar plan v2 Task 4 (tanpa `PT`). `uds.json`: disiplin & tipe sama, `Jenis` kosong.

- [ ] **Step 1: Tes gagal** — `Pupr_Has21DisciplinesInNcsOrder` (gabungan `Code` berurut `Order` = `"GHVBCLSAIQFPDMEWTRXZO"`); `Pupr_Has10Types` (digit 0..9; `Tipe[4].Nama == "Large-Scale Views"`); `Pupr_KindsFor_A1_ContainsDenahFirst` (`KindsFor("A",1).First().Id == "blockplan"`, memuat `"denah"` rentang 10–39); `Pupr_KindRangesDoNotOverlap` (per disiplin+tipe tak ada rentang beririsan); `Uds_HasNoKinds`; `LoadFromJson_MissingField_ThrowsWithFieldName`. Revit: `Smoke_PluginLoadsAndRibbonTabExists`.
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests` → FAIL.
- [ ] **Step 3:** Implement (JSON embedded resource; validasi saat load).
- [ ] **Step 4:** `dotnet test` Core & Revit → PASS.
- [ ] **Step 5:** Commit `chore: scaffold Sheet Studio + profil PUPR/UDS`.

---

### Task 2: Mesin penamaan (SheetId, judul, sort key)

**Files:** Create `Naming/SheetId.cs`, `TitleRules.cs`, `SortKey.cs`; Test `SheetIdTests.cs`, `TitleRulesTests.cs`, `SortKeyTests.cs`

**Interfaces:**
- Consumes: `NamingProfile`.
- Produces:
  - `readonly record struct SheetId(string Discipline, int Type, int Seq) { string Format(); static ParseResult Parse(string? text, NamingProfile p); }`; `record ParseResult(bool Ok, SheetId Id, string? Error, string? Normalized)`.
  - `enum Severity { Ok, Peringatan, Galat }`; `record TitleCheck(string Normalized, Severity Severity, IReadOnlyList<AbbrevHint> Hints, string? Pesan)`; `record AbbrevHint(string Found, string? Suggestion)`.
  - `static class TitleRules { TitleCheck Check(string? raw, NamingProfile p); string ApplySuggestions(string title, NamingProfile p); }` — kosong → Galat "Judul kosong"; huruf kecil → dinormalisasi kapital (Ok); singkatan dikenal → Peringatan + saran; token bertitik/≤3 huruf mati tak dikenal → Peringatan tanpa saran; pengecualian kode `A-A`, `P1`, `J1`, `BV1`, angka.
  - `static class SortKey { string For(SheetId id, NamingProfile p, int positionInGroup); }` → `"{order:00}.{disc}.{type}.{position:0000}"`.

- [ ] **Step 1: Tes gagal** — kasus dari plan v2 Task 2 (format & parse valid/invalid dengan pesan Indonesia: "Disiplin satu huruf wajib tanda '-'", "Format salah", "Disiplin 'K' tidak dikenal", "Sequence 00 hanya untuk G-000", "Nomor kosong") + `Parse_Lowercase_NormalizedSuggested` (`"a-101"` → Ok, `Normalized == "A-101"`); judul: `"Denah Lt. 2"` → Normalized `"DENAH LT. 2"`, Peringatan, hint `LT.`→`LANTAI`; `ApplySuggestions` → `"DENAH LANTAI 2"`; `"POTONGAN A-A"` Ok tanpa hint; `""` Galat; sort key `A-111` posisi 3 → `"08.A.1.0003"`.
- [ ] **Step 2:** Run `dotnet test --filter "SheetIdTests|TitleRulesTests|SortKeyTests"` → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): mesin penamaan UDS`.

---

### Task 3: Snapshot, baris panel lapisan, model pilihan

**Files:** Create `Model/SheetSnapshot.cs`, `Model/ProjectState.cs`, `Layers/LayerRow.cs`, `LayerListBuilder.cs`, `SelectionModel.cs`; Test `LayerListTests.cs`, `SelectionTests.cs`

**Interfaces:**
- Produces:
  - `record SheetSnapshot(string UniqueId, string Number, string Title, string? KindId, string Subjek, bool InSheetList, bool Editable, string? BorrowedBy, string Versi, IReadOnlyDictionary<string,string> Params, string TitleBlockType)` — `Versi` = `Number + "|" + Title`.
  - `record ProjectState(IReadOnlyDictionary<string, IReadOnlyList<string>> OrderPerGroup, IReadOnlyList<string> DisciplineOrder, IReadOnlySet<string> Locked)` — kunci grup `"A|1"`; `OrderPerGroup` = UniqueId berurutan.
  - `enum RowKind { Disiplin, Tipe, Lembar }`; `record LayerRow(RowKind Kind, string Key, int Indent, string Label, string? Number, int Count, bool Locked, bool InSheetList, bool Expanded, bool Editable, string? SheetUniqueId, string? GroupKey)` — `Key`: disiplin `"A"`, tipe `"A|1"`, lembar = UniqueId.
  - `static class LayerListBuilder { IReadOnlyList<LayerRow> Build(IReadOnlyList<SheetSnapshot> sheets, ProjectState state, NamingProfile p, IReadOnlySet<string> collapsed, string? filter); IReadOnlyList<SheetSnapshot> OrderedSheets(IReadOnlyList<SheetSnapshot> sheets, ProjectState state, NamingProfile p); }` — nomor tak terbaca → grup disiplin `"?"` & tipe `"?|?"` (paling bawah); lembar tanpa urutan tersimpan diurut nomor; `Editable == false` → `Locked` true; filter cocok nomor/judul (case-insensitive), grup tanpa hasil disembunyikan.
  - `class SelectionModel { IReadOnlyList<string> Selected; string? Anchor; void Click(string key); void CtrlClick(string key); void ShiftClick(string key, IReadOnlyList<LayerRow> visible); void CtrlShiftClick(string key, IReadOnlyList<LayerRow> visible); void SelectAll(IReadOnlyList<LayerRow> visible); void Clear(); IReadOnlyList<string> SelectedSheetIds(IReadOnlyList<LayerRow> all); event Action? Changed; }` — klik baris grup memilih semua lembar di grup; `SelectedSheetIds` mengurai pilihan grup ke lembarnya, urut tampilan, tanpa duplikat.

- [ ] **Step 1: Tes gagal** —
  - `Build_GroupsByDisciplineThenType_InNcsOrder` (lembar G-001, A-111, S-101, A-201 → baris: G, G|0, G-001, S, S|1, S-101, A, A|1, A-111, A|2, A-201).
  - `Build_UsesSavedOrderWithinGroup` (OrderPerGroup["A|1"] = [L2, L1] → A-112 tampil sebelum A-111).
  - `Build_UnknownNumbersGoToQuestionGroupLocked`.
  - `Snapshot_NotEditableSheetsLocked` (Review Focus #4).
  - `Build_CollapsedGroupHidesChildren`; `Build_FilterHidesEmptyGroups`.
  - `Build_1000Sheets_Under100ms`.
  - Selection: `CtrlClick_Toggles`; `ShiftClick_SelectsRangeFromAnchor`; `CtrlShiftClick_AddsRange`; `ClickGroup_SelectsChildren`; `SelectAll_OnlyVisibleSheets`; `SelectedSheetIds_DedupesGroupAndChild`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): baris lapisan dan model pilihan`.

---

### Task 4: ChangeSet bertahap, undo/redo, operasi urutan, rebase

**Files:** Create `Changes/SheetChange.cs`, `ChangeSet.cs`, `UndoStack.cs`, `Rebase.cs`; Test `ChangeSetTests.cs`, `RebaseTests.cs`

**Interfaces:**
- Consumes: Task 3.
- Produces:
  - `record SheetChange(string UniqueId, string? NewDiscipline = null, int? NewType = null, int? ManualSeq = null, string? NewTitle = null, bool? InSheetList = null, string? NewTitleBlockType = null, IReadOnlyDictionary<string,string>? NewParams = null, bool Delete = false)`.
  - `record ChangeSet(ProjectState State, IReadOnlyDictionary<string, SheetChange> Changes)` (immutable) dengan metode yang mengembalikan `OpResult(ChangeSet Next, string? Pesan)`:
    - `MoveSelection(IReadOnlyList<string> sheetIds, string targetGroup, int targetIndex, IReadOnlyList<SheetSnapshot> sheets)` — abaikan terkunci; urutan relatif dipertahankan; jika `targetGroup` beda grup asal → set `NewDiscipline/NewType`.
    - `MoveBy(IReadOnlyList<string> ids, int delta)`; `MoveToEdge(IReadOnlyList<string> ids, bool top)`.
    - `MoveDisciplineGroup(string disc, int targetIndex)` (ubah `DisciplineOrder`; pesan peringatan bila keluar urutan profil).
    - `ToggleLock(IReadOnlyList<string> ids)`; `SetInSheetList(IReadOnlyList<string> ids, bool v)`; `MarkDelete(IReadOnlyList<string> ids, bool v)`; `Upsert(SheetChange c)` (gabung field non-null).
    - `int PendingCount`; `bool IsEmpty`.
  - `class UndoStack { void Push(ChangeSet s); ChangeSet? Undo(); ChangeSet? Redo(); bool CanUndo; bool CanRedo; void Reset(ChangeSet baseline); }` — batas 100 langkah.
  - `static class Rebase { (ChangeSet Next, IReadOnlyList<string> Dropped) Onto(ChangeSet pending, IReadOnlyList<SheetSnapshot> fresh); }` — buang perubahan untuk UniqueId yang hilang; lembar baru (tak ada di urutan) ditambahkan di akhir grupnya.

- [ ] **Step 1: Tes gagal** — `MoveSelection_KeepsRelativeOrder` (pilih [L3, L1] urutan tampil L1, L3; pindah ke indeks 0 → urutan L1, L3, L2); `MoveSelection_SkipsHeadersAndLocked` (pesan "1 lembar terkunci tidak dipindah"); `MoveSelection_IntoOtherTypeGroup_SetsNewType`; `MoveBy_AtTop_NoOp`; `MoveToEdge_Bottom`; `MoveDisciplineGroup_OutOfNcsOrder_Warns`; `Upsert_MergesFields`; `MarkDelete_ThenUndo_Restores`; `Undo_Redo_Sequence` (3 operasi, 2× undo, 1× redo → state = setelah operasi 2); `Undo_Limit100`; `Rebase_DropsChangesForDeletedSheet`; `Rebase_AppendsNewSheetToGroupEnd`; `Rebase_KeepsValidChanges`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): changeset bertahap, undo, rebase`.

---

### Task 5: Penomoran (sequencer, peta nomor, dua tahap)

**Files:** Create `Numbering/Sequencer.cs`, `NumberMap.cs`, `TwoPhasePlan.cs`; Test `SequencerTests.cs`, `NumberMapTests.cs`

**Interfaces:**
- Consumes: Task 2–4.
- Produces:
  - `enum Strategy { Rentang, Urut }`; `record NumberingOptions(Strategy Strategy, int Mulai, int Langkah)`.
  - `record NumberResult(string UniqueId, string OldNumber, string NewNumber, string SortKey, bool Changed)`; `record SequenceOutcome(IReadOnlyList<NumberResult> Results, IReadOnlyList<string> Errors)`.
  - `static class Sequencer { SequenceOutcome Compute(IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs, NamingProfile p, NumberingOptions o); bool CanAccept(IReadOnlyList<string> ids, string targetGroup, IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs, NamingProfile p, NumberingOptions o, out string? reason); }` — `ManualSeq` dihormati jika bebas (selain itu Errors); terkunci & non-UDS = terpakai; `Rentang` memakai rentang `DrawingKindDef` dari `KindId` (tanpa jenis/jenis di grup lain → 01–99 tipe itu); `Urut` per grup `Mulai + i×Langkah`; grup penuh → Errors, nomor grup itu tidak berubah.
  - `enum CellState { Bebas, Terpakai, Terkunci, Dipilih, DiluarRentang }`; `static class NumberMap { IReadOnlyList<CellState> For(string disc, int type, string? kindId, IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs, NamingProfile p, int? selectedSeq); }` — 99 sel (01–99).
  - `record RenumberStep(string UniqueId, string Temp, string Final)`; `static class TwoPhasePlan { IReadOnlyList<RenumberStep> Build(IEnumerable<NumberResult> changed); }`.

- [ ] **Step 1: Tes gagal** — `Rentang_FollowsPanelOrder` (urutan panel L2, L1 → A-110 L2, A-111 L1); `Urut_Step10` (5 lembar grup A|1 → A-101, A-111, A-121, A-131, A-141); `Urut_GStartsAtZero` (G|0 → G-000, G-001); `LockedKeepsNumberOthersFlowAround` (A-111 terkunci → A-110, A-112, A-113); `NonUdsNumbersOccupied`; `ManualSeq_Taken_ReportsError`; `MovedToOtherType_GetsNumberInNewGroup` (A-111 pindah ke tipe 2 → A-2xx); `DropIntoFullGroup_Rejected` (`CanAccept` false, reason memuat "penuh"); `DeleteMarked_FreesNumberForOthers`; `NumberMap_StatesCorrect` (terpakai/terkunci/rentang jenis/dipilih); `TwoPhase_TempsUnique`; `Compute_1000Sheets_Under100ms`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): sequencer dan peta nomor`.

---

### Task 6: Pemilih Nama (ViewModel)

**Files:** Create `ViewModels/ObservableObject.cs`, `RelayCommand.cs`, `NamePickerViewModel.cs`; Test `NamePickerTests.cs`

**Interfaces:**
- Consumes: Task 1, 2, 5.
- Produces `class NamePickerViewModel : ObservableObject`:
  - Konstruktor `(NamingProfile p, IReadOnlyList<SheetSnapshot> sheets, ChangeSet cs, NumberingOptions o, IReadOnlyList<string> levelNames, string? editingUniqueId = null)`.
  - Disiplin: `IReadOnlyList<DisciplineOption> DisciplineOptions` (`Code`, `NamaId`, `NamaEn`, `Keterangan`, `IsSelected`), `bool ShowAllDisciplines` (false → hanya `Umum`), `string SelectedDiscipline`, `IReadOnlyList<SecondLetterOption> SecondLetters` (item pertama "— (tanpa)"), `char? SelectedSecondLetter`, `string DisciplineDescription`, `ICommand NextDiscipline/PrevDiscipline`.
  - Tipe: `int SheetType` (0–9, slider), `IReadOnlyList<string> TypeTickLabels`, `string TypeName`, `string TypeDescription`.
  - Urut: `bool AutoSeq` (default true), `int ManualSeq`, `int EffectiveSeq`, `IReadOnlyList<CellState> NumberMapCells`, `ICommand PickCell(int seq)` (sel Bebas → `AutoSeq=false`, `ManualSeq=seq`).
  - Judul: `IReadOnlyList<DrawingKindDef> KindOptions` (= `KindsFor(disc,type)` + "Bebas"), `string? SelectedKindId`, `IReadOnlyList<string> SubjectOptions` (level, `TAMPAK DEPAN/BELAKANG/KIRI/KANAN`, `POTONGAN A-A…D-D`), `string Subject`, `string FreeTitle`, `string TitlePreview`, `IReadOnlyList<AbbrevHint> Hints`, `ICommand AcceptSuggestions`.
  - Hasil: `string NumberPreview`, `Severity Status`, `string StatusText`, `bool CanConfirm`, `CreateName Result` → `record CreateName(SheetId Id, string Title, string? KindId, string Subjek)`.
  - Dua arah: memilih `SelectedKindId` mengisi disiplin+tipe dari jenis; mengubah disiplin/tipe ke kombinasi yang tak memuat jenis terpilih → `SelectedKindId = null` (Bebas).

- [ ] **Step 1: Tes gagal** — `Default_A_Type1_AutoSeqFirstFree`; `SliderChange_UpdatesDescriptionAndKinds` (tipe 3 → `TypeName == "Sections"`, `KindOptions` berisi potongan); `SelectKind_SetsDisciplineAndType` (`"detail-pintu-jendela"` → A, 5); `ShowAll_Lists21`; `SecondLetter_EP_PreviewEP1xx`; `PickCell_TakenIgnored_FreeSets`; `DuplicateNumber_StatusGalat_CannotConfirm`; `TitleWithAbbrev_Warns_AcceptSuggestions_Fixes`; `EmptyTitle_CannotConfirm`; `Editing_ExistingSheetNotCountedAsDuplicateOfItself`; `PropertyChanged_RaisedForPreview` (mengubah Subject memicu `TitlePreview`, `StatusText`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): viewmodel pemilih nama`.

---

### Task 7: Perencana pembuatan (tunggal & batch)

**Files:** Create `Create/CreateRequest.cs`, `BatchRow.cs`, `BatchMatrix.cs`, `BatchCsv.cs`, `BatchPlanner.cs`, `LayoutGrid.cs`, `Presets.cs`; Test `CreatePlanTests.cs`, `BatchCsvTests.cs`, `LayoutGridTests.cs`

**Interfaces:**
- Consumes: Task 1, 2, 5, 6 (`CreateName`).
- Produces:
  - `enum Kertas { A3, A2, A1 }`; `enum ViewMode { Tanpa, PakaiYangAda, BuatBaru }`; `record ViewSpec(ViewMode Mode, string? ExistingViewUniqueId, string? JenisView, string? LevelName, int Skala, string? ViewTemplate)`.
  - `record CreateRequest(CreateName Name, Kertas Kertas, string? TitleBlockType, IReadOnlyList<ViewSpec> Views)`.
  - `record BatchRow(string? KindId, string Subjek, string? LevelName, string? NomorDiminta, string? Judul, int Skala, Kertas Kertas, bool BuatView)`.
  - `static class BatchMatrix { IReadOnlyList<BatchRow> Build(IEnumerable<string> kindIds, IEnumerable<(string Name, double Elevation)> levels, NamingProfile p, Kertas k, bool buatView); }` — jenis `PerLevel` → satu baris per level urut elevasi; lainnya satu baris.
  - `static class BatchCsv { IReadOnlyList<BatchRow> Read(string text, NamingProfile p, out IReadOnlyList<string> errors); string Write(IEnumerable<BatchRow> rows); }` — header `JENIS;SUBJEK;LEVEL;NOMOR;JUDUL;SKALA;KERTAS;BUAT VIEW`, pemisah `;` atau `,`, error "Baris N: …".
  - `enum PlanStatus { Baru, SudahAda, Duplikat, Galat }`; `record PlannedSheet(BatchRow Row, CreateRequest? Request, PlanStatus Status, string? Pesan)`; `static class BatchPlanner { IReadOnlyList<PlannedSheet> Plan(IReadOnlyList<BatchRow> rows, IReadOnlyList<SheetSnapshot> existing, ChangeSet cs, NamingProfile p, NumberingOptions o, Preferences prefs); }` — nomor otomatis memakai himpunan terpakai yang bertambah per baris; judul = `NomorDiminta`/`Judul` jika diisi, selain itu `TemplatJudul` + subjek; cek judul via `TitleRules`.
  - `record Rect(double X, double Y, double W, double H)`; `static class LayoutGrid { IReadOnlyList<(double Cx, double Cy)> Arrange(IReadOnlyList<(double W, double H)> sizes, Rect area, double gap, out bool fitsAll); }`.
  - `static class Presets { IReadOnlyList<string> Names; IReadOnlyList<BatchRow> Get(string name, IEnumerable<(string, double)> levels, NamingProfile p, Kertas k); }` — `"DED PUPR LENGKAP"` (checklist spek 01 bab 4), `"DENAH SEMUA LANTAI"`, `"TAMPAK & POTONGAN"`.

- [ ] **Step 1: Tes gagal** — `Matrix_4KindsX3Levels_10Rows`; `Plan_SequentialUniqueNumbers`; `Plan_ExistingAndDuplicateFlagged`; `Plan_RequestedNumberTaken_FallsBackWithPesan`; `Plan_RangeFull_Galat`; `Csv_RoundTrip`; `Csv_Comma`; `Csv_UnknownKind_ErrorLine`; `Layout_Single_Centered`; `Layout_Three_Wrap`; `Layout_TooBig_FitsAllFalse`; `Preset_DedPupr_ContainsCoreNumbers` (G-000, G-001, A-101, A-111, A-201, A-301, A-901 di proyek kosong dengan level LANTAI 1).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): perencana pembuatan dan batch`.

---

### Task 8: Edit massal & ViewModel utama (Lapisan, Inspektur, Batch, Preferensi)

**Files:** Create `Edit/BulkEdit.cs`, `Preferences/*`, `ViewModels/IStudioGateway.cs`, `StudioViewModel.cs`, `LayersViewModel.cs`, `LayerRowViewModel.cs`, `InspectorViewModel.cs`, `CreateSheetViewModel.cs`, `BatchViewModel.cs`, `PreferencesViewModel.cs`; Test `BulkEditTests.cs`, `PreferencesTests.cs`, `StudioViewModelTests.cs` (fake gateway)

**Interfaces:**
- Consumes: Task 1–7.
- Produces:
  - `static class BulkEdit { ChangeSet FindReplace(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string find, string replace, bool wholeWord); ChangeSet PrefixSuffix(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string prefix, string suffix); ChangeSet Uppercase(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel); ChangeSet ApplySuggestions(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, NamingProfile p); ChangeSet SetParam(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string name, string value); ChangeSet SetDisciplineType(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string? disc, int? type); ChangeSet SetTitleBlock(ChangeSet cs, IReadOnlyList<SheetSnapshot> sel, string type); }` — semua bekerja pada judul efektif (perubahan bertahap sebelumnya ikut).
  - `record Preferences(string ProfileId, Strategy Strategy, int Mulai, int Langkah, IReadOnlyDictionary<Kertas,string> TitleBlockPerKertas, Kertas KertasDefault, bool Thumbnail, bool KonfirmasiTerapkan, bool BatchLewatiGagal)`; defaults: `"pupr"`, Rentang, 1, 1, `{A3:"PUPR_KOP_A3",A2:"PUPR_KOP_A2",A1:"PUPR_KOP_A1"}`, A3, true, true, true. `static class PreferencesStore { string Serialize(Preferences p); bool TryDeserialize(string json, out Preferences p, out string? err); Preferences Merge(Preferences bawaan, string? userJson, string? projectJson); }`.
  - `interface IStudioGateway { Task<StudioSnapshot> LoadAsync(); Task<ApplyReport> ApplyAsync(ApplyPlan plan); Task<CreateReport> CreateAsync(IReadOnlyList<CreateRequest> reqs, IProgress<(int Done,int Total,string Title)> progress, CancellationToken ct); Task<CreateReport> DuplicateAsync(IReadOnlyList<string> ids, DuplicateMode mode); Task OpenSheetAsync(string uniqueId); Task<byte[]?> ThumbnailAsync(string uniqueId); Task SaveAsync(Preferences? prefs, ProjectState? state, bool userLevel); event Action? ModelChanged; }`.
  - `record StudioSnapshot(IReadOnlyList<SheetSnapshot> Sheets, ProjectState State, Preferences Prefs, IReadOnlyList<(string Name,double Elevation)> Levels, IReadOnlyList<string> TitleBlockTypes, IReadOnlyList<ViewInfo> Views)`; `record ViewInfo(string UniqueId, string Name, string Jenis, bool OnSheet)`; `record ApplyPlan(IReadOnlyList<RenumberStep> Renumber, IReadOnlyList<SheetChange> Edits, IReadOnlyList<string> Deletes, IReadOnlyDictionary<string,string> ExpectedVersi, ProjectState NewState)`; `record ApplyReport(int Applied, IReadOnlyList<(string UniqueId,string Pesan)> Skipped)`; `enum DuplicateMode { LembarSaja, DenganView, DenganViewDetailing }`; `record CreateReport(IReadOnlyList<(string Title, bool Ok, string? Pesan, string? UniqueId)> Rows, bool Dibatalkan)`.
  - `class StudioViewModel` — memegang snapshot, `ChangeSet Current`, `UndoStack`, `Sequencer` output; `LayersViewModel Layers`, `InspectorViewModel Inspector`; `ICommand Undo, Redo, Apply, Discard, Refresh, NewSheet, Batch, OpenPreferences`; `int PendingCount`; `string Status`; `bool ConfirmApplyRequested` (UI menampilkan dialog pratinjau bila `KonfirmasiTerapkan`). `ModelChanged` → `Rebase.Onto` → status "N perubahan dibuang karena lembar berubah".
  - `class LayersViewModel` — `ObservableCollection<LayerRowViewModel> Rows`, `SelectionModel Selection`, metode yang dipanggil View: `Click/CtrlClick/ShiftClick/CtrlShiftClick(string key)`, `SelectAll()`, `bool CanDrop(IReadOnlyList<string> keys, string targetKey, DropPosition pos, out string? reason)`, `void Drop(IReadOnlyList<string> keys, string targetKey, DropPosition pos)` (`enum DropPosition { Sebelum, Sesudah, Ke }`), `MoveUp/MoveDown/MoveTop/MoveBottom`, `ToggleLock/ToggleEye(string key)`, `ToggleCollapse(string key, bool all)`, `RenameInline(string uniqueId, string title)`, `MarkDelete()`, `string Filter`.
  - `class LayerRowViewModel` — `Kind`, `Indent`, `Label`, `Number`, `NewNumber`, `bool NumberChanged`, `bool Warning`, `string? WarningText`, `bool Edited`, `bool Deleted`, `bool Locked`, `bool Borrowed`, `bool InSheetList`, `bool Expanded`, `bool IsSelected`, `bool IsNew`, `byte[]? Thumbnail`.
  - `class InspectorViewModel` — `int SelectedCount`; nilai campuran sebagai `string?` dengan `MixedText = "<berbeda>"`; `string? Discipline`, `int? SheetType`, `int? ManualSeq` (aktif hanya 1 lembar), `string? Title`, `Find/Replace/Prefix/Suffix`, `ObservableCollection<ParamField> Params` (`Name`, `Value`, `IsMixed`), `string? TitleBlockType`, `bool? InSheetList`; `ICommand ApplyTitle, ApplyFindReplace, ApplyPrefixSuffix, Uppercase, AcceptSuggestions, Duplicate(DuplicateMode), Delete`. Setiap perintah → `BulkEdit` → push ke `StudioViewModel`.
  - `class CreateSheetViewModel` (NamePicker + kertas/title block + `ViewMode` + daftar view yang belum di lembar dengan pencarian + level/skala/template) & `class BatchViewModel` (matriks centang jenis×level, `ObservableCollection<BatchRow>` dapat diedit, impor/ekspor CSV, preset, `Plan`, `Run`, `Cancel`, progres) & `PreferencesViewModel`.

- [ ] **Step 1: Tes gagal** — BulkEdit: `FindReplace_WholeWord` (`LT`→`LANTAI` tidak mengubah `ALTAR`); `PrefixSuffix`; `SetDisciplineType_Multi`. Preferensi: defaults, merge berlapis, JSON rusak → `TryDeserialize` false "Preferensi rusak". StudioViewModel (fake): `Drop_UpdatesNewNumbersWithoutApply` (gateway.ApplyAsync tidak dipanggil; `PendingCount` > 0); `Undo_RestoresRowsAndNumbers`; `Apply_SendsTwoPhasePlanAndExpectedVersi`; `Apply_ClearsUndoAndReloads`; `ModelChanged_RebasesAndReportsDropped`; `Inspector_MixedValuesShowBerbeda`; `Inspector_SeqDisabledForMulti`; `CanDrop_LockedOrFull_FalseWithReason`; `Batch_RunCancel_ReportsDibatalkan`; `Create_InvalidName_CommandDisabled`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): edit massal dan viewmodel studio`.

---

### Task 9: Adapter Revit — snapshot, simpan status, terapkan

**Files:** Create `Revit/SnapshotReader.cs`, `StorageAdapter.cs`, `ApplyExecutor.cs`, `ExternalEventQueue.cs`, `RevitStudioGateway.cs` (bagian Load/Apply/Save/Open); Test `tests/SheetStudio.Revit.Tests/ApplyTests.cs`, `StorageTests.cs`, `SnapshotTests.cs`

**Interfaces:**
- Consumes: Task 8 (`IStudioGateway`, `StudioSnapshot`, `ApplyPlan`, `ApplyReport`).
- Produces:
  - `static class SnapshotReader { StudioSnapshot Read(Document doc, Preferences prefs, ProjectState state); }` — abaikan placeholder; `KindId`/`Subjek` dari parameter proyek `SS_JenisGambar`/`SS_Subjek` (dibuat otomatis saat Terapkan/Create pertama sebagai project parameter teks kategori Sheets); `Editable` & `BorrowedBy` dari `WorksharingUtils.GetCheckoutStatus` + `GetWorksharingTooltipInfo` (non-workshared → editable); `Params` = parameter instance teks/angka lembar yang bisa ditulis.
  - `static class StorageAdapter { (string? Preferensi, string? StatusUrutan) Read(Document doc); void Write(Document doc, string? preferensi, string? statusUrutan); }` — schema dari Global Constraints, di `ProjectInformation`.
  - `static class ApplyExecutor { ApplyReport Apply(Document doc, ApplyPlan plan); }` — `TransactionGroup("Sheet Studio: Terapkan")`: T1 cek `ExpectedVersi` (beda/hilang → Skipped "berubah sejak dibaca"/"tidak ada"), set nomor temp; T2 nomor final, judul, parameter, `Appears In Sheet List`, ganti title block (`FamilyInstance.ChangeTypeId` pada title block lembar), hapus; T3 simpan `NewState`; `Assimilate`. Galat per lembar ditangkap → Skipped dengan pesan, lembar lain lanjut.
  - `class ExternalEventQueue { Task<T> Run<T>(Func<UIApplication,T> work); }`.

- [ ] **Step 1: Tes gagal (ricaun)** — `Snapshot_ReadsSheetsAndParams`; `Storage_RoundTrip`; `Apply_SwapNumbers_NoError`; `Apply_RenameAndParams`; `Apply_ChangeTitleBlock`; `Apply_DeleteSheet`; `Apply_SkipsSheetChangedSinceSnapshot` (ubah nomor via API setelah snapshot → Skipped berpesan "berubah sejak dibaca", lainnya diterapkan); `Apply_SkipsDeletedSheet`; `Apply_PersistsStateInStorage`.
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Revit.Tests --filter "ApplyTests|StorageTests|SnapshotTests"` → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(revit): snapshot, storage, terapkan`.

---

### Task 10: Adapter Revit — buat lembar, view, viewport, duplikat, thumbnail, watcher

**Files:** Create `Revit/CreateExecutor.cs`, `ViewCreator.cs`, `ViewportPlacer.cs`, `ThumbnailService.cs`, `ModelWatcher.cs`; Modify `RevitStudioGateway.cs` (Create/Duplicate/Thumbnail/ModelChanged); Test `CreateTests.cs`, `WatcherTests.cs`

**Interfaces:**
- Produces:
  - `static class CreateExecutor { CreateReport Create(Document doc, IReadOnlyList<CreateRequest> reqs, Preferences prefs, bool lewatiGagal, IProgress<(int,int,string)>? progress, CancellationToken ct); CreateReport Duplicate(Document doc, IReadOnlyList<string> ids, DuplicateMode mode); }` — TransactionGroup "Sheet Studio: Buat Lembar", sub-transaksi per baris; title block: `TitleBlockType` atau `prefs.TitleBlockPerKertas[k]`, tidak ada → galat "Title block 'X' tidak ditemukan"; cancel/`!lewatiGagal` saat galat → rollback grup. Duplikat via `ViewSheet.Duplicate(SheetDuplicateOption)` (Revit 2023+), nomor baru dari `Sequencer` (seq bebas berikutnya di grup).
  - `static class ViewCreator { View? Create(Document doc, ViewSpec spec, string title, out string? warning); }` — `Denah`/`Plafon`: `ViewPlan.Create` di level; `Drafting`, `Legend` (duplikat legend pertama — legend tidak bisa dibuat via API, beri peringatan bila tidak ada), `TigaD`: `View3D.CreateIsometric`; `Tampak`/`Potongan`: drafting placeholder + peringatan; view template diterapkan jika ada; view yang sudah di lembar lain (non-legend/schedule) → null + "View sudah dipakai di lembar X".
  - `static class ViewportPlacer { Rect DrawingArea(ViewSheet s); void Place(Document doc, ViewSheet s, IReadOnlyList<View> views, out bool fitsAll); }` — area dari parameter instance title block `AreaGambar_Kiri/Bawah/Lebar/Tinggi` (mm) bila ada, selain itu outline lembar − margin 10 mm; posisi via `LayoutGrid`.
  - `class ThumbnailService { Task<byte[]?> Get(string uniqueId); }` — `ImageExportOptions` 120 px, cache file `%LocalAppData%\SheetStudio\thumbs\{docGuid}\{uniqueId}-{versi-hash}.png`, antrean satu per Idling.
  - `class ModelWatcher` — `DocumentChanged` dengan `ViewSheet`/title block terpengaruh → debounce 500 ms → `ModelChanged`; tidak mengubah dokumen di handler.

- [ ] **Step 1: Tes gagal (ricaun)** — `Create_SingleWithNewFloorPlan_CenteredViewport` (pusat ±1 mm); `Create_Batch3_NumbersSequential`; `Create_MissingTitleBlock_SkipFailed_ContinuesOthers`; `Create_RollbackAllOnError` (jumlah lembar tetap); `Create_Cancel_RollsBack`; `Create_ViewAlreadyOnSheet_WarnsNoViewport`; `Duplicate_WithViews_NewNumberInSameGroup`; `Thumbnail_ReturnsPng`; `Watcher_RaisesOnSheetAdd`; `Watcher_NoTransactionsInsideHandler`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(revit): buat lembar, duplikat, thumbnail, watcher`.

---

### Task 11: UI — kerangka pane + panel Lapisan (ala Photoshop)

**Files:** Create `Ui/StudioPane.xaml(.cs)`, `LayersPanel.xaml(.cs)`, `LayersDropHandler.cs`, `KeyboardShortcuts.cs`, `Converters.cs`, `Theme/Light.xaml`, `Theme/Dark.xaml`; Modify `App.cs` (register pane, tombol "Sheet Studio" toggle); Test `UiSmokeTests.cs` (ricaun) + checklist manual.

**Interfaces:**
- Consumes: `StudioViewModel`, `LayersViewModel`, `LayerRowViewModel` (Task 8), `RevitStudioGateway` (Task 9–10).
- Keputusan UI:
  - `ListBox` `SelectionMode=Extended` **tidak** dipakai untuk logika pilihan; klik diteruskan ke `LayersViewModel.Click/CtrlClick/ShiftClick/CtrlShiftClick` (berdasarkan `Keyboard.Modifiers`) agar klik grup & aturan Photoshop konsisten; `IsSelected` di-bind satu arah dari VM.
  - Virtualisasi: `VirtualizingPanel.IsVirtualizing=True`, `VirtualizationMode=Recycling`, `ScrollUnit=Pixel`.
  - Drag-drop: gong-wpf-dragdrop; `LayersDropHandler : IDropTarget` memanggil `CanDrop` (adorner garis sisip biru untuk `Sebelum/Sesudah`, sorot kotak untuk `Ke` grup, kursor terlarang + tooltip `reason` bila false) lalu `Drop`. Drag membawa semua key terpilih (gong `IDragSource` kustom).
  - Template baris: kolom ikon 👁 (ToggleButton), 🔒 (ToggleButton), indentasi 16 px × `Indent`, panah lipat (grup), thumbnail 48×34 (lembar, jika `Prefs.Thumbnail`), nomor (tebal + warna aksen bila `NumberChanged`, tooltip `lama → baru`), judul (TextBox inline saat F2/klik-ganda), badge ⚠ ✎ 🔗 ★, coret merah bila `Deleted`.
  - Pintasan (`KeyboardShortcuts` InputBindings pada pane): Ctrl+A, Esc, Delete, F2, Ctrl+D, Ctrl+Z, Ctrl+Y, Ctrl+], Ctrl+[, Ctrl+Shift+], Ctrl+Shift+[, Enter (buka lembar). Menu konteks sesuai spek §3.2.
  - Bilah bawah: jumlah perubahan, ↶ ↷, Pratinjau daftar (dialog tabel lama→baru), Buang, TERAPKAN (aksen). Toolbar atas: + Baru, ⧉ Batch, ⟳, kotak cari, radio Rentang/Urut, ⚙ Preferensi.
  - Tema: kamus sumber daya dipilih dari `UIThemeManager.CurrentTheme`; ganti saat `ThemeChanged`.

- [ ] **Step 1: Tes gagal (ricaun)** — `Pane_RegistersAndShows`; `LayersPanel_BindsRowsFromViewModel` (instansiasi kontrol di thread Revit dengan VM fake 3 lembar → `ItemsSource` berisi 5 baris); `DropHandler_RejectsLockedWithReason` (panggil `DragOver` dengan `DropInfo` buatan).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5: Checklist manual** (Revit tes, add-in diinstal opt-in): Ctrl+klik 3 lembar lalu drag → pindah bersama dengan garis sisip; Shift+klik rentang; drag ke grup tipe lain → nomor berubah & badge; drop ke grup penuh ditolak + tooltip; gembok grup mengunci semua; F2 ubah judul; Ctrl+Z panel; scroll 1000 lembar lancar; tema gelap terbaca.
- [ ] **Step 6:** Commit `feat(ui): pane dan panel lapisan`.

---

### Task 12: UI — Pemilih Nama, dialog Buat, jendela Batch

**Files:** Create `Ui/NamePicker.xaml(.cs)` (UserControl), `CreateSheetWindow.xaml(.cs)`, `BatchWindow.xaml(.cs)`; Test `UiSmokeTests.cs` (tambah)

**Interfaces:**
- Consumes: `NamePickerViewModel`, `CreateSheetViewModel`, `BatchViewModel`.
- Keputusan UI (spek §4–5):
  - Disiplin: `ItemsControl` + `WrapPanel` berisi `RadioButton` bergaya kartu 64×56 (kode 20 pt tebal, nama 9 pt), tooltip = `NamaId — NamaEn — Keterangan`; baris keterangan di bawah; toggle "tampilkan semua"; ←/→ = `PrevDiscipline/NextDiscipline`.
  - Huruf ke-2: `ComboBox` dengan item dua baris (kode + keterangan).
  - Tipe: `Slider` Minimum 0, Maximum 9, `TickFrequency=1`, `IsSnapToTickEnabled=True`, `TickPlacement=BottomRight`; label 0–9 di bawah (klikable); kartu keterangan `TypeName` + `TypeDescription`; roda mouse di atas slider mengubah 1 langkah.
  - Peta nomor: `UniformGrid` 10 kolom × 10 baris (sel 01–99 + kosong), warna per `CellState` (legend kecil), klik → `PickCell`.
  - Judul: ComboBox jenis (Bebas di akhir), ComboBox subjek editable, TextBox bebas; pratinjau nomor 28 pt + judul 14 pt; status warna (hijau/kuning/merah) + daftar saran dengan tombol "Pakai saran".
  - Dialog Buat: NamePicker + Kertas (segmented A3/A2/A1) + title block (ComboBox) + View (tab Tanpa / Pakai yang ada [daftar + cari, hanya `!OnSheet`] / Buat baru [jenis, level, skala, template]); tombol BUAT nonaktif bila `!CanConfirm`.
  - Batch: kiri = matriks centang (baris jenis dikelompokkan disiplin/tipe; kolom level); kanan = `DataGrid` baris (kolom status berwarna + pesan); tombol Impor CSV / Ekspor CSV / Preset ▾ / Pratinjau / Jalankan / Batal; `ProgressBar` + teks "12/30 · DENAH LANTAI 3".

- [ ] **Step 1: Tes gagal (ricaun)** — `NamePicker_Instantiates_WithProfilePupr` (21 kartu saat ShowAll); `CreateWindow_ConfirmDisabledWhenInvalid`; `BatchWindow_Instantiates`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5: Checklist manual**: geser slider → keterangan & daftar jenis berubah langsung; pilih jenis "DETAIL PINTU DAN JENDELA" → disiplin A & tipe 5 ikut; klik sel peta nomor; ketik "Lt" → saran muncul; buat lembar denah baru dengan view → viewport di tengah; batch preset DED PUPR 3 lantai → progres jalan, Batal mengembalikan.
- [ ] **Step 6:** Commit `feat(ui): pemilih nama, buat, batch`.

---

### Task 13: UI — Inspektur & Preferensi

**Files:** Create `Ui/InspectorPanel.xaml(.cs)`, `PreferencesWindow.xaml(.cs)`; Test `UiSmokeTests.cs` (tambah)

**Interfaces:**
- Consumes: `InspectorViewModel`, `PreferencesViewModel`.
- Keputusan UI (spek §6–7): panel kanan berbagian lipat NOMOR / JUDUL / KOP / TAMPILAN / AKSI; nilai campuran ditampilkan miring abu `<berbeda>` (watermark); Disiplin = ComboBox berketerangan (bukan kartu, hemat ruang) + Tipe = slider mini 0–9 dengan label nama tipe; Urut hanya aktif untuk 1 lembar (tombol "Pilih di peta…" membuka popup peta nomor dari NamePicker); KOP = `ItemsControl` `ParamField` (label + TextBox); Duplikat = SplitButton 3 mode; Hapus merah. Preferensi: profil (ComboBox + Impor profil JSON), strategi + Mulai + Langkah (slider 1/5/10), title block per kertas, thumbnail, konfirmasi, mode galat batch; tombol "Simpan untuk proyek ini" & "Jadikan default saya".

- [ ] **Step 1: Tes gagal (ricaun)** — `Inspector_ShowsBerbedaForMixed`; `Preferences_SaveProjectWritesStorage`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5: Checklist manual**: pilih 5 lembar → cari-ganti "LT" → "LANTAI" → badge ✎ di 5 baris → Terapkan → satu Ctrl+Z di Revit mengembalikan semua; ganti title block massal; ubah tipe massal → nomor pindah grup.
- [ ] **Step 6:** Commit `feat(ui): inspektur dan preferensi`.

---

### Task 14: Ujung-ke-ujung, README, rilis lokal

**Files:** Create `tests/SheetStudio.Revit.Tests/EndToEndTests.cs`; Modify `README.md`

- [ ] **Step 1: Tes gagal (ricaun)** — `E2E_CreateReorderEditApply`:
  1. Proyek baru + level BASEMENT 1, LANTAI 1, LANTAI 2; lewat gateway jalankan preset `"DENAH SEMUA LANTAI"` → A-110 (B1), A-111, A-112.
  2. Lewat `StudioViewModel`: pilih A-112, `Drop` ke posisi teratas grup A|1 → pratinjau A-110 = LANTAI 2; kunci LANTAI 1; Terapkan → nomor sesuai; LANTAI 1 tetap.
  3. Inspector: pilih semua grup A|1, Prefix "RENCANA " lalu Undo panel → judul tidak berubah; ganti judul satu lembar jadi "Denah Lt. 2" → badge ⚠, `AcceptSuggestions` → "DENAH LANTAI 2"; ubah tipe satu lembar ke 4 → nomor A-4xx.
  4. Tutup & buka ulang dokumen → urutan, kunci, preferensi sama.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Perbaiki sisa. **Step 4:** Seluruh suite Core + Revit → PASS.
- [ ] **Step 5:** README: install opt-in, tiga sistem, tabel pintasan keyboard, format CSV, profil (PUPR/UDS, impor JSON), strategi penomoran, batasan (tampak/potongan dibuat sebagai drafting placeholder; legend butuh minimal satu legend di proyek; satu-Undo dicek manual).
- [ ] **Step 6:** Commit `feat: Sheet Studio siap uji pengguna`.
