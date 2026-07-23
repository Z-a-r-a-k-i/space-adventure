# SpaceAdventure

SpaceAdventure is the working title for a single-player 3D science-fiction party RPG. Combat is deliberate real time with active pause, inspired by the clarity and direct party control of *Aarklash: Legacy*. Exploration and conversation are first-class actions. Ship command, vehicle combat, boarding, procedural runs, and controlled generative dialogue are later layers rather than prerequisites for the first playable game.

## Repository status

Phase 1, the C# technical bootstrap, is complete. The Phase 2 start-to-destination walking skeleton is implemented, including its automated and graphical verification surfaces and a greybox wall-cutaway POC. A human playthrough is still pending, so Phase 2 has not passed its exit gate and Phase 3 remains deferred.

The current main scene is a small authored station route, not the complete 8–12 minute POC. It provides one protagonist, point movement over a real navigation mesh, tactical pause, a fixed survivor exchange, an optional service-terminal interaction, an evacuation-airlock objective, and explicit completion. Party control, kit selection, recruitment, branching dialogue, combat, inventory, and runtime model dialogue are later phases.

The next action is a human playthrough of the Phase 2 route and correction of any usability blocker it reveals. Follow the short protocol in `docs/PLAYTESTS.md`; milestone sequencing remains in `docs/ROADMAP.md`.

## Agreed foundation

- Godot 4.7.1 Mono, Forward+ renderer, Windows desktop first.
- C# game code with a pure `SpaceAdventure.Core` rules project.
- An authored 8–12 minute greybox demo before roguelite or procedural systems.
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
pwsh -NoProfile -File scripts/dev.ps1 restore
pwsh -NoProfile -File scripts/dev.ps1 build
pwsh -NoProfile -File scripts/dev.ps1 test
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name bootstrap
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 import
pwsh -NoProfile -File scripts/dev.ps1 headless -Name bootstrap
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 capture -Name wall-cutaway
pwsh -NoProfile -File scripts/dev.ps1 run
```

`scenario -Name station-route` completes the pure-core route with a deterministic pathfinder and emits JSON Lines. `headless -Name station-route` loads the real Godot scene and navigation mesh, completes the survivor response, optional terminal, and airlock path, then exits nonzero on failure. `capture -Name wall-cutaway` opens a bounded, noninteractive 1280×720 graphical run, drives the protagonist to a deterministic review state, pauses gameplay, and exercises the live cutaway controller through settled cut → clear-view restore → re-cut checkpoints by changing camera yaw. It then returns to the original camera view and writes `artifacts/visual/captures/wall-cutaway.png` plus a JSON evidence manifest. The command validates the lifecycle and manifest contracts, PNG dimensions and byte length, and the manifest SHA-256 against the generated PNG. The final image still requires visual inspection, and automation does not prove perceived transition smoothness or absence of flicker. `run` launches the playable station route. `run -AutoQuitSeconds <n>` makes that graphical launch bounded by `-TimeoutSeconds` (60 seconds by default). `editor` opens the project editor. `help` lists every implemented option. The root `Makefile` provides equivalent short targets, including `make capture`.

## Current controls

- Right-click navigable floor to issue a move order.
- Right-click the survivor, service terminal, or evacuation airlock to issue a contextual interaction order. A distant interaction automatically includes the approach path.
- Click the authored dialogue response, or press `1`, `Enter`, or keypad Enter.
- Press `Space` to toggle tactical pause. Orders issued while paused remain pending and the newest pending primary order replaces the previous one.
- Use `WASD` or the arrow keys to pan; `Q`/`E` or middle-mouse drag to rotate; Page Up/Page Down or vertical middle-mouse drag to adjust pitch; and the wheel to zoom.
- Press `Home` or `R` to reset camera orientation and `F` to focus the protagonist.

When a station wall blocks the camera-to-protagonist view, the opaque wall mesh collapses vertically toward a short 0.45 m base over a brief transition and restores when the view clears. This initial POC uses cached world-space AABBs because the current walls are static, axis-aligned greybox boxes. It is not the production solution for rotated, moving, concave, multi-storey, decorated, or imported environment geometry; later levels should use separately authored cutaway presentation, occlusion volumes or room/floor metadata, and a dithered transition where needed.

The intended manual route is: interact with the orange survivor, choose the single response, optionally inspect the purple service terminal, then interact with the green evacuation airlock. The completion overlay is the end of the Phase 2 slice.

For graphical agent control, `plugin-link` creates an ignored junction to the external `addons/godot_ai` directory. Override its source with `-GodotAiPlugin` or `SPACE_ADVENTURE_GODOT_AI_PLUGIN`. Then open `editor` and enable **Godot AI Control** in Project Settings → Plugins.

Enabling the addon may write local `[editor_plugins]` and `[autoload]` entries into `game/project.godot`. Those entries are temporary development state and must not be committed. The base project intentionally contains neither entry and is verified to build and launch headlessly without the addon. The plugin chooses the first free port in `6550-6569`; concurrent setups may override `GODOT_AI_PORT` or configure a matching `GODOT_AI_PORT_RANGE` for both editor and MCP process.

Useful but stage-specific tools:

- `godot-ai-plugin`: optional graphical agent control, input injection, runtime inspection, and screenshots.
- Worktrunk (`git-wt` on Windows): convenient worktree management for parallel agents.
- Blender 5.2 LTS plus the official Blender MCP: model and asset-pipeline work only.
- ImageMagick, FFmpeg, glTF Transform, and the Khronos glTF Validator: install when their corresponding visual-review and publication stages are implemented, not as bootstrap blockers.

## Documentation map

- `docs/PRODUCT.md`: product vision and scope boundaries.
- `docs/POC.md`: the first playable demo and acceptance gates.
- `docs/ARCHITECTURE.md`: C# and Godot ownership boundaries.
- `docs/DECISIONS.md`: accepted architectural decisions.
- `docs/AGENT-AUTOMATION.md`: testability and parallel-agent workflow.
- `docs/PLAYTESTS.md`: manual milestone-gate protocol and retained outcomes.
- `docs/DIALOGUE-AI.md`: controlled dialogue-provider design.
- `docs/ART-PIPELINE.md`: asset creation, publication, and visual QA.
- `docs/ROADMAP.md`: milestone order and exit gates.
- `docs/OPEN-QUESTIONS.md`: unresolved choices and recommended defaults.

Phase 2 automation and agent-controlled graphical verification are implemented. The remaining Phase 2 gate is an independent human playthrough of the real route, including the wall-visibility check in `docs/PLAYTESTS.md`; do not begin Phase 3 merely because the automated path or visual capture completes.
