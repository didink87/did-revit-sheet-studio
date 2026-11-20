# Sheet Studio v1.1 — Fill Indicator + English UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** (1) every sheet row shows a hollow circle that is filled Revit blue when the sheet already holds views; clicking it lists those views; (2) the whole UI speaks English.

**Architecture:** The snapshot (`SnapshotReader` → `SheetSnapshot`) gains a list of placed views per sheet; `LayerRowViewModel` exposes it and `LayersPanel.xaml` draws the circle + a `Popup`. `ModelWatcher` also reacts to viewport/schedule placement so the circle stays current. English is a string pass over Core view models, XAML, dialogs, transaction names, ribbon, README and setup scripts. The staged edit → APPLY workflow is unchanged.

**Tech Stack:** C# / .NET 8 (Core, xUnit), Revit 2026 API + WPF (Revit project, ricaun NUnit tests).

**Spec:** user request 2026-11-20 (chat): "indicator: a hollow circle, filled with Revit blue when the sheet has views in it; click it → list of the views in that sheet. Change everything to English." (Auto-apply / no-confirm editing was requested and then cancelled — out of scope.)

**Branch:** `feat/v1.1` from `main` (30cdf3f). Release as `v1.1.0`.

## Global Constraints

- Revit blue = `#0696D7` (same value in Light and Dark theme), brush key `SS.RevitBlue`.
- Circle: 10×10 px `Ellipse`, stroke 1.5 px. Empty sheet → stroke `SS.FgMuted`, fill `Transparent`. Sheet with ≥1 view/schedule → stroke + fill `SS.RevitBlue`.
- "Has content" = at least one `Viewport` **or** one `ScheduleSheetInstance` that is not a revision schedule (`!IsTitleblockRevisionSchedule`). Title block alone = empty.
- The view list is read-only (no click-to-open).
- Edit workflow (staged changes, APPLY button, confirm preview) stays exactly as v1.0 — only its text becomes English.
- English everywhere the user reads: XAML text/tooltips/menus, status lines, MessageBoxes, warnings, transaction names, ribbon, README, Setup.bat/PS1 console output.
- Transaction names keep the prefix `"Sheet Studio"` (ModelWatcher ignores own commits by that prefix).
- NOT translated (data / compat): PUPR profile title vocabulary (DENAH, TAMPAK …), JSON preference keys (`Mulai`, `Langkah`, `KonfirmasiTerapkan` …), file name `judul-tersimpan.json`, C# identifiers/enum names, log file contents.
- Repo has mixed CRLF/LF — preserve per file.
- ricaun tests: rename `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio.addin` → `.addin.disabled` while running, restore after (only with no Revit open). Revit API tests synchronous.

## Review Focus

1. **View placed/removed on a sheet outside Sheet Studio** (drag view onto sheet in Revit, delete a viewport) → circle updates without F5. Tests: `Watcher_ViewportAdded_Raises`, `Watcher_ViewportDeleted_Raises` (Task 2).
2. **Sheet with only a schedule / only a revision schedule / only a legend** → schedule & legend count as content, revision schedule does not. Test: `Snapshot_PlacedViews_SchedulesCountRevisionDoesNot` (Task 2).
3. **Clicking the circle** must not select, drag, or rename the row. Test: `LayersPanel_FillDotClick_DoesNotChangeSelection` (Task 3).
4. **Row reused across Rebuild** (rows are recycled by key) → after a reload where a sheet gained a view, the same row VM shows `HasViews == true`. Test: `Row_Rebuild_UpdatesPlacedViewsInPlace` (Task 1).
5. **Big project load time** — reading placed views must not add a global collector per sheet. Check: log `[load]` duration on `repit` before/after stays within ~10% (Task 5 manual).

---

### Task 1: Core — placed views on snapshot and row

**Files:**
- Modify: `src/SheetStudio.Core/Model/SheetSnapshot.cs`
- Modify: `src/SheetStudio.Core/ViewModels/LayerRowViewModel.cs`
- Modify: `src/SheetStudio.Core/ViewModels/LayersViewModel.cs` (`Rebuild`, where rows get `InSheetList` set)
- Test: `tests/SheetStudio.Core.Tests/LayerListTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record PlacedView(string UniqueId, string Name, string Kind);` in `SheetSnapshot.cs` (`Kind` = Revit `ViewType` string, e.g. `"FloorPlan"`, `"Schedule"`).
  - `SheetSnapshot.PlacedViews { get; init; } : IReadOnlyList<PlacedView>` default `[]` (init property — positional ctor unchanged so existing tests compile).
  - `LayerRowViewModel.PlacedViews : IReadOnlyList<PlacedView>` (settable, raises `HasViews`, `ViewCountText`), `HasViews : bool` (false for group rows), `ViewCountText : string` = `"1 view"` / `"3 views"` / `"Empty sheet"`.

