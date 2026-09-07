# Testing and graphical review

Use `pwsh -NoProfile -File scripts/dev.ps1 help` for the command surface.
Run relevant checks when behavior changes; documentation-only edits need link,
path, and whitespace checks. Reports must identify what actually ran.

## Automated checks

Run from the repository root after the [initial build/import](../README.md#run):

```powershell
pwsh -NoProfile -File scripts/dev.ps1 test
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-combat-defeat
```

Core tests cover rules and regressions; the CLI uses a deterministic fixture
pathfinder. Headless profiles exercise the real Godot scene/navigation through
victory or defeat/retry. Bootstrap and humanoid/hostile gallery smokes are also
available for their respective integration changes. Passing them does not
establish animation quality, camera feel, or physical input usability.

`-TimeoutSeconds` bounds automated runs; `run -AutoQuitSeconds <n>` bounds a
graphical launch. `dev.ps1` isolates Godot user data under ignored
`artifacts/godot-user/`. Retain stdout/stderr and the last observation on failure;
do not extend a timeout merely to make a hanging run appear to pass.

## Reproduce a visual or input defect

All profiles below run the actual game without an editor plugin:

```powershell
# Paused armed checkpoint; Space resumes ordinary play.
pwsh -NoProfile -File scripts/dev.ps1 review -Mode live -Checkpoint armed -AutoQuitSeconds 120
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

Review accepts victory/defeat sequences, 7.5–20 m camera distance, and 720p/1080p.
Restrained recoil is the default; `-Recoil strong` reproduces the alternative.
Checkpoint names are defined in [GameHost.Review.cs](../game/scripts/GameHost.Review.cs).
Input and performance modes use their full profile with the default checkpoint.

Outputs are under `artifacts/solo-review/`, separated by profile. `review.json`
contains state, sampled poses, grip/muzzle measurements, projectile travel,
effect delays, and occlusion. Capture/record gates check weapon size, armed grip
fit, muzzle direction, and retry cleanup. Input checks cover availability of
Stop, projectile pause/travel/arrival, and victory/recruitment or defeat/retry.
Inspect live playback or the exact recording for every visual change; stills
alone can miss accumulated offsets and transition defects.

MovieWriter always records 1080p at fixed 60 fps and may encode slower than real
time. It cannot measure frame pacing. Performance mode instead measures
wall-clock `FramePostDraw` intervals, simulation/presentation CPU, command-to-first-
frame latency, and pipeline compilation deltas after warmup. This excludes OS
input and external-tool latency. Record hardware, resolution, and cache conditions
when comparing results.

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
3. Interrupt a telegraphed strike with Suppressive Fire and use Field Aid after
   taking damage. Both must resolve once and resume the assigned basic attack.
4. Inspect muzzle launch and impact/number alignment. Pause during draw, bolt
   flight, and strike: animation/effects freeze together; camera/UI still work.
5. Win, cross the far door, and recruit Protector. Check the two-member party
   and slice-complete HUD; later combat and final completion remain unavailable.
6. Lose on another run; test Retry button and Enter across separate attempts.
   Preserve prior route progress, reset the encounter without duplicates, and
   ignore X when Stop is unavailable in dialogue, defeat, or securing.
7. Pan/yaw/pitch/zoom around both doors; inspect wall/lintel restoration and
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

Example schema-v3 adapter command:

```json
{"schema_version":3,"command_id":"review.pause","type":"set_pause","payload":{"paused":true}}
```

Prefer these bounded helpers over arbitrary scene mutation. For ad hoc live
control, `plugin-link` links the external `godot-ai-plugin`; override its source
with `-GodotAiPlugin` or `SPACE_ADVENTURE_GODOT_AI_PLUGIN`, then enable **Godot AI
Control** in the editor. Keep its local project entries uncommitted. The plugin
uses the first free port in 6550–6569; explicit `GODOT_AI_PORT` or a matching
`GODOT_AI_PORT_RANGE` can isolate parallel sessions. Never recursively clean
inside the addon junction.
