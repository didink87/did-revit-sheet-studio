# Sheet Studio — Stabil & Ringan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menghilangkan tiga bug laporan pengguna — (1) panel lama memuat, (2) lembar yang baru ditambah kadang tidak muncul, (3) UI terasa berat — tanpa mengubah perilaku penomoran/terapkan.

**Architecture:** Semua pemuatan ulang lewat satu koordinator di `StudioViewModel` (single-flight + coalescing, sadar pane tersembunyi). Antrean ExternalEvent menguras banyak pekerjaan per event. Baris panel diperbarui inkremental (instans dipakai ulang, `ObservableCollection` disinkron minimal) sehingga WPF tidak membangun ulang semua kontainer. Thumbnail keluar dari jalur kritis. Log diagnostik bertimestamp agar sisa masalah bisa dibuktikan, bukan ditebak.

**Tech Stack:** C# 12, .NET 8, Revit 2026 API (Nice3point 2026.*), WPF, xUnit (Core), NUnit + ricaun.RevitTest (Revit).

**Spec:** `docs/superpowers/specs/2026-11-03-sheet-studio-design.md` (perilaku fitur, tidak berubah) + bagian **Diagnosa** di bawah (spesifikasi perbaikan ini).

## Diagnosa (dari pembacaan kode, 2026-11-03)

| Gejala | Penyebab di kode |
|---|---|
| Lama memuat | `ShowStudioCommand` **dan** `StudioPane.IsVisibleChanged` sama-sama memanggil `OnShownAsync` → dua snapshot penuh per buka. Tiap tampil/aktif-view/perubahan model memuat ulang walau pane tersembunyi. `TitleBlocks.On(s)` membuat satu `FilteredElementCollector` **per lembar**. `CreateAsync`/`DuplicateAsync` membaca snapshot penuh hanya untuk `Prefs`. `ExternalEventQueue.Execute` menjalankan **satu** item per siklus idle Revit. Add-in terpasang dari `bin\Debug`. |
| Lembar baru tidak muncul | `ReloadKeepingChangesAsync` mengambil `pending = Current` **sebelum** `await` → beberapa reload bersamaan (watcher + view-activated + after-create) saling timpa. Bila `CreateAsync` melempar galat, `AfterCreateAsync` tidak pernah jalan padahal sebagian chunk sudah ter-commit (watcher mengabaikan transaksi "Sheet Studio"). Lembar baru bisa tersembunyi oleh **Filter** aktif atau grup yang **dilipat**, dan panel tidak menggulir ke sana. Save As dokumen mengubah `DocumentKey` (berbasis path) → bila ada perubahan tertunda, panel membeku di snapshot lama. |
| UI berat | Tiap perubahan: `DisplaySheets()` dihitung 3× (Recompute, Rebuild, Inspector.Refresh), `LayerListBuilder.Build` 2×, lalu **semua** `LayerRowViewModel` dibuat baru → ListBox membuang semua kontainer. Thumbnail: `ExportImage` di thread Revit per baris, dipicu tiap `ScrollChanged` (O(n) `ContainerFromItem` per tick), cache dikunci nomor+judul sehingga renomor = ekspor ulang (457 file di cache). Glyph emoji per baris memicu font fallback. |

## Global Constraints

- Repo `D:\Library\AI\Plugin Revit\Sheet Studio`; buat branch `fix/stabil` dari `feat/sheet-studio` sebelum Task 1.
- Revit 2026, `net8.0` (Core) / `net8.0-windows` (Revit), LangVersion 12, `TreatWarningsAsErrors` tetap aktif.
- Tes Revit hanya lewat ricaun (`NUnit.Open/Close`): **jangan** attach/tutup/kill Revit milik user; tanpa `Stop-Process` by name.
- Tes ricaun yang memanggil Revit API harus **sinkron** (bukan `async Task`); binding WPF di ricaun butuh dispatcher pump.
- Revit menolak `~ { } [ ] | ; < > ? \`` di nomor/nama lembar.
- Teks UI dan pesan status berbahasa Indonesia, gaya yang sudah ada.
- Perilaku penomoran, Terapkan (satu langkah Undo), dan rulings di `2026-11-03-sheet-studio-ledger.md` tidak berubah.
- Perintah tes: `dotnet test tests/SheetStudio.Core.Tests` dan `dotnet test tests/SheetStudio.Revit.Tests`.

## Review Focus

