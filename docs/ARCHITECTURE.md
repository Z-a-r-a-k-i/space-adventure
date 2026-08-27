# Architecture

## Purpose

The architecture exists to make one small game fast to change, easy to test, and safe for several agents to work on. It separates authoritative rules from Godot integration without predicting every future RPG, vehicle, procedural, or model-provider system.

## Dependency shape

```text
Human input --------> Godot input adapter --------+
Godot AI/MCP -------> Godot automation adapter ---+---> typed command dispatcher
Godot scenario -----> Godot automation adapter ---+                |
Simulation CLI -----> CLI adapter -----------------+                v
Future replay ------> replay adapter --------------+       SpaceAdventure.Core
                                                                     |
                                     snapshots <---------------------+
                                     events <------------------------+
                                                                     |
                         Godot presentation <-------------------------+
```

All control paths create the same typed C# commands and receive the same acknowledgement or rejection types. JSON is used only where a process or tool boundary requires it.

The intended project dependency graph is one-way:

```text
SpaceAdventure.Core.Tests ---> SpaceAdventure.Core <--- SpaceAdventure.SimCli
                                         ^
                                         |
                                 SpaceAdventure.Game
```

`SpaceAdventure.Core` never references `SpaceAdventure.Game` or Godot assemblies.

## Project responsibilities

### `src/SpaceAdventure.Core`

Pure C# targeting `net8.0`. It owns:

- Stable identifiers and gameplay value types.
- Party, character, combat, item, interaction, and scenario state.
- Typed gameplay commands and complete pre-mutation validation.
- Command acknowledgements and structured rejection reasons.
- The fixed simulation clock, tactical pause, and deterministic ordering rules.
- Ability execution, cooldowns, damage, healing, AI decisions, victory, defeat, and completion rules.
- Seeded randomness through an explicit source.
- Gameplay events and immutable observation snapshots.
- Content contracts and validation independent of Godot resources.
- Dialogue requests, proposals, validation results, and permitted state effects.

The core does not know about nodes, scenes, input events, cameras, meshes, animations, audio, editor plugins, MCP, file dialogs, or viewport capture.

The implemented station-route slice makes this boundary concrete. `GameSession` owns party and hostile position, current and pending primary actions, interaction state, authored dialogue, health, cooldowns, item charges, encounter phases, objectives, and progression. The schema-v4 `station-route-v6` content orders survivor choice, entry-service-door opening, the solo Security Enforcer fight, victory-gated solo exit, and Protector recruitment. The final airlock and later main encounter remain unavailable. `StationRouteDefinition` contains validated content while `StationRouteLayout` supplies stable spatial and encounter placements. `ISpatialPathfinder` is the only engine-facing spatial contract; it accepts pure `WorldPosition` values and returns an immutable path result.

### `game/SpaceAdventure.Game`

The Godot 4.7.1 .NET project references the core and owns:

- Scene composition, level geometry, navigation data, collision, and spawn markers.
- Input mapping and conversion from clicks or UI actions to typed commands.
- A `GameHost` node that owns one core `GameSession` for the current run.
- Character and world presentation synchronized from snapshots and events.
- Camera, selection indicators, target previews, HUD, dialogue UI, animation, effects, and audio.
- Loading authored level and presentation data into validated core definitions.
- Godot headless integration fixtures and the stable runtime automation node.

Godot nodes are replaceable views and adapters. They may cache presentation state, but they do not become an alternate source of gameplay truth.

For the station route, `station_route.tscn` owns the five-area serpentine layout, invisible floor collision and navigation wrappers, lights, camera, spawn and approach markers, interaction views, and production GLB instances. Navigation is split at both service doors. The entry `NavigationLink3D` enables when the authoritative interaction becomes available, so pathfinding can route through the still-closed unlocked door. Core movement completes that interaction on approach; the blocker and animated leaves then synchronize from completed state. The solo-exit link stays disabled, while always-on links preserve continuity between the inaccessible Protector, main-arena, and final-approach islands without bypassing that gate. `GameHost` validates gameplay `stable_id` metadata against `station-route.json`, translates ray hits and dialogue UI input into typed commands, and renders core observations. `GodotSpatialPathfinder` performs bounded, validated `NavigationServer3D` path queries. It never moves the protagonist node as gameplay authority; the node is positioned from the latest core observation.

