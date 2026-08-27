# Agent automation and parallel development

## Principle

Agents need three complementary execution channels:

- Pure C# tests and simulation scenarios for fast rule feedback.
- The real Godot game in headless mode for engine integration.
- The real graphical game for input, navigation, camera, UI, animation, effects, readability, and screenshots.

Use structured state and events to determine what happened. Use live interaction and images to judge how it felt and looked. Visual checks are mandatory for visual changes.

## Current command surface

The repository provides one canonical PowerShell entry point:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 help
pwsh -NoProfile -File scripts/dev.ps1 doctor
pwsh -NoProfile -File scripts/dev.ps1 restore
pwsh -NoProfile -File scripts/dev.ps1 build
pwsh -NoProfile -File scripts/dev.ps1 test
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name bootstrap
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 plugin-link
pwsh -NoProfile -File scripts/dev.ps1 import
pwsh -NoProfile -File scripts/dev.ps1 headless -Name bootstrap
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-combat-defeat
pwsh -NoProfile -File scripts/dev.ps1 capture -Name wall-cutaway
pwsh -NoProfile -File scripts/dev.ps1 editor
pwsh -NoProfile -File scripts/dev.ps1 run
```

Only implemented subcommands may appear in `README.md` as usable. Asset publication and multi-angle review commands remain future art-pipeline work.

The script accepts an explicit `-Godot` path and otherwise resolves `SPACE_ADVENTURE_GODOT` or a small documented candidate list. Godot automation uses the console executable so stdout and stderr are available to agents. `-TimeoutSeconds` bounds automated Godot processes, including graphical captures; a graphical `run` becomes automated and bounded when `-AutoQuitSeconds` is supplied.

`doctor` reports the exact pinned .NET and Godot versions, required command and project paths, addon-link presence, and the active Godot AI port policy without mutating the project. Art tools are checked only when a future art command requires them.

## Runtime automation contract

The Godot host exposes a stable C# node named `AutomationBridge` after the authored navigation map has synchronized. Its public methods are:

- `GetObservationJson()` — complete clock, pause, and station-route observation.
- `GetEventsJson(sinceSequence)` — retained events after a sequence, plus oldest/latest sequence and history-gap metadata.
- `SubmitCommandJson(commandJson)` — schema-v3 command envelope translated into the same typed dispatcher used by input, tests, and the CLI.
- `SetPaused(paused)` — convenience wrapper around the typed pause command.
- `AdvanceExactTicks(count)` — deterministic development stepping from 0 to 3,000 ticks per call; the session must already be paused.
- `AdvanceUntilEventJson(afterSequence, eventType, maximumTicks)` — step while paused until a named event or budget exhaustion. The budget must be 1–3,000 ticks, and the result always includes the last observation.
- `GetScreenPositionJson(stableId)` — read-only viewport projection for the protagonist or a known interaction, including a `visible` flag.
- `InjectContextClickJson(stableId)` — emit a right-click `InputEvent` at a visible known projection without exposing arbitrary screen coordinates. Its result reports `injected`, the projected point, and `event_sequence_before`; injection is not command acceptance, so confirm the resulting `input.*` command through `GetEventsJson(event_sequence_before)` and observation.
- `Shutdown(exitCode)` — clean process exit.

The command envelope always contains `schema_version`, `command_id`, `type`, and an object `payload`. Implemented command types are:

```json
{"schema_version":3,"command_id":"example.pause","type":"set_pause","payload":{"paused":true}}
{"schema_version":3,"command_id":"example.move","type":"move_actor","payload":{"actor_id":"actor.protagonist","destination":{"x":-10.0,"y":0.0,"z":2.75}}}
{"schema_version":3,"command_id":"example.attack","type":"assign_basic_attack_target","payload":{"actor_id":"actor.protagonist","target_id":"actor.enemy.security_enforcer.solo"}}
{"schema_version":3,"command_id":"example.ability","type":"use_ability","payload":{"actor_id":"actor.protagonist","ability_id":"ability.crew.vanguard.suppressive_fire","target_position":{"x":-10.0,"y":0.0,"z":-1.4}}}
{"schema_version":3,"command_id":"example.item","type":"use_item","payload":{"actor_id":"actor.protagonist","item_id":"item.healing.field_aid.v1","target_actor_id":"actor.protagonist"}}
{"schema_version":3,"command_id":"example.retry","type":"restart_encounter","payload":{"encounter_id":"encounter.station.solo_tutorial"}}
```

The observation projection uses snake-case JSON and contains tick, pause state, scenario/content identity, party and hostile combat state, current/pending actions with phases and remaining ticks, encounter phase, cooldowns, item charges, objectives, interactions, and dialogue. Event projections include typed encounter, attack, ability, damage, healing, interruption, and defeat details. The adapter returns JSON-safe values and stable error codes; it never exposes arbitrary property mutation, script evaluation, node deletion, unrestricted method invocation, or private core state. Encounter retry is a narrow gameplay command, not a scenario-reset backdoor.

Automation commands name stable actor and target IDs. They do not depend on UI selection or incidental node paths.

## Simulation scenarios

`SpaceAdventure.SimCli` runs the pure core. `scenario -Name bootstrap` proves the minimal pause contract. `scenario -Name station-route` loads schema-v4 `station-route.json`, uses a deterministic fixture layout/pathfinder, performs the survivor, terminal, and entry-door flow, fights the solo Enforcer with basic fire, Suppressive Fire, and Field Aid, opens the victory-gated exit, and verifies Protector availability while the final airlock remains locked. Both emit JSON Lines containing run metadata, typed combat events, assertions, duration, and the final snapshot, and exit nonzero on failure.

Later milestones may add external versioned scenario files containing:

- Scenario ID, content revision, seed, and initial fixture.
- Ordered commands and controlled tick advancement.
- Intermediate and final assertions.
- Optional event expectations and maximum tick budgets.

A scenario fixture is a reproducible rule bug report. It is not proof that a Godot level, navmesh, UI, or animation works.

## Godot headless scenarios

Both `headless` modes launch the real Godot project with isolated user data and a hard process timeout:

- `headless -Name bootstrap` proves C# assembly/scene startup, valid pause submission, malformed-envelope rejection, observation, and clean shutdown.
- `headless -Name station-route` traverses the authored navigation, completes the survivor and terminal flow, auto-opens the entry door, fights the solo Enforcer, exercises Suppressive Fire and Field Aid, verifies combat presentation and victory-gated navigation, opens the solo exit, and stops at the now-available Protector.
- `headless -Name station-combat-defeat` deliberately loses the same fight, verifies the atomic defeat pause and retry UI state, submits the typed retry command, and proves that health, charges, cooldowns, actors, and encounter state reset while completed route state remains intact.

The station-route smoke proves engine integration and the real navigation mesh. It is not evidence that click targets, camera feel, text placement, color, or route comprehension are good for a human.

On Windows, scripts redirect `APPDATA` and `LOCALAPPDATA` to a worktree-local ignored directory for the test process. The capture command uses the validated child scope `artifacts/godot-user/scopes/capture-wall-cutaway`, distinct from the existing automated and interactive roots. This prevents a capture from sharing Godot editor data or caches with those sessions.

## Graphical sessions

Graphical checks exercise the actual viewport and input mappings. The optional external `godot-ai-plugin` may:

- Launch and stop the game.
- Inspect the runtime scene tree and public runtime properties.
- Invoke the explicit `AutomationBridge` methods.
- Inject keyboard and named input actions.
- Capture the game viewport.
- Inspect editor logs, debugger errors, dialogs, and unsaved scene state.

The plugin is a development accelerator, not a game dependency. Create its ignored junction with `plugin-link`, open `editor`, and enable **Godot AI Control** only in a worktree that needs live control. Enabling it may write `[editor_plugins]` and `[autoload]` entries to `game/project.godot`; remove those temporary local changes before integration. Ad hoc viewport capture remains an MCP capability. The single deterministic `capture -Name wall-cutaway` regression command is built into the game and does not require the addon. A human can perform the same playable flow without the addon.

For the station route, use `GetScreenPositionJson(stableId)` to locate a known 3D target. An OS-level control tool or a human can right-click that viewport position; when no pointer injector is available, `InjectContextClickJson(stableId)` sends the same Godot mouse event through ray picking and the normal input-to-command path. The helper confirms only that the input was injected. Read events after its returned `event_sequence_before` and require the expected `input.*` command acknowledgement before treating the click as successful. The current graphical path is survivor → optional terminal → auto-opening entry door → solo fight → victory-gated exit → Protector. Verify both victory and retry after defeat. The agent check must include graphical input events, while an independent human check remains the proof of physical pointer usability and feel.

Current controls are right-click for movement, interaction, or a repeating basic-attack target. Orders issued during tactical pause remain pending until `Space` resumes the simulation. `1` enters Suppressive Fire position targeting and left-click confirms it; `2` queues Field Aid; Escape cancels ability targeting; Space toggles tactical pause. Camera-relative WASD/arrows pan, Q/E or middle drag yaw, Page Up/Page Down or vertical middle drag pitch, wheel zooms, Home/R resets orientation, and F focuses the protagonist. The opening camera focus is Vanguard's spawn. `E` is camera yaw, not an interaction shortcut.

Short visual effects are poor synchronization points. Drive the game to a structured event or stable review state, then capture the viewport. Record the scenario, event sequence, camera profile, resolution, and screenshot path with visual-test artifacts.

### Deterministic wall-cutaway capture

Run:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 capture -Name wall-cutaway
```

