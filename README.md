# Sheet Studio — Revit 2026

A Revit plugin to **create, order and edit sheets** with UDS / US National CAD Standard numbering (Discipline Designator + Sheet Type Designator) and the Indonesian PUPR standard profile.

The UI and the reference documents are in Indonesian; labels quoted below are shown as they appear in the UI, with an English gloss where needed.

- Design: `docs/superpowers/specs/2026-11-03-sheet-studio-design.md`
- Naming rules: `docs/reference/02-penamaan-lembar-aia-ncs.md`
- Plan & decisions: `docs/superpowers/plans/2026-11-03-sheet-studio.md`

## Installation

Requires **Revit 2026** (Windows). Close Revit before installing or updating.

### Route 1 — Simple setup (no terminal)

1. Open the [Releases](https://github.com/didink87/did-revit-sheet-studio/releases) page and download `SheetStudio-<version>-Revit2026.zip`.
2. Extract **everything** in the ZIP into one folder.
3. Double-click **`Setup.bat`** → choose **[1] Pasang / perbarui** (Install / update).
4. Open Revit 2026 → **DID** tab → **Sheet Studio**. If the add-in security dialog appears, choose **Always Load**.

Setup copies the add-in to `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio\` and writes `SheetStudio.addin`. To uninstall: run `Setup.bat` → **[2] Copot** (Uninstall).

### Route 2 — Terminal (from source)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Git.

```
git clone https://github.com/didink87/did-revit-sheet-studio.git
cd did-revit-sheet-studio
dotnet build src/SheetStudio.Revit -c Release -p:InstallAddin=true
```

Copies `SheetStudio.addin` to `%AppData%\Autodesk\Revit\Addins\2026\` and points it at `bin\Release\SheetStudio.Revit.dll`. Use **Release**: Debug builds are slower (the target warns when installed from Debug). To update: `git pull`, then repeat the build command. To uninstall: delete that `.addin` file.

Both routes write the same manifest (`SheetStudio.addin`), so whichever route you ran last is the one in effect.

### Building a release package (developers)

```
powershell -ExecutionPolicy Bypass -File setup/Build-Release.ps1 -Version 1.0.0
```

Result: `dist/SheetStudio-1.0.0-Revit2026.zip` (contents: `bin\`, `Setup.bat`, `Install-SheetStudio.ps1`, `BACA-DULU.txt` ("read me first"), `PANDUAN.md` ("guide")).

Short user guide: [`docs/PANDUAN.md`](docs/PANDUAN.md).

## Diagnostics

The log is written to `%LocalAppData%\SheetStudio\log\sheetstudio-yyyyMMdd.log` (one file per day). Logged events:

| Event | Content |
|---|---|
| `startup` | Add-in loaded |
| `load` | Model read (with duration) |
| `create` | Sheet creation (＋ Baru / Batch) |
| `apply` | TERAPKAN (Apply) to the model |
| `thumb` / `thumb-gagal` | Thumbnail created / failed |
| `watcher` | Model change watcher |
| `event` | ExternalEvent rejected/not accepted by Revit (Denied/TimedOut) |
| `reload` | Panel re-read |

Attach that day's log file when reporting a problem.

## Three systems in one panel

Ribbon tab **DID → DID panel → Sheet Studio** opens/closes the panel. (The DID panel is shared with Family Browser and Face View.)

| Part | What it does |
|---|---|
| **Lapisan** (Layers, left) | Set order like Photoshop Layers: Discipline › Type groups, multi-select, drag-drop, lock, eye, thumbnail |
| **Inspektur** (Inspector, right) | Edit the selected sheet(s), one or many: discipline, type (slider), number, title, find-replace, prefix/suffix, title block parameters, title block, duplicate, delete |
| **＋ Baru / ⧉ Batch** (New / Batch) | Interactive name picker (discipline cards, type slider, 10×10 number map) and batch (kind × level matrix, CSV, presets) |

All changes in the panel are **staged**: a new number appears in blue, a changed title shows ✎, a deleted sheet is struck through in red. Ctrl+Z/Ctrl+Y in the panel undo/redo staged steps. **TERAPKAN** (Apply) writes everything to the model as **one Revit Undo step**. Sheets changed by someone else since they were read are skipped and reported.

## Keyboard shortcuts (focus in the panel)

| Key | Action |
|---|---|
| Click / Ctrl+click / Shift+click / Ctrl+Shift+click | Select one / add-remove / range / add range |
| Click a group row | Select all sheets in the group |
| Ctrl+A / Esc | Select everything visible / clear the selection |
| Drag between rows | Move position (everything selected goes along, relative order preserved) |
| Drag onto a Type/Discipline group row | Move group = change type/discipline (rejected if the range is full) |
| Drag a Discipline header | Change the set order (warning if it leaves UDS order) |
| Ctrl+] / Ctrl+[ | Move up / down one |
| Ctrl+Shift+] / Ctrl+Shift+[ | To the top / bottom of the group |
| F2 / double-click the title | Edit the title in the row |
| Enter / double-click the number | Open the sheet |
| Delete | Mark for deletion |
| Ctrl+D | Duplicate (sheet only) |
| Ctrl+Z / Ctrl+Y | Undo / redo in the panel |
| F5 | Re-read the model |
| Alt+click arrow | Collapse/expand all groups |

## Numbering

Format: `{Discipline 1–2 letters}{'-' if 1 letter}{Type 0–9}{Sequence 01–99}` → `A-111`, `EP110`, `G-000` (cover sheet).

- **Range** (default): each drawing kind has a range in the PUPR profile (e.g. plan `A-110…139`, ceiling `A-145…154`). The order in the panel decides who gets the lower numbers.
- **Sequence** ("Urut"): consecutive numbers per type group, starting at 1 (G starts at 0), step 1/5/10 (to leave gaps).
- Only the **groups you changed** are renumbered. The **⇅ Rapikan nomor** (Tidy numbers) button recalculates every group.
- **Locked** sheets, sheets borrowed by another user (workshared) and non-UDS numbers are never changed.

## Profiles

- `pupr` — 21 NCS disciplines + second-letter AIA examples (AD, EP, EL, MH, MP, QH, FA, RA; AS/AE/AI/AF/AG are marked "cek NCS", i.e. check NCS), 10 types, PUPR drawing kinds with ranges & title templates, abbreviation list.
- `uds` — disciplines & types only, no kind ranges.
- Importing your own JSON profile is **not** available yet.

## Batch CSV format

Header (separator `;` or `,`). The column names are Indonesian and must be written exactly as shown:

```
JENIS;SUBJEK;LEVEL;NOMOR;JUDUL;SKALA;KERTAS;BUAT VIEW
denah;LANTAI 1;Lantai 1;;;100;A3;YA
;;;A-701;GAMBAR KHUSUS;;A3;TIDAK
```

Columns: `JENIS` (kind), `SUBJEK` (subject), `LEVEL`, `NOMOR` (number), `JUDUL` (title), `SKALA` (scale), `KERTAS` (paper), `BUAT VIEW` (create view, `YA` = yes / `TIDAK` = no).

`JENIS` = kind id (e.g. `denah` = plan, `rencana-plafon` = ceiling plan) or kind name; empty = free title (`NOMOR` and `JUDUL` are then required). Empty `NOMOR` = automatic. Rows that already exist (same kind + subject) are skipped.

## Preferences

⚙ in the panel. Order of precedence: default < `%AppData%\SheetStudio\preferensi.json` ("Jadikan default saya", i.e. make it my default) < saved in the project ("Simpan untuk proyek ini", i.e. save for this project; Extensible Storage). The panel order, locks, and discipline order are saved in the project on **TERAPKAN** (Apply).

## Known limitations

- **Elevations & sections** are created as temporary *drafting views* — you must draw the section/elevation lines on the plan and then replace the view.
- **Legends** cannot be created through the API; the project must contain at least one legend to duplicate.
- **Batch** runs 5 sheets per step so progress & the Cancel button stay alive; one batch = several Undo steps. Cancel/error (the "undo everything" mode) deletes the sheets already created.
- Revit can "steal" Ctrl+Z when keyboard focus is not inside the panel — click the sheet list first.
- The parameters `SS_JenisGambar` and `SS_Subjek` (Identity Data) are added to the Sheets category on first creation.
- **Thumbnails are off by default** (expensive in large projects) — turn them on in ⚙ Preferences.
- Interaction feel (drag, dark theme, 1000 sheets) and the "single Undo" were tested manually, not automatically.

## Development

```
dotnet test tests/SheetStudio.Core.Tests      # xUnit, no Revit
dotnet test tests/SheetStudio.Revit.Tests     # ricaun: opens & closes its own Revit 2026
```

The Revit tests never touch the Revit you are using.

**Note:** `dotnet test tests/SheetStudio.Revit.Tests` will hang (ricaun timeout) while `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio.addin` is installed, because the test Revit shows the security dialog for unsigned add-ins. Temporarily rename it to `SheetStudio.addin.disabled` while the tests run, then restore it.