### `tools/SpaceAdventure.SimCli`

A console application references the core and runs rule-level scenarios without launching Godot. It provides `bootstrap` and `station-route` scenarios and emits JSON Lines containing metadata, command results, events, bounded advancement results, assertions, and a final snapshot. The station-route scenario loads the same versioned content as Godot but uses a deterministic fixture layout and pathfinder. Versioned external scenario inputs remain a later extension. The CLI is optimized for fast agent iteration, reproduction, and regression—not for validating the real navigation mesh, rendering, or input.

### `tests/SpaceAdventure.Core.Tests`

Fast tests construct small fixtures directly and do not load Godot. They cover the pause clock and bounded event retention plus schema-v4 content validation, exact door-effect counts, layout-ID validation, fixed-tick movement, pause and pending-order replacement, unreachable-order atomicity, both authored survivor choices and terminal consequences, atomic door progression, combat start/readiness, repeating attacks, positional ability interruption, cooldowns, one-charge healing, victory progression, defeat pause, retry isolation, and post-fight Protector recruitment. The later main encounter and generated-dialogue rules remain future work.

## Game session and fixed time

`GameSession` is the authoritative runtime aggregate for one scenario. It advances at 30 gameplay ticks per second.

In real time, the Godot host accumulates frame delta and requests whole ticks. Under tactical pause, it requests no gameplay ticks; input, camera, UI, observation, and command submission continue. Development tools may advance an exact number of ticks while paused. Single stepping is a development capability, not necessarily a player-facing control in the final game.

Systems iterate entities in stable identifier order when order affects results. Randomness is obtained only from the session's seeded random source. We target reproducible rule state and events under the same content and build, not bit-identical animation, physics, or navigation across machines.

## Spatial boundary

Combat position is gameplay state, so presentation cannot move a character independently and later report the result as truth.

The implemented `ISpatialPathfinder` contract finds a path for a stable actor ID between pure `WorldPosition` values. `GodotSpatialPathfinder` waits for the authored map to synchronize, snaps origin and destination within small tolerances, queries `NavigationServer3D.MapGetPath`, rejects empty, excessive, non-finite, disconnected, or endpoint-mismatched results, and converts accepted points back to core values. The deterministic CLI/test implementation supplies the same contract without Godot.

The core stores accepted waypoints and advances position at the authored movement speed on the fixed clock. Contextual interaction orders path to an authored approach point and apply their effect only after arrival and range validation. Godot renders the observed position. No `NavigationAgent3D`, physics body, or presentation callback becomes an alternate movement authority, and no general physics abstraction is introduced.

## Camera occlusion boundary

Wall cutaway is presentation state, not gameplay state. The Phase 2 controller discovers explicitly tagged static wall meshes, caches their full transforms and world-space axis-aligned bounding boxes, and tests an expanded camera-to-protagonist segment against those cached bounds with entry/release hysteresis. A blocking mesh remains opaque while its local Y scale and position animate toward a bottom-anchored 0.45 m stub over 0.15 seconds; it restores when it no longer blocks the view. This does not mutate core position, navigation, collision, interaction state, or command results.

The cached-AABB approach is deliberately narrow. It is deterministic, cheap, and inspectable for the current static axis-aligned wall panels, but it over-approximates non-box silhouettes and becomes invalid for rotated, animated, deforming, concave, streamed, or multi-floor environments. Production walls are discovered recursively from each imported GLB node's `extras.occluder_id` metadata; there is no scene-code wall-name map. Scaling a complete wall still compresses trim and provides a whole-panel collapse rather than a local view hole. Production levels should move to separately authored upper/base presentation, occlusion volumes and room/floor visibility metadata, with a dithered or otherwise sorting-safe visual transition, while retaining stable occluder IDs and structured observation.

## Command contract

Commands are explicit C# types under a common gameplay-command contract. The current route implements:

- `SetPauseCommand`.
- `ChooseProtagonistKitCommand` with the fixed Vanguard POC kit.
- `MoveActorCommand` with a stable actor ID and world destination.
- `MovePartyCommand` with stable actor IDs and a formation destination.
- `InteractCommand` with stable actor and interaction IDs; it includes approach movement when needed.
- `ChooseDialogueResponseCommand` with stable actor, interaction, and authored response IDs.
- `AssignBasicAttackTargetCommand` with stable actor and hostile IDs.
- `UseAbilityCommand` with a stable ability ID and typed position target.
- `UseItemCommand` with stable actor, item, and ally target IDs.
- `RestartEncounterCommand` with a stable encounter ID.