This is a graphical, windowed 1280×720 run, never a headless substitute. The game reaches the start-room terminal approach through a bounded `movement_arrived` state, keeps gameplay paused, applies the fixed 14.5 m capture camera, and waits until exactly `presentation.wall.start.west` is requested and fully settled as the cutaway. It then changes live camera yaw to clear the view and waits for restoration, returns to the original yaw and waits for the same wall to re-cut, and only then captures the original final view. The run records all three cutaway checkpoints and the complete gameplay observation, atomically writes `artifacts/visual/captures/wall-cutaway.png` and `wall-cutaway.json`, then exits. The manifest uses `schema_version: 1`, `capture_id: "wall-cutaway"`, `passed: true`, and includes stable-state, viewport, camera, cutaway-lifecycle, observation, and image evidence. The wrapper rejects missing or stale output, a failed lifecycle checkpoint, a wrong schema/capture/result, a non-1280×720 PNG, mismatched byte length, a malformed hash, or a SHA-256 that does not match the PNG.

The hash is an evidence-integrity check, not a cross-GPU golden-image assertion. Inspect the PNG and confirm that the protagonist and selection feedback are readable, the expected blocking upper wall is absent, the short base remains, unrelated walls remain opaque, and the HUD is not obscured. Retain the JSON beside the reviewed PNG so the exact camera, stable state, observed occluder IDs, renderer, and gameplay state remain attributable. The settled checkpoints verify deterministic cut, restoration, and re-cut state changes while gameplay remains paused; they cannot establish perceived animation smoothness, transient flicker, camera feel, or usability. Those remain human-playtest checks.

