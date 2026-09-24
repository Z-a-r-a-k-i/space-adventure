# Testing and graphical review

Use `pwsh -NoProfile -File scripts/dev.ps1 help` for the command surface.
Run relevant checks when behavior changes; documentation-only edits need link,
path, and whitespace checks. Reports must identify what actually ran.

## Automated checks

Run from the repository root after the [initial build/import](../README.md#run):

```powershell
pwsh -NoProfile -File scripts/dev.ps1 test
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-party
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-party-defeat
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-escape
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-escape-defeat
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-combat-defeat
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-party
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-party-defeat
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-escape
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-escape-defeat
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-vision
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name ship-victory     # also ship-overwhelm, ship-defeat, ship-retry
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name ship-balance     # every pilot across 40 seeds (tuning report)
pwsh -NoProfile -File scripts/dev.ps1 headless -Name ship-battle      # also ship-battle-defeat
pwsh -NoProfile -File scripts/dev.ps1 headless -Name ship-handoff     # station escape -> ordinary handoff
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter ship -Mode capture -Resolution 1280x720   # and 1920x1080
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter ship -Mode input      # input + window resize
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter ship -Mode handoff    # graphical handoff capture
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter ship -Mode performance
```

Core tests cover rules and regressions; the CLI uses a deterministic fixture
pathfinder. Headless profiles exercise the real Godot scene/navigation through
victory or defeat/retry. Bootstrap and humanoid/hostile gallery smokes are also
available for their respective integration changes. Passing them does not
establish animation quality, camera feel, or physical input usability.
`station-escape` completes all six fights and departure. The
`station-escape-defeat` CLI/headless profiles force defeat and retry in security
checkpoint and launch bay while preserving previous victories and recruitment.

Ship profiles: `ShipCombatTests` pin tick order, chunking, power and pause exploits, manning/Hold, auto priority, door traversal gas exchange, single-breach recovery, Medic interruption/no revival, defeat precedence, per-weapon telegraphs, wins inside the pilot band (`ShipBattlePilot.MinimumWinSeconds`–`MaximumWinSeconds`) for two distinct pilots with the repair reserve exhausted, retry and continuation orderings/failures. `ShipFtlRulesTests` pin shield layers versus piercing missiles, crew injury by damage, seeded per-attempt evasion (missiles never miss), mounting-order weapon power, synchronized held volleys, missile ammunition and Medic auto-treatment. `ship-balance` reports win rate, duration and hull spread per pilot across 40 seeds; it informs tuning and asserts nothing. CLI and `ship-battle` headless use the same scripted typed-command pilot. `ship-handoff` runs the escape pilot through the real 8 s departure and threaded load into the battle (paused tick 0, crew identity, one instance, battle-only retry). Ship reviews write `artifacts/ship-review/*.png/json` and check publication bounds/anchors (including enemy room anchors), that each ship and its shield bubble fit the HUD-safe area, that HUD panels neither overlap each other nor the hulls, on-screen controls, projectiles in flight (`ship-combat-*` capture), weapon-card/room aiming, power-column clicks, volley hold, the bounded destruction clock and retry cleanup; the project stretches a 1280x720 logical layout, so resize evidence records the real window size. Missing hazard publications are reported in the manifests. None of this is owner acceptance of art, handling or audio.

`station-vision` checks perception against the actual Godot station layout.
Keep pure rule fixtures separate: their synthetic paths and blockers cannot
establish that scene walls and door openings match the visible map.

The expansion requires coverage for atomic healing validation, maximum-health
clamping, release revalidation, pause/pending replacement, field membership and
expiry, and defensive recovery/attack-intent preservation. Exercise rifle
approach/targeting/projectile flight, Barrier/Interrupt/Taunt interactions,
ordered six-fight progression, victory recovery, intermediate and final defeat
retry, and exactly-once three-person boarding. Review profiles also verify
actual Godot paths around all eight service recesses, rejected pit movement and
field placement, supporting floor under every spawn, and closed door separation. Full-route profiles must reach
departure through commands and navigation. Record actual results separately;
this checklist is not passing evidence.

`-TimeoutSeconds` bounds automated runs; `run -AutoQuitSeconds <n>` bounds a
graphical launch. `dev.ps1` isolates Godot user data under ignored
`artifacts/godot-user/`. Retain stdout/stderr and the last observation on failure;
do not extend a timeout merely to make a hanging run appear to pass.

## Reproduce a visual or input defect

All profiles below run the actual game without an editor plugin:

```powershell
# Paused armed checkpoint; Space resumes ordinary play.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode live -Checkpoint armed -AutoQuitSeconds 120
# Party checkpoint after playing through solo victory and recruitment.
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter party -Mode live -Checkpoint ready -AutoQuitSeconds 120
# Inspect one named pose and its diagnostics.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode capture -Checkpoint recoil -Distance 7.5 -Resolution 1280x720
# Motion evidence, including draw, repeated fire, and holster.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode record -Distance 14.5
# Keyboard/mouse events through normal picking, buttons, and commands.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode input -Sequence victory
pwsh -NoProfile -File scripts/dev.ps1 review -Mode input -Sequence defeat
# Separate 30-second real-time frame-pacing sample.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode performance -Resolution 1920x1080
```

Add `-Encounter party` to the same capture, record, input, or performance commands
for the two-character arena. Review accepts victory/defeat sequences, 7.5–20 m
camera distance, and 720p/1080p.
Use `-Encounter escape` for the expanded route in capture, live, input, record,
or performance mode:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter escape -Mode capture -Checkpoint service -Resolution 1920x1080
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter escape -Mode live -Checkpoint launch -AutoQuitSeconds 120
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter escape -Mode input -Sequence defeat -Resolution 1280x720
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter escape -Mode record -Distance 14.5
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter escape -Mode performance -Resolution 1920x1080
```

Escape checkpoints are `service`, `security`, `dock`, `launch`, `heal`, `field`,
`boarding`, `departure`, and `complete`; `ready` and `armed` target service access. Escape
performance starts its real-time sample in launch bay. Inspect three-person
selection, portraits/Tab focus, Heal picking, field radius/affected-crew
previews, rifle muzzle/grip alignment, crowded health/order labels, navigation,
animation, sound, and boarding/closure/takeoff at the same distances/resolutions.
For environment inspection, capture/live also accept optional `-Pitch 0.45..1.15`
and `-Yaw -3.14..3.14` (radians); these captures have separate output folders.
Restrained recoil is the default; `-Recoil strong` reproduces the alternative.
Checkpoints are defined in [GameHost.Review.cs](../game/scripts/GameHost.Review.cs),
[GameHost.PartyReview.cs](../game/scripts/GameHost.PartyReview.cs), and
[GameHost.EscapeReview.cs](../game/scripts/GameHost.EscapeReview.cs).
Input and performance modes use their full profile with the default checkpoint.

For [shared crew vision](ARCHITECTURE.md#shared-crew-vision), run:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 review -Encounter vision -Mode capture -Checkpoint all
```

The checkpoints are `vision-hidden`, `vision-revealed`, `vision-occluded`, and
`vision-shared` (Medic scouting while the other two crew remain out of range).
Live mode starts at `vision-hidden`; the vision profile has no performance mode.
`review` rejects a checkpoint that the chosen encounter's profile never captures.
The profile also checks Protector sharing sight from the earlier junction.
Review closed and completed doors, dormant discovery before
the all-crew trigger, loss of sight behind full walls, and a scouting companion
sharing sight while another crew member is selected. Check range boundaries,
hidden enemy models/picking/health bars, and rejection of unseen target orders.
Pan, rotate, and zoom while paused: camera changes must not change perception.
Use the supported resolutions and camera distances; inspect the resulting
Godot frames directly. These checks do not replace the owner acceptance gate.
The profile is defined in [GameHost.VisionReview.cs](../game/scripts/GameHost.VisionReview.cs).

Outputs are under `artifacts/solo-review/`, `artifacts/party-review/`,
`artifacts/escape-review/`, or `artifacts/vision-review/`, separated by profile. `review.json`
contains state, sampled poses, grip/muzzle measurements, projectile travel,
effect delays, and occlusion. Capture/record gates check weapon size, armed grip
fit, muzzle direction, and retry cleanup. Input checks cover availability of
Stop, projectile pause/travel/arrival, and victory/recruitment or defeat/retry.
Party input also checks world/portrait/drag selection, Shift-drag, Tab ability
focus within a group, drag cancellation/HUD exclusion, edge panning and its
focus/drag/HUD stops, independent orders,
single-click barrier placement/cancellation/interception, movement and automatic
turning after deployment, removed-facing shortcut/command rejection, Taunt targeting, separate Burst releases
and pause, both skill cooldowns, rejection of removed commands/shortcuts, sentry aim limits, complete
death playback, shotgun pellets, and HUD bounds. Injected mouse events use the
viewport's final transform so 1080p exercises the same hit regions as 720p.
Solo and party input reviews also check that F1 opens a bounded field manual,
blocks gameplay input, and restores camera input when Escape closes it.
They check session volume/mute against the audio bus and keyboard focus without
letting Space close the manual or resume combat. Solo dialogue checks cover
Tab/arrow response focus, number-key choice, and camera/command isolation.
Field Ops checks cover dialogue bounds, the open world-input space between
control groups, pending-order replacement and label bounds, shared target badges,
and hiding world labels in the manual. The party `field-orders` checkpoint
captures shared attack planning; the solo `dialogue` checkpoint captures a response panel.
Overhead health checks cover current/max health, player-perception filtering,
death/retry, mouse passthrough, paused camera tracking, and separation from order labels.
Inspect live playback or the exact recording for every visual change; stills
alone can miss accumulated offsets and transition defects.

Use solo `-Checkpoint briefing` and party `-Checkpoint recruitment` for the
authored dialogue and room landmarks. Ability previews show current affected
targets; queued actions are revalidated on resume. Inspect their pointer card
near crowded actors and confirm cooldown/range feedback remains legible.

Use the [audio audition](ATTACK-PRESENTATION.md#audio) alongside ordinary play.
Signal analysis and successful playback do not establish listening acceptance.

MovieWriter always records 1080p at fixed 60 fps and may encode slower than real
time. It cannot measure frame pacing. Performance mode instead measures
wall-clock `FramePostDraw` intervals, simulation/presentation CPU, command-to-first-
frame latency, engine/render CPU and GPU, focus, VSync mode, and compilation
deltas after warmup. This excludes OS input and external-tool latency. A
completed performance profile is not a frame-rate acceptance gate: inspect its
percentiles and spikes. `measurement_completed` reports harness completion;
`frame_pacing_warning` flags more than 1% of frames exceeding 50 ms.
Record hardware, resolution, and cache conditions when
comparing results. Reset native desktop capture sessions before measuring; a
retained inspection session has produced compositor frame spikes on this workstation.

For the owner's RustDesk/dummy-display setup, the NVIDIA **Godot program**
profile uses **Vulkan/OpenGL present method → Prefer layered on DXGI Swapchain**.
Disable game VSync locally with this ignored `game/override.cfg`:

```ini
[display]
window/vsync/vsync_mode=0
```

Godot [loads this override automatically](https://docs.godotengine.org/en/stable/classes/class_projectsettings.html#description);
the normal launch/review commands need no extra flags. Forward+/Vulkan and the
frame cap remain in `game/project.godot`; global NVIDIA settings are unchanged.
To retest a physical monitor, remove the local VSync entry and restore the
Godot program's presentation setting to its inherited Auto value. A renamed
Godot executable or exported game needs its own program profile. These checks
do not measure RustDesk transport or client input latency.

For wall occlusion, `capture -Name wall-cutaway` produces and validates one
1280×720 JSON/PNG pair after cut → restore → re-cut while paused. Inspect the
frame; settled state and an image checksum cannot prove smoothness or lack of
flicker. Live Godot inspection is required for lintel and camera changes.

## Manual playtest

Use a fresh `dev.ps1 run` process with physical pointer/keyboard input. Agent
input injection is separate evidence. The [roadmap](ROADMAP.md) owns gate status.

1. Complete either survivor response, inspect the optional terminal, and cross
   the entry door. Combat must start and auto-pause once; resume to finish draw.
2. Queue and replace an order while paused. Resume attack, then repeatedly
   click the same enemy: cadence must remain stable. Check move/Stop cancellation
   and that released-shot recovery cannot be bypassed.
3. Interrupt a telegraphed strike and use Burst on an enemy.
   Skills keep separate cooldowns and resume the assigned basic
   attack. Pause between Burst shots, then cancel it with a move.
4. Inspect muzzle launch and impact/number alignment. Pause during draw, bolt
   flight, and strike: animation/effects freeze together; camera/UI still work.
5. Win, cross the far door, recruit Protector, and lead both crew into the
   main arena. Drag around both crew, order a group move/attack, and use Tab to
   switch abilities while keeping the group selected. Use portraits or Shift-click
   for independent orders; assign different enemies, place Protector's Barrier with one click
   between him and the sentry, then Taunt. Check that nearby enemies focus him, front-side shots
   are blocked, and melee still hits. Move Protector in another direction: the barrier must
   keep its deployed position and direction until expiry.
   Check per-member health, orders, cooldowns, and incoming-hit countdowns.
   Idle crew must wait for a target order, including after their target falls.
6. Win the two-person fight and recruit Medic at the safe junction. Select and
   issue independent orders to all three crew through portraits, world picking,
   group drag, and Tab. Complete service access, security checkpoint, dock
   concourse, and launch bay in order. Each fight must wait for the recruited
   crew in its entry zone, pause once, and remain completed after victory.
7. Heal a wounded ally and Medic through world/portrait targeting; try invalid,
   fallen, and out-of-range recipients. Place Healing Field, move crew into/out
   of it, pause mid-pulse, move Medic, and replace the field. Check healing
   feedback, cooldown release, and resumed assigned pistol fire. Watch rifle
   enemies approach, stop, telegraph, and fire; dodge/intercept their shots,
   Interrupt and Taunt them, and retain the sentry's firing limits.
8. Let a crew member fall before a victory; securing must restore the entire
   crew and cooldowns for travel and clear orders/effects. On separate attempts
   lose an intermediate fight and the launch-bay fight. Test Retry button and
   Enter; preserve recruitment, dialogue, inspection, and completed fights,
   with no duplicate enemies or stale fields/projectiles. Ignore X when Stop
   is unavailable in dialogue, defeat, or securing.
9. After final victory, reach the evacuation airlock and board the cutter.
   Boarding must require all three crew, including at approach completion.
   Inspect entrance closure, takeoff, and exactly one completion summary.
10. Pan at each window edge and with the keyboard; edge scrolling must stop
   over HUD, during a selection drag, and when the game loses focus. Yaw/pitch/zoom
   throughout the connected route; inspect wall/lintel restoration and
   floor clicks through open doors. At 7.5, 14.5, and 20 m judge weapon fit,
   motion, effects, text, and input readability. Record the first confusion or
   hitch even if the route completes.

Record only date, operator type, commit/content revision, passed/blocked,
checks covered, and named defects in [history](archive/prototype-history.md).
The complete POC requires the full-route runs specified in [Product](PRODUCT.md).

## Live automation

[AutomationBridge.cs](../game/scripts/AutomationBridge.cs) exposes observations,
presentation diagnostics, sequenced events with gap metadata, typed-command
submission, pause, exact stepping, bounded advance-until-event, screen projection,
context-click injection, and shutdown. Stepping requires pause and is capped
at 3,000 ticks. Read events/acknowledgements after injected input; injection
success alone is not command acceptance. The bridge exposes no arbitrary
property setters or code evaluation.

Example schema-v11 adapter command:

```json
{"schema_version":11,"command_id":"review.pause","type":"set_pause","payload":{"paused":true}}
```

`use_ability` takes `target_position` for Interrupt or Healing Field,
`target_actor_id` for Burst or Heal (including Medic's own ID), or
`target_self: true` for Taunt. Barrier requires both `target_position` (ground)
and `target_facing` (horizontal direction). Mixed target kinds are rejected.
Observations expose automatic body heading, each crew member's health and skill
slots/cooldowns, taunted targets, barrier/field state, flying projectiles, and
ordered encounter progress. `visible_hostiles` is the player-perception list;
diagnostic `hostiles`, `encounter.hostile_ids`, and events may include enemies
the player cannot see. Hostile projections include `encounter_id`,
`encounter_phase`, `encounter_attempt`, and `facing`. Use the hostile's own
metadata when presenting discovered enemies outside the active encounter.
Both input paths use the same commands and shared-sight validation. Schema 11
migrates content, fixtures, and tools together for crew vision;
the schema-9 removal of manual-facing commands and held/pending heading fields
remains in force.

Prefer these bounded helpers over arbitrary scene mutation. For ad hoc live
control, `plugin-link` links the external `godot-ai-plugin`; override its source
with `-GodotAiPlugin` or `SPACE_ADVENTURE_GODOT_AI_PLUGIN`, then enable **Godot AI
Control** in the editor. Keep its local project entries uncommitted. The plugin
uses the first free port in 6550–6569; explicit `GODOT_AI_PORT` or a matching
`GODOT_AI_PORT_RANGE` can isolate parallel sessions. Never recursively clean
inside the addon junction.