1. **Proyek besar (500+ lembar)** — satu staged edit harus tetap terasa instan → tes `Push_500Sheets_Under300ms` di Task 6.
2. **Lembar ditambah lewat Revit native saat pane tersembunyi (tab di belakang Project Browser)** — saat pane ditampilkan, lembar harus ada → tes `ModelChanged_WhileHidden_LoadsOnShow` di Task 3.
3. **Edit di panel saat reload sedang berjalan** — edit tidak boleh hilang → tes `Reload_EditDuringLoad_IsKept` di Task 3.
4. **Pembuatan gagal di tengah batch (exception)** — lembar yang sudah ter-commit tetap muncul → tes `Create_GatewayThrows_StillReloads` di Task 3.
5. **Save As dokumen belum tersimpan dengan perubahan tertunda** — panel tetap mengikuti dokumen yang sama → tes `Reload_SameDocumentNewPath_AdoptsKey` di Task 4.

---

### Task 1: Log diagnostik

**Files:**
- Create: `src/SheetStudio.Core/Diagnostics/StudioLog.cs`
- Create: `src/SheetStudio.Revit/Diagnostics/FileLogSink.cs`
- Modify: `src/SheetStudio.Revit/App.cs` (pasang sink, ukur `OnStartup`)
- Modify: `src/SheetStudio.Revit/Revit/RevitStudioGateway.cs`, `RevitStudioGateway.Create.cs`, `ModelWatcher.cs` (instrumentasi)
- Test: `tests/SheetStudio.Core.Tests/StudioLogTests.cs`

**Interfaces:**
- Produces:
  - `public static class StudioLog { public static Action<string>? Sink { get; set; } public static void Write(string evt, string detail = ""); public static IDisposable Time(string evt, string detail = ""); }`
  - Format baris: `HH:mm:ss.fff [evt] detail` dan untuk `Time`: `HH:mm:ss.fff [evt] detail (123 ms)`. `Write`/`Time` tidak pernah melempar, juga bila `Sink` null atau melempar.
  - `public sealed class FileLogSink(string dir) { public void Write(string line); }` — file `sheetstudio-yyyyMMdd.log` di `%LocalAppData%\SheetStudio\log\`, `lock` per instans, galat IO ditelan.

- [ ] **Step 1: Tes gagal** — `StudioLogTests` (tandai `[Collection("StudioLog")]` karena `Sink` statis):
  ```csharp
  [Fact] public void Time_WritesEventDetailAndElapsed()
  {
      var lines = new List<string>(); StudioLog.Sink = lines.Add;
      using (StudioLog.Time("load", "12 lembar")) Thread.Sleep(20);
      StudioLog.Sink = null;
      Assert.Matches(@"^\d\d:\d\d:\d\d\.\d{3} \[load\] 12 lembar \(\d+ ms\)$", Assert.Single(lines));
  }
  [Fact] public void Write_SinkThrowsOrNull_DoesNotThrow()
  {
      StudioLog.Sink = null; StudioLog.Write("x");
      StudioLog.Sink = _ => throw new IOException(); StudioLog.Write("x");
      StudioLog.Sink = null;
  }
  ```
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests --filter StudioLogTests` → FAIL (tipe belum ada).
- [ ] **Step 3: Implementasi** `StudioLog` + `FileLogSink`. Pasang di `App.OnStartup`: `StudioLog.Sink = new FileLogSink(dir).Write;` dan bungkus isi `OnStartup` dengan `StudioLog.Time("startup")`. Instrumentasi: `RevitStudioGateway.Load` → `Time("load", $"{n} lembar")` (n diisi setelah baca; gunakan `Write` di akhir bila lebih mudah); `CreateAsync` → `Time("create", $"{reqs.Count} permintaan")`; `ApplyAsync` → `Time("apply")`; `ThumbnailAsync` → `Time("thumb", uniqueId)`; `ModelWatcher.Raise` → `Write("watcher", "berubah")`.
- [ ] **Step 4:** `dotnet test tests/SheetStudio.Core.Tests` → PASS; `dotnet build src/SheetStudio.Revit` → 0 warning.
- [ ] **Step 5: Commit** — `git commit -m "feat: log diagnostik Sheet Studio (%LocalAppData%\SheetStudio\log)"`

---

### Task 2: Antrean ExternalEvent menguras banyak pekerjaan

