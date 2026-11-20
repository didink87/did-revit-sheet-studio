# Sheet Studio — Revit 2026

A Revit add-in to **create, reorder, and edit sheets** using UDS / US National CAD Standard numbering (Discipline Designator + Sheet Type Designator) and the PUPR standard profile.

- Design: `docs/superpowers/specs/2026-11-03-sheet-studio-design.md`
- Naming rules: `docs/reference/02-penamaan-lembar-aia-ncs.md`
- Plan & decisions: `docs/superpowers/plans/2026-11-03-sheet-studio.md`

## What's new in 1.1

- **Sheet fill indicator.** Every sheet row in the Layers panel starts with a small circle: **hollow** when the sheet is empty, **filled Revit blue** when the sheet holds views or schedules. Click it to open a short list of what is on the sheet (name and kind of each view or schedule). The indicator follows the model when viewports or schedules are placed or removed.
- **English UI.** Every window, menu, tooltip, status line, message box, ribbon label, transaction name, and the setup scripts are now in English. Profile descriptions (discipline and sheet-type notes) are English too. PUPR title vocabulary (DENAH, TAMPAK, POTONGAN …), the profile JSON, and the stored preference keys are unchanged, so existing projects, saved titles, and preferences keep working.
- **Batch CSV** now exports an English header (`KIND;SUBJECT;LEVEL;NUMBER;TITLE;SCALE;PAPER;CREATE VIEW`, `YES`/`NO`). Files exported by 1.0 (Indonesian header, `YA`/`TIDAK`) still import.
- Revit transaction names are now English: `Sheet Studio: Apply sheet changes`, `Sheet Studio: Delete & temporary numbers`, `Sheet Studio: Save sheet order`, and so on.

## Installation

Requires **Revit 2026** (Windows). Close Revit before installing or updating.

### Option 1 — Simple setup (no terminal)

