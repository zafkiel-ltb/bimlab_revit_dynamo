# US-100 Revit 2024: BIMLab Player keeps the input form open after a CAD pick

## Status

in-progress

## Lane

normal

Intake #1. Type: Change request. Flags: existing-behavior, cross-platform, weak-proof.

## Product Contract

When a user runs a `.dynx` through BIMLab Player and the graph has a CAD/DWG
selection input, clicking **Chọn...**, picking CAD elements in Revit, and
finishing the pick must return the `InputForm` to the foreground **and keep it
open**, with the status box showing `Đã chọn CAD (N)`. The dialog must only
close when the user presses **>  Chạy** (run) or **Hủy** (cancel). This must
behave identically on Revit 2024, 2025, and 2026.

## Bug Being Fixed

On Revit 2024 only, after finishing the CAD pick the form flashed back and then
closed immediately ("hiện lên xong tắt ngay"), so the user never saw the
selected state and could not edit the remaining parameters. Revit 2025/2026
behaved correctly.

## Root Cause

- The add-in multi-targets `net48` (Revit 2024) and `net8.0-windows` (Revit
  2025/2026) — see `src/DynLock.Addin/DynLock.Addin.csproj`.
- `InputForm.ShouldKeepVisibleShellForSelection()` returns `true` for Revit year
  ≤ 2024, so on 2024 the CAD pick path keeps the modal form **visible**
  (`Opacity = 0.02`, no `Hide()`) during `uidoc.Selection.PickObjects`, while
  2025/2026 call `Hide()`.
- A Revit multi-select `PickObjects` is finished with **Enter** (Revit's
  "Finish" gesture). Because the form stays visible on 2024, that finishing
  keystroke reaches the modal dialog as it regains focus and is promoted by
  default WinForms dialog handling to `AcceptButton` (`ok` → `OnRunClicked` →
  `DialogResult.OK` → `Close()`) — or `Escape` → `CancelButton` → cancel. Either
  way the dialog closes. On 2025/2026 the form is `Hide()`-den during the pick,
  so the key cannot leak in.

Verified by a multi-agent root-cause workflow (4 lenses + adversarial review +
synthesis). The "form closes" symptom (vs. merely "fails to return") confirms a
key→button dismissal rather than a focus-only race.

## Design Notes

- UI surfaces: `src/DynLock.Addin/UI/InputForm.cs` (BIMLab Player input dialog).
- Fix (minimal, additive): a `_selectionInProgress` guard set in
  `HideInputFormForSelection`, cleared when the restore retry-timer settles, plus
  a `ProcessDialogKey` override that swallows `Enter`/`Escape` while the guard is
  set. This severs the proven Enter→Run / Escape→Cancel leak for **both**
  vectors and applies to CAD and non-CAD selection alike.
- Why not the alternative (`ShouldKeepVisibleShellForSelection()` → `false`,
  i.e. force 2024 onto the `Hide()` path): the opacity-shell + retry-timer were
  added deliberately for a Revit-2024 focus problem (`InputForm.cs:601` comment).
  Reverting to `Hide()` bets that problem is gone; the chosen guard fixes the bug
  without disturbing that workaround, so it cannot reintroduce it. The `Hide()`
  revert remains the documented fallback if a 2024 repaint/flicker glitch ever
  surfaces.
- Pre-existing dead code noticed (NOT touched): `_selectionUsedVisibleShell`
  (`InputForm.cs`) is assigned but never read. Left in place per surgical-change
  discipline; flagged for a separate cleanup.

## Validation

When updating durable proof status, use numeric booleans:
`scripts/bin/harness-cli story update --id US-100 --unit 0 --integration 0 --e2e 0 --platform 0`.

| Layer | Expected proof |
| --- | --- |
| Unit | None practical — WinForms/Revit modal + key routing is not unit-testable without a Revit host. |
| Integration | None — requires a live Revit host. |
| E2E | None automated. |
| Platform | **Manual, on Windows + Revit (REQUIRED).** Build `net48` for Revit 2024 and `net8.0-windows` for 2025/2026. |
| Release | Ship only after the platform check passes on Revit 2024 and at least one of 2025/2026. |

### Manual platform test (must run on Windows)

Revit 2024 (net48):
1. Run a `.dynx` whose inputs include a CAD/DWG selection row **plus** another
   param after it. Click **Chọn...**, marquee-select CAD, press Enter/Finish.
2. Expected after fix: form reappears and **stays open**; status box shows
   `Đã chọn CAD (N)`; remaining params editable; only **>  Chạy** closes/runs it.
3. Cancel path: click **Chọn...**, press Escape → form returns, dialog stays open.

Revit 2025 and 2026 (net8) — no regression:
4. Same flow → identical to prior behavior (form returns and stays open).

## Harness Delta

- Intake #1 recorded (`harness-cli intake`).
- This story created and registered.
- No harness friction found; no harness changes needed.

## Evidence

- Root-cause workflow run: `revit2024-cad-pick-dismiss-rootcause` (run
  `wf_32123537-815`), synthesis confidence ~0.74 on root cause, high confidence
  the fix converges 2024 onto correct behavior.
- Code change: `src/DynLock.Addin/UI/InputForm.cs` (`_selectionInProgress` guard
  + `ProcessDialogKey` override).
- Platform proof: **pending** — to be filled in after the Windows/Revit 2024 run.