**Files:**
- Create: `src/SheetStudio.Core/Threading/WorkQueue.cs`
- Modify: `src/SheetStudio.Revit/Revit/ExternalEventQueue.cs`
- Test: `tests/SheetStudio.Core.Tests/WorkQueueTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class WorkQueue<TCtx>(Func<long>? clockMs = null)` — default clock `Environment.TickCount64`.
  - `public Task<T> Enqueue<T>(Func<TCtx, T> work)` — TCS dengan `RunContinuationsAsynchronously`; exception → task faulted.
  - `public int Drain(TCtx ctx, int budgetMs)` — jalankan item FIFO sampai kosong atau waktu ≥ budget; **selalu minimal satu** item bila ada; kembalikan jumlah yang dijalankan.
  - `public bool IsEmpty { get; }`
  - `ExternalEventQueue` memakai `WorkQueue<UIApplication>`; `Execute` → `Drain(app, 40)`, lalu `Raise()` bila masih ada. Hasil `_event.Raise()` selain `ExternalEventRequest.Accepted` di-log `StudioLog.Write("event", result.ToString())`.

- [ ] **Step 1: Tes gagal** — `WorkQueueTests`:
  - `Drain_RunsAllWithinBudget`: 3 item, clock tetap → `Drain` = 3, ketiga task selesai dengan hasilnya, `IsEmpty`.
  - `Drain_StopsAtBudget_RunsAtLeastOne`: clock maju 50 tiap panggilan; budget 40 → `Drain` = 1, `IsEmpty == false`; Drain kedua menjalankan item berikut.
  - `Enqueue_Exception_FaultsTaskAndContinues`: item 1 melempar, item 2 mengembalikan 7 → task 1 `IsFaulted`, task 2 = 7.
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests --filter WorkQueueTests` → FAIL.
- [ ] **Step 3: Implementasi** `WorkQueue` (`ConcurrentQueue<Action<TCtx>>`), lalu ubah `ExternalEventQueue` agar memakai `WorkQueue` (API publik `Run<T>`/`Run` tetap).
- [ ] **Step 4:** Core tests PASS; `dotnet test tests/SheetStudio.Revit.Tests` → PASS (regresi E2E).
- [ ] **Step 5: Commit** — `git commit -m "perf: ExternalEvent menguras antrean per siklus idle"`

---

### Task 3: Koordinator reload (single-flight, sadar pane tersembunyi, aman saat gagal buat)

**Files:**
- Modify: `src/SheetStudio.Core/ViewModels/StudioViewModel.cs`
- Modify: `src/SheetStudio.Core/ViewModels/CreateSheetViewModel.cs:123-129`, `BatchViewModel.cs:~138`, `InspectorViewModel.cs:200-204` (pakai `CreateThenReloadAsync`)
- Modify: `src/SheetStudio.Revit/ShowStudioCommand.cs` (hapus panggilan `OnShownAsync`), `src/SheetStudio.Revit/Ui/StudioPane.xaml.cs` (set `IsPaneVisible`), `src/SheetStudio.Revit/App.cs` (ViewActivated → `ReloadAsync(ReloadReason.GantiDokumen)`)
- Modify: `tests/SheetStudio.Core.Tests/FakeGateway.cs` (tambah `LoadGate`, `CreateError`)
- Test: `tests/SheetStudio.Core.Tests/ReloadTests.cs`

**Interfaces:**
- Consumes: `StudioLog.Write` (Task 1).
- Produces (di `StudioViewModel`):
  - `public enum ReloadReason { Tampil, ModelBerubah, GantiDokumen, SetelahBuat }` (Core, file yang sama).
  - `public Task ReloadAsync(ReloadReason reason, string? statusIfOk = null)` — bila reload sedang berjalan: tandai "ulangi", simpan `statusIfOk` terakhir, kembalikan task yang selesai setelah putaran ulang. Maks. satu putaran tambahan per rentetan pemicu.
  - Satu putaran: `fresh = await _gateway.LoadAsync()` **lalu** baru `pending = Current` dan rebase (memperbaiki edit yang hilang). Logika beda-dokumen dan pesan status sama seperti `ReloadKeepingChangesAsync` sekarang (ganti fungsi itu).
  - `public bool IsPaneVisible { get; set; } = true;` dan `public bool IsStale { get; private set; }` — `ModelBerubah`/`GantiDokumen` saat `!IsPaneVisible` → `IsStale = true`, tanpa load. `Tampil` → load hanya bila belum pernah load (`DocumentKey == ""`) atau `IsStale`.
  - `OnShownAsync()` → `ReloadAsync(Tampil)` (galat jadi `Status`); `OnModelChangedAsync()` → `ReloadAsync(ModelBerubah)`; `AfterCreateAsync` memakai `ReloadAsync(SetelahBuat, pesan)`.
  - `public async Task<CreateReport> CreateThenReloadAsync(Func<Task<CreateReport>> run)` — sukses → `AfterCreateAsync(report)`, kembalikan report; exception → `await ReloadAsync(SetelahBuat)` lalu lempar ulang.
  - FakeGateway: `public TaskCompletionSource? LoadGate { get; set; }` (LoadAsync menunggu gate bila ada, **setelah** menyalin `Sheets` saat gate dilepas), `public string? CreateError { get; set; }` (CreateAsync melempar `InvalidOperationException(CreateError)`).

- [ ] **Step 1: Tes gagal** — `ReloadTests` (setup sama dengan `StudioViewModelTests.Loaded()`):
  - `Reload_ConcurrentTriggers_CoalesceToTwoLoads`: `gw.LoadGate = new()`; `loadsBefore = gw.Loads`; panggil `ReloadAsync(ModelBerubah)` 3× tanpa await; `gw.LoadGate.SetResult()`; `await Task.WhenAll(...)` → `gw.Loads - loadsBefore == 2`.
  - `Reload_EditDuringLoad_IsKept`: gate; `var t = vm.ReloadAsync(ModelBerubah)`; `vm.Layers.Click("L1"); vm.Layers.MarkDelete();` tambah `S("L3","A-112","DENAH LANTAI 3")` ke `gw.Sheets`; lepas gate; `await t` → `vm.PendingCount == 1`, `vm.Layers.Rows` berisi key `"L3"`.
  - `ModelChanged_WhileHidden_LoadsOnShow`: `vm.IsPaneVisible = false`; tambah L3; `gw.RaiseModelChanged()` → `gw.Loads` tidak bertambah, `vm.IsStale`; `vm.IsPaneVisible = true; await vm.OnShownAsync()` → baris `"L3"` ada, `!vm.IsStale`.
  - `Show_NotStale_DoesNotLoad`: setelah load, `await vm.OnShownAsync()` → `gw.Loads` tidak bertambah.
  - `Create_GatewayThrows_StillReloads`: `gw.CreateError = "putus"`; tambah L3 ke `gw.Sheets`; `await Assert.ThrowsAsync<InvalidOperationException>(() => vm.CreateThenReloadAsync(() => gw.CreateAsync([], new Progress<(int,int,string)>(), default)))` → baris `"L3"` ada.
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests --filter ReloadTests` → FAIL.
- [ ] **Step 3: Implementasi** di `StudioViewModel`; ubah ketiga pemanggil buat/duplikat ke `CreateThenReloadAsync`. Revit: `ShowStudioCommand` hanya `Show/Hide`; `StudioPane.IsVisibleChanged` → `vm.IsPaneVisible = (bool)e.NewValue; if (true) await vm.OnShownAsync();`; `App.ViewActivated` (kondisi beda-dokumen tetap) → `ViewModel.ReloadAsync(ReloadReason.GantiDokumen)` dengan try/catch ke `ReportError` (hapus cek `pane.IsVisible`, koordinator yang menangani).
- [ ] **Step 4:** Seluruh Core tests PASS (termasuk `StudioViewModelTests` lama); `dotnet build src/SheetStudio.Revit` bersih.
- [ ] **Step 5: Commit** — `git commit -m "fix: koordinator reload — tidak ada load ganda, edit tak hilang, lembar muncul walau buat gagal"`

