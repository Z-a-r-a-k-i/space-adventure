# SpaceAdventure

SpaceAdventure is the working title for a single-player 3D science-fiction party RPG. Combat is deliberate real time with active pause, inspired by the clarity and direct party control of *Aarklash: Legacy*. Exploration and conversation are first-class actions. Ship command, vehicle combat, boarding, procedural runs, and controlled generative dialogue are later layers rather than prerequisites for the first playable game.

## Repository status

Phase 1, the C# technical bootstrap, Phase 2 walking skeleton, and Phase 3
production-presented station route are complete. Phase 4 is active: the first
bounded solo tutorial combat and its September repair are implemented on
`station-route-v7`. Agent graphical checks are recorded; owner hands-on
acceptance remains pending.

The current main scene is a production-presented authored station route, not
the complete 8–12 minute POC. Vanguard speaks to the survivor, crosses the
entry service door, and fights one Security Enforcer using repeating carbine
fire, position-targeted Suppressive Fire, Field Aid, and tactical pause.
Victory unlocks the far service door; crossing it makes the Protector
recruitment interaction available. Recruiting Protector displays **Current
prototype slice complete**. The later two-character encounter and final
airlock completion remain locked.

The next milestone work is owner acceptance and tuning of the solo fight,
then Protector combat and the main encounter. Milestone sequencing remains in
`docs/ROADMAP.md`.

## Agreed foundation

- Godot 4.7.1 Mono, Forward+ renderer, Windows desktop first.
- C# game code with a pure `SpaceAdventure.Core` rules project.
- An authored 8–12 minute production-presented demo before roguelite or procedural systems.
- A two-character party for the POC, beginning with the player alone.
- One typed command path shared by UI, automation, scenarios, and future replays.
- Structured tests plus real graphical playtests and screenshot inspection.
- Replaceable low-poly art through a controlled Blender-to-GLB-to-Godot pipeline.
- Authored dialogue first; future model output is bounded and validated.

## Repository shape

```text
SpaceAdventure/
|-- docs/                              Product and technical decisions
|-- game/                              Godot 4.7.1 .NET project
|   |-- scenes/
|   |-- scripts/                       Godot adapters and presentation
|   `-- addons/godot_ai/               Optional ignored local junction
|-- src/SpaceAdventure.Core/           Pure authoritative C# rules
|-- tests/SpaceAdventure.Core.Tests/   Fast rule tests
|-- tools/SpaceAdventure.SimCli/       Structured scenario runner
|-- scripts/                           Canonical PowerShell entry points
`-- artifacts/                         Ignored test and visual-review output
```

Add new folders only when a current milestone needs them; the architecture is not a reason to create empty subsystem forests.

## Local baseline

Required for development:

- Godot `4.7.1.stable.mono.official`.
- .NET SDK `8.0.319`, selected by `global.json`.
- PowerShell 7 and Git.

The verified engine directory is:

```text
C:\Program Files\Godot_v4.7.1-stable_mono_win64
```

`scripts/dev.ps1` resolves an explicit `-Godot` argument, then `SPACE_ADVENTURE_GODOT`, then this documented installation. It uses the console executable for logs and automation; Godot does not need to be on `PATH`.

## Current commands