## Visual verification

Every change to scenes, camera, UI, shaders, materials, models, animation, effects, or input requires:

1. A graphical run at the relevant state.
2. At least one viewport capture.
3. Inspection at sufficient resolution.
4. A concise statement of what was checked and any limitation.

Headless success alone cannot validate clipping, scale, composition, selection feedback, target clarity, animation, or usability.

Phase 2 provides the automated and agent-controlled graphical verification path, including the deterministic wall-cutaway capture, but its exit gate remains open until a human independently completes the real route, checks wall visibility while moving and rotating the camera, and any usability blocker is corrected. Do not treat automated completion or a passing capture manifest as authorization to begin Phase 3.

## Parallel-agent workflow

1. Start from a clean integration branch and create one branch plus worktree per implementation agent. Worktrunk may create and manage them through `git-wt` on Windows.
2. Give each task a narrow outcome, explicit file or subsystem ownership, dependencies, and executable exit gate.
3. Assign one integration owner for core contracts, `project.godot`, shared scenes, content schemas, and imported-asset registries.
4. Publish assumptions and contract changes before another task depends on them.
5. Integrate the smallest dependency first and run the receiving subsystem's tests after each integration.
6. Never share a running Godot editor, `.godot` directory, redirected user-data directory, log directory, or MCP port between worktrees.
7. Give each graphical worktree a distinct plugin port. The default addon behavior selects the first free port in `6550-6569`; set `GODOT_AI_PORT` only when an exact port is useful, or configure the same `GODOT_AI_PORT_RANGE` in both editor and MCP process.
8. Recreate the ignored external `game/addons/godot_ai` junction in each worktree that needs graphical control.
9. Never recursively delete, move, or clean inside that junction. Confirm it is a link and remove only the link entry when explicitly requested.

Good parallel units include one core rule, one isolated UI scene, one content definition set, one asset ID, one scenario, or one documentation decision. Poor units include overlapping edits to the same scene, `project.godot`, command contract, or binary Blender file.

Native agent collaboration and an integrating parent agent are sufficient initially. Do not build an in-repository task broker or agent messaging service until an observed coordination failure justifies one.

Task assignment does not authorize commits or pushes. Those remain user-controlled.

## Failure classification

When a Godot, .NET, Git, browser, network, cache, installed-tool, or filesystem-visibility check fails in the sandbox, rerun the exact command outside it when permitted. If the outside run passes, report a harness artifact. If both fail in the same way, report a project or machine finding.

Every automated process must be bounded and retain its available stdout and stderr. Scenario drivers should also emit their last observation when one exists. An agent must not silently extend a timeout until a hanging scenario appears to pass.

## Repository-friendly artifacts

Prefer small text scenes, versioned data, JSON scenarios, manifests, and code-built test fixtures. Generated logs, screenshots, videos, contact sheets, caches, and raw candidate assets belong under ignored artifact directories unless intentionally accepted as fixtures or production sources.