---

### Task 4: Identitas dokumen tahan Save As

**Files:**
- Modify: `src/SheetStudio.Core/ViewModels/IStudioGateway.cs` (atau file tempat `StudioSnapshot` didefinisikan)
- Modify: `src/SheetStudio.Revit/Revit/SnapshotReader.cs:41`
- Modify: `src/SheetStudio.Core/ViewModels/StudioViewModel.cs` (putaran reload dari Task 3)
- Modify: `tests/SheetStudio.Core.Tests/FakeGateway.cs` (tambah `DocumentId`)
- Test: `tests/SheetStudio.Core.Tests/ReloadTests.cs`, `tests/SheetStudio.Revit.Tests/SnapshotTests.cs`

**Interfaces:**
- Consumes: `ReloadAsync` (Task 3).
- Produces:
  - `StudioSnapshot` mendapat `public string DocumentId { get; init; } = "";` (init-only agar konstruktor posisi yang ada tidak berubah).
  - `SnapshotReader.Read` mengisi `DocumentId = doc.ProjectInformation?.UniqueId ?? ""`.
  - Aturan di putaran reload: `fresh.DocumentKey != Snapshot.DocumentKey` **tetapi** `fresh.DocumentId != ""` dan sama dengan `Snapshot.DocumentId` → dokumen yang sama (Save As): perlakukan sebagai reload biasa (rebase, perubahan tertunda dipertahankan) dan `Snapshot = fresh` (kunci baru dipakai `BuildPlan`).