- [ ] **Step 1: Failing tests** in `LayerListTests.cs`:
  - `Row_SheetWithViews_HasViewsTrue`: sheet `PlacedViews = [new("v1","Level 1","FloorPlan"), new("v2","Sch","Schedule")]` → row `HasViews == true`, `PlacedViews.Count == 2`, `ViewCountText == "2 views"`.
  - `Row_EmptySheet_HasViewsFalse`: `PlacedViews = []` → `HasViews == false`, `ViewCountText == "Empty sheet"`.
  - `GroupRow_HasViewsFalse`.
  - `Row_Rebuild_UpdatesPlacedViewsInPlace`: load with empty sheet, keep row VM reference, reload snapshot where the sheet has 1 view → same instance, `HasViews == true`, `PropertyChanged` raised for `HasViews`.
- [ ] **Step 2: Run** `dotnet test tests/SheetStudio.Core.Tests --filter "FullyQualifiedName~LayerListTests"` → FAIL (members missing).
- [ ] **Step 3: Implement** the members; in `LayersViewModel.Rebuild` copy `display.PlacedViews` onto the row VM where `InSheetList` is copied.
- [ ] **Step 4: Run** filter → PASS; full `dotnet test tests/SheetStudio.Core.Tests` → all pass.
- [ ] **Step 5: Commit** `feat(core): placed views per sheet on snapshot and layer row`

### Task 2: Revit — read placed views, watch viewport changes

**Files:**
- Modify: `src/SheetStudio.Revit/Revit/SnapshotReader.cs` (`Sheet(...)`)
- Modify: `src/SheetStudio.Revit/Revit/ModelWatcher.cs`
- Test: `tests/SheetStudio.Revit.Tests/SnapshotTests.cs`, `tests/SheetStudio.Revit.Tests/WatcherTests.cs`

**Interfaces:**
- Consumes: `PlacedView`, `SheetSnapshot.PlacedViews` (Task 1).
- Produces: `internal static IReadOnlyList<PlacedView> SnapshotReader.PlacedViews(Document doc, ViewSheet s)`.

- [ ] **Step 1: Failing tests**
  - `Snapshot_PlacedViews_ListsViewportViews`: sheet + drafting view placed with `Viewport.Create` → `PlacedViews` has 1 item, `Name` = view name, `Kind == "DraftingView"`; second sheet without views → empty.
  - `Snapshot_PlacedViews_SchedulesCountRevisionDoesNot`: sheet with `ScheduleSheetInstance` of a new schedule → 1 item `Kind == "Schedule"`; sheet with only a title block (its revision schedule) → empty.
  - `Watcher_ViewportAdded_Raises`: watcher `debounceMs: 0`; place a viewport on an existing sheet in a transaction → `Changed` raised once.
  - `Watcher_ViewportDeleted_Raises`: delete that viewport → `Changed` raised.
- [ ] **Step 2: Run** Revit tests (addin disabled) → the four FAIL.
- [ ] **Step 3: Implement**
  - `PlacedViews`: `s.GetAllViewports()` → `Viewport.ViewId` → view; plus `new FilteredElementCollector(doc, s.Id).OfClass(typeof(ScheduleSheetInstance))` where `!IsTitleblockRevisionSchedule` → `ScheduleId`. Order by name. Set via `with { PlacedViews = ... }` in `Sheet(...)`.
  - `ModelWatcher._filter`: add `ElementClassFilter(typeof(Viewport))` and `ElementClassFilter(typeof(ScheduleSheetInstance))`. For deletions keep a per-doc known set of viewport + schedule-instance ids beside `_knownSheets` (same lazy-init pattern) so `GetDeletedElementIds().Any(known.Contains)` catches them.
- [ ] **Step 4: Run** all Revit tests → PASS (40 old + 4 new). Restore `.addin`.
- [ ] **Step 5: Commit** `feat(revit): read placed views and watch viewport/schedule changes`

### Task 3: UI — fill circle + view list popup

**Files:**
- Modify: `src/SheetStudio.Revit/Ui/LayersPanel.xaml` (row `DataTemplate`, new column), `LayersPanel.xaml.cs`
- Modify: `src/SheetStudio.Revit/Ui/Theme/Light.xaml`, `Theme/Dark.xaml` (add `SS.RevitBlue`)
- Test: `tests/SheetStudio.Revit.Tests/UiSmokeTests.cs`

**Interfaces:**
- Consumes: `LayerRowViewModel.HasViews / PlacedViews / ViewCountText` (Task 1).