Run these from the repository root:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 doctor
pwsh -NoProfile -File scripts/dev.ps1 path-check
pwsh -NoProfile -File scripts/dev.ps1 restore
pwsh -NoProfile -File scripts/dev.ps1 build
pwsh -NoProfile -File scripts/dev.ps1 test
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name bootstrap
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 import
pwsh -NoProfile -File scripts/dev.ps1 headless -Name bootstrap
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-combat-defeat
pwsh -NoProfile -File scripts/dev.ps1 headless -Name humanoid-gallery
pwsh -NoProfile -File scripts/dev.ps1 headless -Name hostile-gallery
pwsh -NoProfile -File scripts/dev.ps1 capture -Name wall-cutaway
pwsh -NoProfile -File scripts/dev.ps1 review -Mode live -Checkpoint armed -AutoQuitSeconds 120
pwsh -NoProfile -File scripts/dev.ps1 review -Mode input -Sequence victory
pwsh -NoProfile -File scripts/dev.ps1 review -Mode input -Sequence defeat
pwsh -NoProfile -File scripts/dev.ps1 review -Mode performance
pwsh -NoProfile -File scripts/dev.ps1 run
```

`scenario -Name station-route` completes the pure-core solo encounter and
post-victory gate with a deterministic pathfinder and emits JSON Lines.
`headless -Name station-route` exercises the same victory route through the
real Godot scene and navigation mesh. `headless -Name station-combat-defeat`
verifies defeat pause and isolated retry. `hostile-gallery` validates the
published hostile actions and rigid sentry contract. `capture -Name
wall-cutaway` retains the deterministic camera-occlusion evidence path. `run`
launches the playable station route, and `run -AutoQuitSeconds <n>` bounds a
graphical launch. `editor` opens the project editor; `help` lists every option.

`review` reaches reproducible combat checkpoints, records motion, exercises
Godot input events, or measures real-time frame pacing without an editor
plugin. MovieWriter recordings are visual evidence, not performance samples.
See [the solo review guide](docs/SOLO-REVIEW.md) for profiles and
[the recorded results](docs/SOLO-REVIEW-RESULTS.md) for evidence and limitations.

## Current controls

- Right-click navigable floor to issue a move order.
- Right-click the survivor, service terminal, service door, or hostile to issue
  the relevant contextual order. A distant interaction automatically includes
  the approach path. A hostile order starts repeating basic attacks; clicking
  the same target again preserves the attack cycle. Basic fire resumes after
  an ability or Field Aid; movement, interaction, or Stop clears that intent.
- Click either authored dialogue response, press `1` or `2` for the
  corresponding response, or use Enter/keypad Enter for the first response.
- Press `Space` to toggle tactical pause. Orders issued while paused remain
  pending and the newest pending primary order replaces the previous one.
- Press `1`, then left-click a valid world position, to target Suppressive
  Fire; press `Esc` to cancel targeting. Press `2` to use the one-charge Field
  Aid. The encounter pauses automatically at the start of readying; resume
  to play the draw. Orders wait through readying, and new offensive orders
  wait for any released attack's recovery.
- Press `X` or click Stop to clear the selected crew's current and pending
  orders. Stop follows the same pause and readying rules as other orders and
  is unavailable during dialogue, defeat, and victory securing.
- On defeat, press Enter/keypad Enter or click Retry to restart the encounter
  while preserving completed survivor and entry-door progression.
- Use `WASD` or the arrow keys to pan; `Q`/`E` or middle-mouse drag to rotate; Page Up/Page Down or vertical middle-mouse drag to adjust pitch; and the wheel to zoom.
- Press `Home` or `R` to reset camera orientation and `F` to focus the protagonist.

When a station wall blocks the camera-to-protagonist view, the opaque wall mesh collapses vertically toward a short 0.45 m base over a brief transition and restores when the view clears. This initial POC uses cached world-space AABBs because the current production wall panels are static and axis-aligned. It is not the production solution for rotated, moving, concave, multi-storey, or deforming environment geometry; later levels should use separately authored cutaway presentation, occlusion volumes or room/floor metadata, and a dithered transition where needed.

Service-door lintels fade separately when they obscure Vanguard, preserving
door collision and navigation. Carbine bolts leave the actual muzzle; impact
flashes, floating damage values, and the suppression pulse follow visual
arrival. Gameplay damage and interruption still resolve on the release tick.

The current manual route is: talk to the survivor, choose either response,
optionally inspect the terminal, cross the opening entry door, defeat the
Security Enforcer, cross the newly unlocked far service door, and recruit
Protector. The two-member party and slice-complete HUD mark the current
endpoint; the later encounter and final airlock remain locked.

For graphical agent control, `plugin-link` creates an ignored junction to the external `addons/godot_ai` directory. Override its source with `-GodotAiPlugin` or `SPACE_ADVENTURE_GODOT_AI_PLUGIN`. Then open `editor` and enable **Godot AI Control** in Project Settings → Plugins.

Enabling the addon may write local `[editor_plugins]` and `[autoload]` entries into `game/project.godot`. Those entries are temporary development state and must not be committed. The base project intentionally contains neither entry and is verified to build and launch headlessly without the addon. The plugin chooses the first free port in `6550-6569`; concurrent setups may override `GODOT_AI_PORT` or configure a matching `GODOT_AI_PORT_RANGE` for both editor and MCP process.

Useful but stage-specific tools:

- `godot-ai-plugin`: optional graphical agent control, input injection, runtime inspection, and screenshots.
- Worktrunk (`git-wt` on Windows): convenient worktree management for parallel agents.
- Blender 5.2 LTS plus the official Blender MCP: model and asset-pipeline work only.
- ImageMagick, FFmpeg, glTF Transform, and the Khronos glTF Validator: install when their corresponding visual-review and publication stages are implemented, not as bootstrap blockers.

The reproducible setup for the dedicated Tripo/Blender/Godot-review machine is
in `docs/ART-WORKSTATION.md`. The existing `godot-ai-plugin` is the Godot AI
Control integration; no second Godot-control addon is required.

## Documentation map

Use the roadmap for current scope, the architecture and attack contract for
implementation rules, and the solo review guide for executable checks. Dated
audits, decisions, and production records retain historical evidence; a
superseding decision or current repair section takes precedence over an older
implementation description.

- `docs/PRODUCT.md`: product vision and scope boundaries.
- `docs/POC.md`: the first playable demo and acceptance gates.
- `docs/ARCHITECTURE.md`: C# and Godot ownership boundaries.
- `docs/DECISIONS.md`: accepted architectural decisions.
- `docs/AGENT-AUTOMATION.md`: testability and parallel-agent workflow.
- `docs/PLAYTESTS.md`: manual milestone-gate protocol and retained outcomes.
- [docs/SOLO-REVIEW.md](docs/SOLO-REVIEW.md): current combat controls and
  repeatable graphical review profiles.
- [docs/SOLO-REVIEW-RESULTS.md](docs/SOLO-REVIEW-RESULTS.md): recorded agent
  verification, visual findings, and frame-pacing measurements.
- `docs/DIALOGUE-AI.md`: controlled dialogue-provider design.
- `docs/ART-PIPELINE.md`: asset creation, publication, and visual QA.
- `docs/PATH-CONVENTIONS.md`: portable repository path and filename budgets.
- `docs/ART-WORKSTATION.md`: dedicated Windows art-machine installation and
  verification checklist.
- `docs/POC-ASSET-ROSTER.md`: approved model, rig, animation, 2D, and VFX
  target inventory with brief-scoped offline production and phase-scoped live
  integration.
- `docs/ATTACK-PRESENTATION.md`: weapon, attack-source, rig, and animation
  contract for combatant assets.
- `docs/ROADMAP.md`: milestone order and exit gates.
- `docs/OPEN-QUESTIONS.md`: unresolved choices and recommended defaults.

Agent victory, defeat, input, motion, and performance checks are recorded.
The owner-operated physical-input and visual-acceptance protocol in
[docs/PLAYTESTS.md](docs/PLAYTESTS.md) remains the next gate.