- [ ] **Step 1: Tes gagal**
  - Core `Reload_SameDocumentNewPath_AdoptsKey`: `gw.DocumentId = "P1"`; load; stage satu perubahan; `gw.DocumentKey = "C:\\baru.rvt"`; tambah L3; `await vm.ReloadAsync(ModelBerubah)` → `vm.Snapshot.DocumentKey == "C:\\baru.rvt"`, `PendingCount == 1`, baris `"L3"` ada, `vm.BuildPlan().DocumentKey == "C:\\baru.rvt"`.
  - Core `Reload_DifferentDocument_StillKeepsPendingAndWarns`: `DocumentId` beda + perubahan tertunda → snapshot lama tetap, `Status` berisi `"milik dokumen lain"` (perilaku lama).
  - Revit `Snapshot_DocumentId_IsProjectInfoUniqueId` (sinkron): `Read(doc, ...)` → `DocumentId == doc.ProjectInformation.UniqueId` dan tidak kosong.
- [ ] **Step 2:** Jalankan kedua filter → FAIL.
- [ ] **Step 3: Implementasi** sesuai Interfaces.
- [ ] **Step 4:** Core + `dotnet test tests/SheetStudio.Revit.Tests --filter SnapshotTests` → PASS.
- [ ] **Step 5: Commit** — `git commit -m "fix: panel tetap mengikuti dokumen setelah Save As"`

---

### Task 5: Lembar baru selalu terlihat (filter, lipat, gulir)

**Files:**
- Modify: `src/SheetStudio.Core/ViewModels/LayersViewModel.cs`
- Modify: `src/SheetStudio.Core/ViewModels/StudioViewModel.cs` (`AfterCreateAsync`)
- Modify: `src/SheetStudio.Revit/Ui/LayersPanel.xaml.cs` (berlangganan event gulir)
- Test: `tests/SheetStudio.Core.Tests/StudioViewModelTests.cs`

**Interfaces:**
- Consumes: `AfterCreateAsync` / `ReloadAsync` (Task 3).
- Produces (di `LayersViewModel`):
  - `public void Reveal(IReadOnlyCollection<string> sheetIds)` — id yang tidak ada di snapshot diabaikan. Bila ada id yang tidak lolos `Filter` → `Filter = ""`. Buka (hapus dari `_collapsed`) grup disiplin dan tipe yang memuat id tersebut. Pilih semuanya (`Selection.Click` id pertama menurut urutan panel, `CtrlClick` sisanya). Picu `ScrollIntoViewRequested` dengan id pertama menurut urutan panel.
  - `public event Action<string>? ScrollIntoViewRequested;`
  - `AfterCreateAsync` memanggil `Layers.Reveal(...)` dengan `UniqueId` baris yang `Ok` setelah reload selesai.
  - `LayersPanel`: pada `DataContextChanged`, berlangganan `ScrollIntoViewRequested` → cari `LayerRowViewModel` dengan key tersebut di `Rows`, lalu `LayerList.ScrollIntoView(row)` via `Dispatcher.BeginInvoke` (prioritas `Loaded`).

