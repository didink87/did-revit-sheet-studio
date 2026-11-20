# Sheet Studio — Quick Guide

A Revit 2026 add-in to create, number, and reorder sheets quickly, using the UDS numbering pattern and the PUPR profile.

## Basic principles

1. **Sheets appear as a list of layers** (like layers in Photoshop). The order in the panel = the order of the sheet numbers.
2. **Changes are staged first and do not touch the model yet.** A `● N pending changes` marker appears at the bottom.
3. **APPLY** writes all changes to Revit at once, as **one Undo step**. **Discard** cancels everything.
4. **Numbers follow the rules.** Discipline + type + sequence, e.g. `A-110` = Architectural, type 1 (plans), sequence 10.

## Getting started

1. Open a Revit project and show the **Sheet Studio** panel.
2. Open **⚙ Preferences** once at the start: choose the title block for each paper size A3 / A2 / A1.
3. Press **⟳** (or F5) when the model changes from outside the panel.

## Creating sheets

### One sheet — `＋ New`

1. Choose the **discipline** (A, S, M, E, …) and the sheet **type** (drag the 0–9 slider).
2. The sequence number fills in automatically. Want it manual? Choose **Manual** or click a free cell on the number map.
3. Type the **title**, for example `DENAH LANTAI 1`.
   - Press **💾 Save** if you want to reuse the title (optional).
   - Saved titles appear in the pick list; **Remove from list** deletes one.
4. **Choose the paper (A3/A2/A1) and title block.** Required. A warning appears if you forget.
5. Choose the view: **None**, **Use existing**, or **Create new**.
6. Press **CREATE SHEET**.

### Many sheets — `⧉ Batch`

1. Tick the **drawing kinds** and **levels**, then press **Fill from matrix**. Or choose a **Preset**, or **Import CSV**.
2. Press **Preview** and check the status of each row:
   - green = new
   - grey = exists
   - yellow = duplicate
   - red = error
3. Press **RUN**. **Stop** cancels midway, and the model returns to how it was.
4. When it finishes, the button becomes **✓ Done — back to Revit**.

## Managing existing sheets

- **Reorder:** drag a layer, or `Ctrl+]` / `Ctrl+[` (up/down), `Ctrl+Shift+]` / `[` (to top/bottom).
- **Multi-select:** `Ctrl` / `Shift` + click, or `Ctrl+A`. `Esc` clears the selection.
- **Rename:** `F2`. **Open the sheet in Revit:** `Enter`.
- **Duplicate:** `Ctrl+D` (sheet only), or the **Duplicate ▾** menu in the Inspector.
- **Delete:** `Delete` or **Mark for deletion**. It is only deleted after APPLY.
- The **Inspector** (right or bottom) edits the selected sheets: number, title, title block, title-block parameters, and show in sheet list.
- **⇅ Renumber all** recomputes every number from the panel order. Use it only when needed.
- **Fill indicator:** the circle at the start of each row is hollow for an empty sheet and filled Revit blue when the sheet holds views or schedules. Click it to see what is on the sheet.

## Numbering modes

- **Range:** numbers come from the drawing-kind range (plans in A-110…139).
- **Sequential:** consecutive numbers per type.

Switch with the **Numbering** button in the toolbar.

## Shortcuts

| Key | Function |
|---|---|
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo in the panel |
| `F5` | Re-read the model |
| `F2` | Rename |
| `Delete` | Mark for deletion |
| `Ctrl+D` | Duplicate sheet |

## If something goes wrong

Send the log file: `%LocalAppData%\SheetStudio\log\sheetstudio-yyyyMMdd.log`.

User settings are stored in `%AppData%\SheetStudio\` (`preferensi.json`, `judul-tersimpan.json`).