Expected later POC commands include the Protector's attack/ability orders and
any explicit main-encounter interaction required by that bounded slice.

Selection may remain player-control state in the Godot host rather than world simulation state, but the resulting orders always name stable actor IDs. Automation never depends on which portrait happens to be selected in a UI.

The dispatcher validates identity, scenario phase, control ownership, target shape, range, path availability, cooldown, resource cost, and other preconditions before applying a command. It returns an acknowledgement containing the command ID and accepted result or a stable rejection code with safe details. Rejections also enter the event stream.

The external JSON envelope contains a schema version, command ID, command type, and command-specific payload. Serialization DTOs translate into typed commands; raw dictionaries do not flow through gameplay code.

## Actions and active pause

Each party member has a current action and at most one pending primary action in the POC. While paused, a newly accepted primary order replaces that character's prior pending order. On resume, pending actions become eligible in stable order. There is no arbitrary action queue, timeline scripting, or programmable behavior system.

Movement, contextual interaction, repeating attacks, position-targeted ability use, and healing-item use all share this primary-action boundary. A running order replaces the current primary action. A paused order replaces only the pending primary action, so the character remains stationary until resume; the newest accepted pending order wins. Development-only paused stepping executes the same fixed-tick rule path for deterministic scenarios and is capped at 3,000 ticks per call.

Basic attacks repeat against an explicitly assigned target until invalidated or replaced; active abilities and items remain explicit orders. Combat start triggers one automatic pause. Defeat also pauses atomically so retry can be inspected safely; victory does not. All other POC pausing is manual.

## Combat attack and presentation boundary

Every combatant's basic attack has a stable gameplay identity independent of
how the model appears to deliver it. Handheld, integrated, and body-based
attacks use the same authoritative command, action, timing, and damage path.
The core owns target validation, range and affected area, approach movement,
fixed-tick wind-up and recovery, resolution, interruption, repetition, damage,
and defeat.

Godot maps the stable attack identity to a reviewed presentation profile. That
profile owns the weapon scene when separate, attachment nodes, rig and clips,
muzzle or contact markers, telegraph rendering, effects, and audio. The core
owns any telegraph geometry or timing that communicates authoritative danger.
Model node paths, sockets, animation callbacks, and physical weapon collisions
never apply damage or advance authoritative state.

Attack observations and typed wind-up, attack-release, ability-release,
damage, healing, interruption, defeat, and encounter events expose the phase
and fixed-tick timing required for presentation. Combat clips remain in-place for the
POC: Godot follows observed gameplay position, and animation root motion does
not become an alternate movement authority. See `ATTACK-PRESENTATION.md`.

## Events and observations

Events are immutable facts with at least an event sequence, simulation tick, event type, and typed data. The runtime retains a bounded recent buffer for tools and presentation. Implemented route events include command acceptance/rejection, protagonist-kit selection, primary-action assignment/failure, movement arrival, dialogue and recruitment, interaction completion, objective change, encounter lifecycle, attack wind-up/release, ability release, damage, healing, interruption, combatant defeat, and scenario completion. Cooldowns and item charges are observable state; animation callbacks never emit authoritative combat facts.

The current observation additionally contains available and selected protagonist
kits, route-power choice, party loadouts and combat state, hostile
identity/position/action/health, encounter phase and transition ticks,
cooldowns, and item charges. The external automation projection is JSON-safe,
snake-case, and versioned at its command boundary.

Use observations and events to answer what happened. Use screenshots and live play to judge composition, readability, animation, art, and input feel.

## Content boundary

The POC uses small versioned text definitions for gameplay data and Godot scenes for spatial layout and presentation references. The implemented `game/content/station-route.json` defines schema 4/revision `station-route-v6`, actor loadouts and health, attacks, ability, healing item, encounter transitions, objectives, interaction kinds/radii/effects, authored dialogue, and result text. `station_route.tscn` owns positions, trigger and approach markers, collision, navigation, and production presentation instances. Both are validated into core records and joined by stable IDs before the scenario starts.