- [ ] **Step 1: Tes gagal** — `AfterCreate_RevealsNewSheet_ClearsFilterExpandsSelects`: `vm.Layers.Filter = "TAMPAK"`; `vm.Layers.ToggleCollapse("A|1", all: false)`; `string? scrolled = null; vm.Layers.ScrollIntoViewRequested += k => scrolled = k;` tambah `S("new-A-112","A-112","DENAH LANTAI 3")` ke `gw.Sheets`; `await vm.AfterCreateAsync(new CreateReport([("DENAH LANTAI 3", true, null, "new-A-112")], false))` → `vm.Layers.Filter == ""`, `Rows` memuat `"new-A-112"`, `vm.Layers.SelectedSheetIds()` = `["new-A-112"]`, `scrolled == "new-A-112"`.
  - `AfterCreate_FilterStillMatches_IsKept`: `Filter = "DENAH"` → setelah create lembar "DENAH LANTAI 3", `Filter` tetap `"DENAH"`.
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests --filter AfterCreate_` → FAIL.
- [ ] **Step 3: Implementasi** sesuai Interfaces.
- [ ] **Step 4:** Core tests PASS; build Revit bersih.
- [ ] **Step 5: Commit** — `git commit -m "fix: lembar baru dibuka, dipilih, dan digulir ke tampilan"`

---

### Task 6: Pembaruan baris inkremental (UI ringan)

**Files:**
- Create: `src/SheetStudio.Core/ViewModels/CollectionSync.cs`
- Modify: `src/SheetStudio.Core/Layers/LayerListBuilder.cs` (tambah `Collapse`)
- Modify: `src/SheetStudio.Core/ViewModels/LayerRowViewModel.cs`, `LayersViewModel.cs` (`Rebuild`, `Rows`), `StudioViewModel.cs` (cache `DisplaySheets`)
- Modify: `src/SheetStudio.Revit/Ui/LayersPanel.xaml` (glyph ikon)
- Test: `tests/SheetStudio.Core.Tests/CollectionSyncTests.cs`, `LayerListTests.cs`, `StudioViewModelTests.cs`

**Interfaces:**
- Produces:
  - `public static class CollectionSync { public static void Apply<T>(ObservableCollection<T> target, IReadOnlyList<T> desired) where T : class; }` — kesamaan **referensi**; hapus item yang tidak ada di `desired`, lalu per indeks `Move` bila item sudah ada di posisi lain atau `Insert` bila baru. Tanpa event bila sudah sama.
  - `public static IReadOnlyList<LayerRow> Collapse(IReadOnlyList<LayerRow> all, IReadOnlySet<string> collapsed)` di `LayerListBuilder` — hasil **identik** dengan `Build(..., collapsed, filter)` untuk input yang sama (header yang dilipat → `Expanded = false`, anak-anaknya dibuang).
  - `LayerRowViewModel`: konstruktor tetap `(LayerRow row)`; `Row` dan semua properti `init` (`Number`, `NewNumber`, `WarningText`, `Edited`, `Deleted`, `Borrowed`, `BorrowedBy`, `InSheetList`, `IsNew`) jadi setter yang memanggil `Set(...)`; setter `Row` me-raise semua properti turunan (`Label`, `Count`, `Locked`, `Expanded`, `Kind`, `Indent`, `NumberChanged`, `NumberTooltip`, `Warning`). Properti lain tetap.
  - `LayersViewModel.Rows` bertipe `ObservableCollection<LayerRowViewModel>` (dibuat sekali, tidak pernah diganti). `Rebuild()`: `_allRows = Build(..., ∅, filter)` sekali; `visible = Collapse(_allRows, _collapsed)`; pakai ulang VM berdasarkan key dari `Dictionary<string, LayerRowViewModel>`, perbarui propertinya, lalu `CollectionSync.Apply(Rows, desired)`; VM yang tak terpakai dibuang dari kamus.
  - `StudioViewModel.DisplaySheets()` mengembalikan hasil cache yang dihitung sekali di `Recompute()` (dan di `SavePrefsAsync` lewat `Recompute`). `HasBlockingErrors`, `LayersViewModel.Rebuild`, dan `InspectorViewModel.Refresh` memakai cache yang sama.
  - XAML: ganti teks emoji dengan `FontFamily="Segoe MDL2 Assets"`: mata `&#xE7B3;`, gembok `&#xE72E;`, peringatan `&#xE7BA;`, edit `&#xE70F;`, tautan `&#xE71B;`, bintang `&#xE735;`.