- [ ] **Step 1: Failing tests**
  - `LayersPanel_FillCircle_BindsHasViews`: pane with one filled + one empty sheet, pump dispatcher; element `FillDot` in filled row has `Fill` = `SolidColorBrush #0696D7`, empty row `Fill` = `Transparent`.
  - `LayersPanel_FillDotClick_DoesNotChangeSelection`: raise click on `FillDot` toggle of an unselected row → popup `IsOpen == true`, `Layers.SelectedSheetIds()` unchanged, row `IsEditing == false`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement**
  - New first column (`Width="18"`) left of the eye, visible for sheet rows only (`IsGroup` + `NotVis`). `ToggleButton` (style `IconToggle`) containing `Ellipse x:Name="FillDot"` per Global Constraints; `DataTrigger` on `HasViews` sets Fill/Stroke. Tooltip = `ViewCountText`.
  - `Popup` (`StaysOpen="False"`, `Placement="Bottom"`, target = toggle, `IsOpen` ↔ toggle `IsChecked`): header `ViewCountText`, `ItemsControl` over `PlacedViews` showing `Name` + muted `Kind`; empty → "No views on this sheet". Panel colors from theme brushes.
  - Exclude the toggle in `OnRowMouseDown` the same way eye/lock buttons are excluded (no drag, no selection).
- [ ] **Step 4: Run** UiSmoke tests → PASS. Build Release; live Revit: empty vs filled circle, popup lists views, click outside closes.
- [ ] **Step 5: Commit** `feat(ui): sheet fill indicator with placed-view list`

### Task 4: English — all user-facing text

**Files:**
- Modify Core: user-readable strings in `StudioViewModel`, `LayersViewModel`, `InspectorViewModel`, `CreateSheetViewModel`, `BatchViewModel`, `NamePickerViewModel`, `PreferencesViewModel`, `BulkEdit`, `TitleRules` messages (not vocabulary), `Sequencer`/`NumberMap` errors, `BatchCsv` errors, `PreferencesStore` error.
- Modify Revit: all `src/SheetStudio.Revit/Ui/*.xaml` + `*.xaml.cs` (headers, menus, tooltips, MessageBoxes in `BatchWindow`, `CreateSheetWindow`, `PreviewWindow`), `StudioPane.StrategyLabel`, converters with text, `App.cs` ribbon text/tooltip, `ShowStudioCommand.cs`, user-facing errors + transaction names in `ApplyExecutor`, `CreateExecutor`, `ViewCreator`.
- Modify docs: `README.md` (+ "What's new in 1.1"), `setup/Setup.bat`, `setup/Install-SheetStudio.ps1`, `setup/Build-Release.ps1` console text.
- Test: Core + Revit tests that assert Indonesian strings.

- [ ] **Step 1: List** candidates: `grep -rnE '"[^"]*(lembar|nomor|judul|perubahan|dibuang|diterapkan|dilewati|gagal|dibuat|Galat|tidak|belum|kertas|kop|grup|tipe|Dokumen|Terapkan|Buang|Batal|Selesai)[^"]*"' src setup README.md` plus XAML `Content=|Header=|Text=|ToolTip=`. Put the list in the task report.
- [ ] **Step 2: Update tests first** to the English wording → run → FAIL.
- [ ] **Step 3: Translate** with this glossary (consistent wording):
  lembar → sheet · nomor → number · judul → title · kop → title block · kertas → paper · grup → group · tipe → type · perubahan tertunda → pending changes · diterapkan → applied · dilewati → skipped · dibuang → discarded · Galat → Error · TERAPKAN → APPLY · Buang → Discard · Pratinjau daftar → Preview list · Urungkan → Undo · Rapikan nomor → Renumber all · Penomoran: Rentang/Urut → Numbering: Range/Sequential · Kunci → Lock · Tampil di daftar gambar → Show in sheet list · Tandai hapus → Mark for deletion · Dipinjam → Borrowed by · Baru → New · Batal → Cancel · Selesai → Done · Hentikan → Stop · Preferensi → Preferences · Duplikat: Lembar saja / Dengan view / Dengan view + detailing → Duplicate: Sheet only / With views / With views + detailing · `LAPISAN — URUTAN LEMBAR` → `LAYERS — SHEET ORDER` · `INSPEKTUR` → `INSPECTOR`.
  Transaction names: `"Sheet Studio: Delete & temporary numbers"`, `"Sheet Studio: Apply sheet changes"`, `"Sheet Studio: Save sheet order"` (group name likewise English).
- [ ] **Step 4: Run** full Core + Revit tests → PASS; rerun Step 1 grep → only PUPR vocabulary / JSON keys / identifiers remain. Open every window in live Revit (New, Batch, Edit number, Preferences, Preview, Name picker, context menu) — no Indonesian left.
- [ ] **Step 5: Commit** `i18n: English UI, ribbon, README, setup`

### Task 5: Release v1.1.0

- [ ] **Step 1:** Full `dotnet test` Core + Revit green (counts in report).
- [ ] **Step 2:** Manual check on the user's project (`repit`): circles match sheets with views; popup correct; place a view on an empty sheet in Revit → circle fills within ~1 s without F5; `[load]` time in `%LocalAppData%\SheetStudio\log` within ~10% of v1.0.
- [ ] **Step 3:** Bump version to `1.1.0`, `setup/Build-Release.ps1 -Version 1.1.0`, merge `feat/v1.1` → `main`, tag, `gh release create v1.1.0` with the zip. Plan doc committed (repo is public). `backup-sheets-repit.csv` NOT committed.
