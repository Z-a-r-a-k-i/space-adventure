# Playtest records

This file records milestone-gate evidence, not automated-test results. A run counts as a human playtest only when a person operates the real graphical build with a physical pointer and keyboard from a fresh process. Agent injection, CLI scenarios, headless runs, and screenshots remain useful evidence but do not substitute for that classification.

The current gate is the Phase 4 solo-tutorial protocol below. Phase 2 and
Phase 3 protocols and records describe their historical content revisions;
their route endpoints do not describe the current build. Agent verification
of the repaired solo slice is recorded in
[SOLO-REVIEW-RESULTS.md](SOLO-REVIEW-RESULTS.md).

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
| 2026-07-24 | Human (project owner) | `94f4e5b`; `station-route-v1` | Passed | The owner operated a fresh graphical process on the second machine through physical keyboard and pointer input forwarded by AnyDesk; no agent input injection was used. Physical target clicking, pause and pending-order replacement, camera controls, wall cutaway and restoration, survivor targeting, dialogue-button clicking, optional terminal inspection, and airlock completion were all exercised. The owner validated the run, reported that it worked well, and identified no blocker. The live route retained its intended greybox presentation; absence of staged production art was noted but did not impede usability or completion. |

The noninteractive `capture -Name wall-cutaway` manifest provides repeatable agent evidence for a deterministic cut → clear-view restore → re-cut lifecycle, and its PNG records one final fixed camera/state. Neither establishes perceived smoothness, transient flicker, camera feel, or physical usability; they are not a human playtest and do not close this gate.

For each completed human run, record the date, `Human`, the Git commit when one exists (otherwise `uncommitted working tree`) and content revision, `Passed` or `Blocked`, and concise notes. A passing Phase 2 record must state that physical target clicking, pause/replacement, camera controls, wall cutaway/restoration, dialogue-button clicking, optional inspection, and completion were all exercised.

The full POC later requires five consecutive blocker-free manual playthroughs
covering Vanguard's solo tutorial, Protector recruitment, and the two-character
encounter. Those runs belong in this same file once the corresponding systems
exist.

## Phase 3 route-v2 acceptance protocol

Launch `station-route-v5` at 1920×1080 and verify:

1. Either survivor response unlocks navigation through the amber entry service
   door while it remains visibly closed.
2. Right-clicking a destination in the solo arena routes Vanguard through the
   door. On approach it automatically slides both leaves over 0.25 seconds,
   changes the status strip to cyan, and removes collision without clipping.
3. Vanguard can enter the solo arena but cannot interact with or navigate
   through the amber far service door.
4. Camera inspection covers the start room, solo arena, Protector room, main
   arena, final-airlock approach, and unchanged evacuation airlock, including
   the currently inaccessible rooms.
5. The opening camera is centered on Vanguard. WASD/arrow panning follows the
   current camera orientation after yaw changes. The route remains readable at
   the default 14.5 m camera distance and the 20 m maximum; wall cutaway and
   restoration continue to behave normally.

| Date | Operator | Build/content | Result | Evidence and notes |
| --- | --- | --- | --- | --- |
| 2026-08-04 | Agent | Uncommitted PR 14 working tree; `station-route-v5` | Technical graphical checkpoint passed; owner feel check pending | The exact structure and door GLBs passed Blender review and fresh reimport. Godot import, the real-navigation headless flow, authoritative door/link/blocker synchronization, the locked navigation-island bypass check, and the deterministic start-room cutaway passed. The retained contextual capture was inspected at 14.5 m; a temporary overwritten capture was inspected at 20 m. Safe Windows window rebinding failed before live input could be injected, so door smoothness, clipping feel, physical clicking, and a free-camera sweep of every inaccessible room remain explicit owner checks rather than inferred passes. |

## Phase 4 solo-tutorial acceptance protocol

Status: owner-operated acceptance of the repaired `station-route-v7` slice
remains pending. Launch with `scripts/dev.ps1 run` at 1920×1080 and verify from
a fresh process:

1. Complete the survivor exchange and cross the entry service door. Confirm
   the Enforcer encounter starts and automatically pauses once at the
   beginning of readying. The draw must advance only after resume.
2. While still paused, right-click the Enforcer and confirm the repeating
   attack is pending without advancing time. Press `Space`; Vanguard must then
   finish drawing, approach into range, face the target, and fire repeatedly.
   Repeated clicks on that same target must neither restart wind-up nor
   accelerate the firing cadence.
3. During an Enforcer wind-up, pause, press `1`, left-click its position, and
   resume. Confirm Suppressive Fire has a readable targeting preview, muzzle
   release, cooldown, and interrupts that wind-up exactly once. Basic fire
   must resume against the assigned target without another click. A new
   offensive order issued during released-shot recovery must wait rather
   than fire early.
4. Allow Vanguard to take damage and press `2`. Confirm Field Aid consumes its
   only charge, heals once, and displays one green pulse with a floating
   positive value, then resumes basic fire. Move during wind-up and use both
   `X` and the Stop button in separate attempts: they must cancel the order
   and stop automatic fire without removing an already released shot's
   recovery. While paused, a newer primary order replaces the pending one.
5. Inspect a cyan bolt leaving the actual muzzle and travelling toward the
   target. Its impact flash and damage number must appear together on visual
   arrival; Suppressive Fire also delays its target pulse to that arrival.
   Health and interruption still resolve at release. Normal hits display
   `-10` over the Enforcer and `-15` over Vanguard; Suppressive Fire displays
   `-30`. Neither combatant plays a hit-reaction animation. Pause during draw,
   bolt flight, impact, and hostile strike; animation and effects must freeze
   together while camera and UI remain usable.
6. Win once. Confirm the Enforcer down pose, Vanguard holster sequence, cyan
   far door, and collision/navigation unblock. Cross the opening door and
   recruit Protector: the party must contain two members and the HUD must
   show **Current prototype slice complete**. The later encounter and final
   airlock remain locked; this is not scenario completion.
7. Start a fresh run and deliberately lose. Confirm defeat pauses atomically
   and both the visible Retry button and Enter/keypad Enter work across
   separate attempts. Retry keeps survivor and entry-door progression, resets
   only the encounter attempt, and returns to the readying pause without
   duplicating either actor. `X` must do nothing while Stop is unavailable
   during dialogue, defeat, or victory securing.
8. Rotate and lower the camera around both service doors. Obstructing lintels
   must fade and restore without distracting flicker; wall cutaway must remain
   readable. Floor clicks through an already open door must issue movement.
   After the one combat-entry framing, camera input must remain under player
   control throughout the attempt.
9. At 7.5 m, 14.5 m, and 20 m inspect the carbine hand/back attachment, muzzle
   line, both-hand plausibility, Vanguard draw/fire/holster/down deformation,
   Enforcer approach/strike/down deformation, numeric damage and impact-effect
   readability, health and retry UI, camera controls, and absence of visible
   greybox presentation. Record the first confusing moment and any visible
   hitch, even when the route is completable.

Use [SOLO-REVIEW.md](SOLO-REVIEW.md) to reproduce named checkpoints or a defect
after the manual run. Automated victory, defeat, and Godot input-event checks
are required evidence but do not satisfy this owner-operated protocol.