- [ ] **Step 1: Tes gagal**
  - `CollectionSyncTests.Apply_ReorderInsertRemove_MatchesDesiredKeepsInstances`: target `[a,b,c,d]`, desired `[d,a,e,c]` → urutan sama dengan desired, `a`/`c`/`d` instans yang sama.
  - `CollectionSyncTests.Apply_Unchanged_RaisesNoEvents`: hitung `CollectionChanged` → 0.
  - `LayerListTests.Collapse_EqualsBuildWithCollapsed`: data uji yang ada, untuk himpunan lipat `{}`, `{"A"}`, `{"A|1"}`, `{"A","A|2"}` → `Assert.Equal(Build(s,st,p,c,null), Collapse(Build(s,st,p,∅,null), c))` (record equality).
  - `StudioViewModelTests.Rebuild_AfterRename_ReusesRowInstances`: simpan `var l2 = Row(vm,"L2")`; `vm.Layers.RenameInline("L1","DENAH BARU")` → `ReferenceEquals(l2, Row(vm,"L2"))` dan `Row(vm,"L1").Label == "DENAH BARU"`.
  - `StudioViewModelTests.Push_500Sheets_Under300ms`: 500 lembar `A-1xx`…`A-5xx` lintas grup tipe (pakai `S(...)`), load, lalu ukur `Stopwatch` untuk `vm.Layers.Click(id); vm.Layers.MoveDown.Execute(null);` → < 300 ms (lakukan satu pemanasan dulu).
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests --filter "CollectionSyncTests|Collapse_|ReusesRowInstances|Push_500"` → FAIL.
- [ ] **Step 3: Implementasi** sesuai Interfaces (urutan: `CollectionSync` → `Collapse` → cache `DisplaySheets` → VM baris yang bisa diubah → `Rebuild`). Pastikan `IsSelected` dan `Thumbnail` dari VM yang dipakai ulang tetap benar setelah `Rebuild`.
- [ ] **Step 4:** Seluruh Core tests PASS; `dotnet test tests/SheetStudio.Revit.Tests --filter UiSmokeTests` → PASS.
- [ ] **Step 5: Commit** — `git commit -m "perf: baris panel diperbarui inkremental, DisplaySheets dihitung sekali"`

---

### Task 7: Snapshot & thumbnail cepat (sisi Revit)

**Files:**
- Modify: `src/SheetStudio.Revit/Revit/TitleBlocks.cs`, `SnapshotReader.cs`
- Modify: `src/SheetStudio.Revit/Revit/RevitStudioGateway.cs`, `RevitStudioGateway.Create.cs`
- Modify: `src/SheetStudio.Revit/Revit/ThumbnailService.cs`
- Modify: `src/SheetStudio.Core/Preferences/Prefs.cs` (default `Thumbnail = false`)
- Modify: `src/SheetStudio.Core/ViewModels/LayersViewModel.cs` (`LoadThumbnailsAsync`)
- Modify: `src/SheetStudio.Revit/Ui/LayersPanel.xaml.cs` (`OnScrollChanged`)
- Modify: `tests/SheetStudio.Core.Tests/FakeGateway.cs` (tambah `ThumbGate`, `ThumbRequests`)
- Test: `tests/SheetStudio.Revit.Tests/SnapshotTests.cs`, `CreateTests.cs`; `tests/SheetStudio.Core.Tests/PreferencesTests.cs`, `StudioViewModelTests.cs`

**Interfaces:**
- Produces:
  - `internal static Dictionary<ElementId, FamilyInstance> TitleBlocks.ByOwner(Document doc)` — satu collector `OST_TitleBlocks`/`FamilyInstance`, instans pertama per `OwnerViewId`. `SnapshotReader` memakai kamus ini (hapus pemakaian `TitleBlocks.On` di jalur snapshot).
  - `public static Prefs RevitStudioGateway.LoadPrefs(Document doc)` — gabungan bawaan < file user < storage proyek; `Load` memakainya; `CreateAsync` dan `DuplicateAsync` memakai `LoadPrefs` alih-alih `Load(doc).Prefs`.
  - `ThumbnailService`: versi cache = hash dari `TitleBlock symbol id` + `string.Join(",", sheet.GetAllPlacedViews().OrderBy(id => id.Value))` (bukan nomor/judul). Nama file `{uniqueId}-{version}.png` tetap.
  - Core `LayersViewModel`: cache thumbnail dikunci `UniqueId` saja (bukan `|Versi`); `_thumbRequested` dikosongkan di setiap reload. `LoadThumbnailsAsync(keys)` menaikkan generasi; loop lama berhenti sebelum meminta key berikutnya bila generasinya sudah lewat.
  - `LayersPanel.OnScrollChanged`: debounce `DispatcherTimer` 250 ms; kumpulkan key hanya dari kontainer yang **ter-realize** (anak `VirtualizingStackPanel` yang `IsVisible` dan berpotongan dengan viewport `ScrollViewer`), bukan loop seluruh `Rows`.
  - FakeGateway: `public TaskCompletionSource? ThumbGate { get; set; }`, `public List<string> ThumbRequests { get; } = [];` (dicatat sebelum menunggu gate).

- [ ] **Step 1: Tes gagal**
  - Revit `Snapshot_TitleBlockPerSheet_FromSingleCollector` (sinkron): dua lembar dengan title block A3 dan A2 (`Fixtures.LoadTitleBlock`) → `TitleBlockType` masing-masing sesuai miliknya.
  - Revit `Gateway_LoadPrefs_EqualsLoadPrefs`: `RevitStudioGateway.LoadPrefs(doc) == RevitStudioGateway.Load(doc).Prefs`.
  - Revit `Thumbnail_RenumberKeepsCache`: render → ubah `SheetNumber` dalam transaksi → render lagi → jumlah file `*.png` di cache dir tidak bertambah.
  - Core `PreferencesTests.Defaults_MatchSpec`: ubah ekspektasi `Thumbnail` jadi `false`.
  - Core `Thumbnails_NewRequestSupersedesOld`: `gw.Thumb = [1]; gw.ThumbGate = new()`; `var a = vm.Layers.LoadThumbnailsAsync(["L1","L2"])`; `var b = vm.Layers.LoadThumbnailsAsync(["T1"])`; lepas gate; `await Task.WhenAll(a,b)` → `gw.ThumbRequests` = `["L1","T1"]`.
- [ ] **Step 2:** Jalankan filter terkait (Core dan Revit) → FAIL.
- [ ] **Step 3: Implementasi** sesuai Interfaces.
- [ ] **Step 4:** Core tests PASS; `dotnet test tests/SheetStudio.Revit.Tests` → PASS (semua, regresi penuh).
- [ ] **Step 5: Commit** — `git commit -m "perf: snapshot satu collector title block, thumbnail opsional & tidak diekspor ulang saat renomor"`

---

### Task 8: Pasang Release + dokumentasi + verifikasi manual

**Files:**
- Modify: `src/SheetStudio.Revit/SheetStudio.Revit.csproj` (target `InstallAddin`)
- Modify: `README.md` (instal, diagnosa, batasan)

**Interfaces:**
- Produces: target `InstallAddin` memunculkan `<Warning Text="InstallAddin dari konfigurasi Debug: add-in lebih lambat. Pakai -c Release." Condition="'$(Configuration)' == 'Debug'" />`. README: perintah instal jadi `dotnet build src/SheetStudio.Revit -c Release -p:InstallAddin=true`; bagian baru **Diagnosa** (lokasi log `%LocalAppData%\SheetStudio\log\`, apa yang dicatat); Batasan: thumbnail mati secara bawaan (aktifkan di ⚙).

- [ ] **Step 1:** Ubah csproj + README.
- [ ] **Step 2:** `dotnet test tests/SheetStudio.Core.Tests` dan `dotnet test tests/SheetStudio.Revit.Tests` → semua PASS (tempel ringkasan hitungan).
- [ ] **Step 3:** **Revit milik user harus ditutup dulu** (tanya user, jangan tutup sendiri). `dotnet build src/SheetStudio.Revit -c Release -p:InstallAddin=true` → `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio.addin` menunjuk ke `bin\Release\SheetStudio.Revit.dll`.
- [ ] **Step 4: Commit** — `git commit -m "chore: instal Release, README diagnosa"`
- [ ] **Step 5: Checklist manual bersama user** (hasil dicatat di ledger):
  1. Buka panel di proyek nyata → log `load` hanya **sekali**, durasinya dicatat.
  2. ＋ Baru dengan filter aktif dan grup terlipat → lembar baru muncul, terpilih, tergulir ke tampilan.
  3. Batch 10 lembar → semua muncul; Batal di tengah → model kembali, panel sinkron.
  4. Tambah lembar lewat Revit native saat pane di tab belakang → saat tab panel dibuka, lembar ada.
  5. Drag-drop dan Ctrl+]/[ di proyek terbesar user → tidak ada jeda terasa.
  6. Save As proyek belum tersimpan dengan perubahan tertunda → panel tetap dan Terapkan berhasil.
  7. Sisa checklist lama: tema gelap, Ctrl+Z di pane, satu Undo Revit per Terapkan.