1. Open the [Releases](https://github.com/didink87/did-revit-sheet-studio/releases) page and download `SheetStudio-<version>-Revit2026.zip`.
2. Extract **the whole** ZIP into one folder.
3. Double-click **`Setup.bat`** → choose **[1] Install / update**.
4. Open Revit 2026 → **DID** tab → **Sheet Studio**. If an add-in security dialog appears, choose **Always Load**.

Setup copies the add-in to `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio\` and writes `SheetStudio.addin`. To uninstall: run `Setup.bat` → **[2] Uninstall**.

### Option 2 — Terminal (from source)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Git.

```
git clone https://github.com/didink87/did-revit-sheet-studio.git
cd did-revit-sheet-studio
dotnet build src/SheetStudio.Revit -c Release -p:InstallAddin=true
```

This copies `SheetStudio.addin` to `%AppData%\Autodesk\Revit\Addins\2026\` and points it at `bin\Release\SheetStudio.Revit.dll`. Use **Release**: Debug builds are slower (the build target warns when installing from Debug). To update: `git pull`, then repeat the build command. To uninstall: delete that `.addin` file.

Both options write the same manifest (`SheetStudio.addin`), so whichever you ran last wins.

### Building a release package (developers)

```
powershell -ExecutionPolicy Bypass -File setup/Build-Release.ps1 -Version 1.0.0
```

Output: `dist/SheetStudio-1.0.0-Revit2026.zip` (contents: `bin\`, `Setup.bat`, `Install-SheetStudio.ps1`, `BACA-DULU.txt`, `PANDUAN.md`; the last two are the read-me and the quick guide, in English despite their file names).

Short user guide: [`docs/PANDUAN.md`](docs/PANDUAN.md).

## Diagnostics

The log is written to `%LocalAppData%\SheetStudio\log\sheetstudio-yyyyMMdd.log` (one file per day). Log lines keep their original wording. Events recorded:

| Event | Content |
|---|---|
| `startup` | Add-in loaded |
| `load` | Model read (with duration) |
| `create` | Sheet creation (＋ New / Batch) |
| `apply` | APPLY to the model |
| `thumb` / `thumb-gagal` | Thumbnail created / failed |
| `watcher` | Model change watcher |
| `event` | ExternalEvent denied/not accepted by Revit (Denied/TimedOut) |
| `reload` | Panel re-read |

Attach that day's log file when reporting a problem.

## Three systems in one panel

Ribbon tab **DID → DID panel → Sheet Studio** shows/hides the panel. (The DID panel is shared with Family Browser and Face View.)

| Part | What it does |
|---|---|
| **Layers** (left) | Set order like Photoshop's Layers panel: Discipline › Type groups, multi-select, drag-drop, lock, eye, thumbnail, fill indicator |
| **Inspector** (right) | Edit the selected sheets (one or many): discipline, type (slider), number, title, find-replace, prefix/suffix, title-block parameters, title block, duplicate, delete |
| **＋ New / ⧉ Batch** | Interactive name picker (discipline cards, type slider, 10×10 number map) and batch (kind × level matrix, CSV, presets) |

All panel changes are **staged**: new numbers show in blue, edited titles show ✎, sheets marked for deletion are struck through in red. Ctrl+Z/Ctrl+Y in the panel undo/redo staged steps. **APPLY** writes everything to the model as **one Revit Undo step**. Sheets changed by someone else since they were read are skipped and reported.

## Keyboard shortcuts (focus in the panel)

| Key | Action |
|---|---|
| Click / Ctrl+click / Shift+click / Ctrl+Shift+click | Select one / toggle / range / add range |
| Click a group row | Select all sheets in the group |
| Ctrl+A / Esc | Select all visible / clear selection |
| Drag between rows | Move (everything selected follows, relative order kept) |
| Drag onto a Type/Discipline group row | Move group = change type/discipline (refused when the range is full) |
| Drag a Discipline header | Reorder the set (warning when it leaves UDS order) |
| Ctrl+] / Ctrl+[ | Move up / down one |
| Ctrl+Shift+] / Ctrl+Shift+[ | To top / bottom of the group |
| F2 / double-click title | Rename the title in the row |
| Enter / double-click number | Open the sheet |
| Delete | Mark for deletion |
| Ctrl+D | Duplicate (sheet only) |
| Ctrl+Z / Ctrl+Y | Undo / redo in the panel |
| F5 | Re-read the model |
| Alt+click arrow | Collapse/expand all groups |

## Numbering

Format: `{Discipline 1–2 letters}{'-' if 1 letter}{Type 0–9}{Sequence 01–99}` → `A-111`, `EP110`, `G-000` (cover).

- **Range** (default): each drawing kind has a range in the PUPR profile (e.g. plans `A-110…139`, ceiling plans `A-145…154`). The order in the panel decides who gets the lower numbers.
- **Sequential**: consecutive numbers per type group, starting at 1 (G starts at 0), step 1/5/10 (to leave gaps).
- Only the **groups you changed** are renumbered. The **⇅ Renumber all** button recomputes every group.
- **Locked** sheets, sheets borrowed by another user (workshared), and non-UDS numbers are never changed.

## Profiles

- `pupr` — 21 NCS disciplines + AIA example second letters (AD, EP, EL, MH, MP, QH, FA, RA; AS/AE/AI/AF/AG flagged "check NCS"), 10 types, PUPR drawing kinds with ranges & title templates, abbreviation list.
- `uds` — disciplines & types only, no kind ranges.
- Importing your own JSON profile is **not** available yet.

## Batch CSV format

Header (separator `;` or `,`):

```
KIND;SUBJECT;LEVEL;NUMBER;TITLE;SCALE;PAPER;CREATE VIEW
denah;LANTAI 1;Lantai 1;;;100;A3;YES
;;;A-701;GAMBAR KHUSUS;;A3;NO
```

`KIND` = kind id (e.g. `denah`, `rencana-plafon`) or kind name; empty = free title (`NUMBER` and `TITLE` are then required). Empty `NUMBER` = automatic. Rows that already exist (same kind + subject) are skipped. The old Indonesian header (`JENIS;SUBJEK;LEVEL;NOMOR;JUDUL;SKALA;KERTAS;BUAT VIEW`, `YA`/`TIDAK`) is still accepted on import.

## Preferences

⚙ in the panel. Precedence: defaults < `%AppData%\SheetStudio\preferensi.json` ("Make my default") < saved in the project ("Save for this project", Extensible Storage). Panel order, locks, and discipline order are saved in the project on **APPLY**.

## Known limitations

- **Elevations & sections** are created as temporary *drafting views* — you must draw the section/elevation line on a plan and swap the view.
- **Legends** cannot be created through the API; the project needs at least one legend to duplicate.
- **Batch** runs 5 sheets per step so progress and the Stop button stay alive; one batch = several Undo steps. Cancel/error ("cancel all" mode) deletes the sheets already created.
- Revit can "steal" Ctrl+Z when keyboard focus is not inside the panel — click the sheet list first.
- The parameters `SS_JenisGambar` and `SS_Subjek` (Identity Data) are added to the Sheets category on first creation.
- **Thumbnails are off by default** (expensive in large projects) — enable them in ⚙ Preferences.
- Interaction feel (drag, dark theme, 1000 sheets) and the "single Undo" are tested manually, not automatically.

## Development

```
dotnet test tests/SheetStudio.Core.Tests      # xUnit, no Revit
dotnet test tests/SheetStudio.Revit.Tests     # ricaun: opens and closes its own Revit 2026
```

The Revit tests never touch the Revit you are using.

**Note:** `dotnet test tests/SheetStudio.Revit.Tests` hangs (ricaun timeout) while `%AppData%\Autodesk\Revit\Addins\2026\SheetStudio.addin` is installed, because the test Revit shows the unsigned-add-in security dialog. Temporarily rename it to `SheetStudio.addin.disabled` while the tests run, then restore it.
