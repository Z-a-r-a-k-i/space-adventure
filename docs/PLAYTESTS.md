# Playtest records

This file records milestone-gate evidence, not automated-test results. A run counts as a human playtest only when a person operates the real graphical build with a physical pointer and keyboard from a fresh process. Agent injection, CLI scenarios, headless runs, and screenshots remain useful evidence but do not substitute for that classification.

## Phase 2 route protocol

Launch from the repository root:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 run
```

Complete one fresh run without using `AutomationBridge` or the Godot input-injection tools:

1. Right-click two different floor positions. While the protagonist is moving, press `Space`, issue a different move order, confirm that the HUD shows it as pending and the protagonist remains still, then resume.
2. Pan, yaw, change pitch, zoom, reset orientation, and focus the protagonist. Record any control that is unclear or fights the player.
3. At the starting berth and again on the east branch, rotate and lower the camera until a wall would sit between it and the protagonist. Confirm that the blocking upper panel cuts away, a short opaque base still communicates the room boundary, the protagonist and selection feedback remain readable, unrelated walls remain present, and the removed panel returns without distracting flicker after the view clears.
4. Hover and right-click the survivor. Confirm that the target is discoverable and the pointer does not unexpectedly issue a floor move.
5. Click the visible dialogue response with the pointer.
6. Hover and inspect the optional service terminal.
7. Hover and enter the evacuation airlock. Confirm that the completion overlay is unambiguous.
8. Record every blocker and the first confusing moment even if the route ultimately completes.

A blocker means the route cannot be completed, an intended control cannot be discovered from the game, input produces a materially different action than indicated, or state becomes misleading enough that a restart is required. Minor feel or presentation issues are recorded separately and do not automatically fail the run.

## Phase 2 records

| Date | Operator | Build/content | Result | Evidence and notes |
| --- | --- | --- | --- | --- |
| 2026-07-22 | Agent | Uncommitted working tree; `station-route-v1` | Completed | Structured input events reached the normal dispatcher and the route completed; retained images are under `artifacts/visual/phase2/`. The deterministic 1280×720 wall-cutaway capture also recorded a settled `presentation.wall.branch_north` cut, clear-view restoration, and re-cut after returning to the original yaw while gameplay remained paused. Its PNG/manifest are under `artifacts/visual/captures/`; the final PNG shows the protagonist readable behind the retained wall stub while unrelated walls and the HUD remain present. These sampled checkpoints and final frame do not assess perceived smoothness or flicker and are not the human exit gate. |
| Pending | Human | — | Not run | Required before Phase 2 can be marked complete or Phase 3 can begin. |

The noninteractive `capture -Name wall-cutaway` manifest provides repeatable agent evidence for a deterministic cut → clear-view restore → re-cut lifecycle, and its PNG records one final fixed camera/state. Neither establishes perceived smoothness, transient flicker, camera feel, or physical usability; they are not a human playtest and do not close this gate.

For a completed human record, replace the pending row with the date, `Human`, the Git commit when one exists (otherwise `uncommitted working tree`) and content revision, `Passed` or `Blocked`, and concise notes. A passing record must state that physical target clicking, pause/replacement, camera controls, wall cutaway/restoration, dialogue-button clicking, optional inspection, and completion were all exercised.

The full POC later requires five consecutive blocker-free manual playthroughs and coverage of both protagonist kits. Those runs belong in this same file once the corresponding systems exist.