Do not add a database, spreadsheet import service, generic graph editor, or mod framework for the POC. If direct text authoring becomes the dominant bottleneck, add an authoring adapter while preserving the validated core contracts.

## Dialogue boundary

A dialogue provider receives a bounded request containing only relevant world facts, conversation state, allowed intents, and permitted moves. It returns a structured proposal. Deterministic validators check identity, revisions, knowledge, references, size limits, intents, and state-transition preconditions. Accepted effects become ordinary gameplay commands or authored state transitions.

The POC uses the scripted provider. The current route implements one authored survivor line and two authored responses, selected through `ChooseDialogueResponseCommand`; either response atomically completes the survivor interaction, records the route-power choice, advances the objective, and makes the entry service door available. Moving through its unlocked navigation link completes and opens the door on approach, then advances to and triggers the solo tutorial encounter. Combat victory makes the far service door available; crossing it makes the authored Protector recruitment interaction available. Choosing its recruitment response adds the Protector to the party, while the following main encounter and final airlock remain locked. These explicit effects are intentionally not a branching dialogue or generic gate framework. An optional local development provider may later invoke the official Codex CLI with ChatGPT sign-in; manual-inbox and recorded providers support inspection and deterministic replay. These are experiment tools, not dependencies of the playable scenario. Model, reasoning effort, and Fast mode are independent per-request profile settings and never enter authoritative or saved gameplay state. See `DIALOGUE-AI.md`.

## Automation boundary

The Godot game exposes a stable C# runtime node named `AutomationBridge`. It
returns complete route observations, retained events since a sequence with
explicit gap metadata, and schema-v3 command acknowledgements. Its JSON adapter
supports protagonist-kit selection, pause, individual and party movement,
interaction, dialogue response, basic-attack target, position ability, healing
item, and encounter retry commands. Explicit helpers provide pause/resume,
exact paused stepping, advancement until a named event with a maximum
3,000-tick budget, stable-ID-to-screen projection, a bounded known-target
context-click input hook, and clean shutdown. A fresh process resets the current
scenario; retry resets only combat attempt state. The bridge does not expose
arbitrary property setters or code evaluation.

The optional external `godot-ai-plugin` may call those methods and additionally provides scene inspection, real input injection, runtime inspection, and ad hoc viewport capture. Enabling it can temporarily add editor-plugin and autoload entries to `project.godot`; those local entries are not part of the base project. Automated tests and the shipped game do not require the plugin.

The built-in `wall-cutaway` visual-capture mode is a narrower deterministic diagnostic boundary. It reaches a stable gameplay event through normal commands, pauses gameplay, and drives live camera yaw through settled blocking, clear-view, and re-blocking states. It then returns to the original camera view, records the cutaway lifecycle and gameplay observations, captures a 1280×720 graphical frame, writes an atomic schema-v1 JSON/PNG pair, and exits. `scripts/dev.ps1 capture -Name wall-cutaway` independently verifies the lifecycle result, fixed identity, pass flag, dimensions, byte length, and PNG SHA-256. Capture mode does not expose arbitrary scene mutation, establish perceived transition quality, or participate in normal player flow.

## Diagnostics

Both CLI scenarios record game and runtime versions, content revision, scenario ID, seed, command results, events, assertions, duration, and final outcome. The station-route scenario additionally records its deterministic pathfinder and tick budgets. The real-Godot smoke prints navigation readiness, a structured final summary, and per-step diagnostics on failure. Failures should produce enough structured output to reproduce them without reading a screenshot.

Godot automation uses the console executable and isolated user-data directories so parallel runs do not share editor state or logs. Deterministic graphical captures use their own validated user-data scope beneath the ignored artifact root rather than sharing the existing automated or interactive directories.

## Deliberate non-architecture

The POC has no ECS, networking stack, event-sourcing framework, dependency-injection container, message broker, generic quest engine, procedural generator, save-migration framework, model-provider framework, or custom engine build. Add an abstraction only at a demonstrated replacement boundary.

## Parallel-change boundaries

Core command and event contracts, `project.godot`, shared scenes, imported-asset registries, and binary Blender sources are merge hotspots and have one owner at a time. Good parallel units are an isolated core rule, test fixture, UI scene, automation scenario, asset ID, or documentation decision. See `AGENT-AUTOMATION.md`.
