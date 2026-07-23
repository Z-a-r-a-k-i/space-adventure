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

The implemented station-route slice makes this boundary concrete. `GameSession` owns the protagonist position, current and pending primary actions, interaction state, active authored dialogue, objective, and completion. `StationRouteDefinition` contains validated content while `StationRouteLayout` supplies stable spatial placements. `ISpatialPathfinder` is the only engine-facing spatial contract; it accepts pure `WorldPosition` values and returns an immutable path result.

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

For the station route, `station_route.tscn` owns the authored L-shaped floor, collision, embedded navigation mesh, lights, camera, spawn marker, approach markers, interaction views, and tagged wall-cutaway presentation. `GameHost` validates gameplay `stable_id` metadata against `station-route.json`, translates ray hits and dialogue UI input into typed commands, and renders core observations. `GodotSpatialPathfinder` performs bounded, validated `NavigationServer3D` path queries. It never moves the protagonist node as gameplay authority; the node is positioned from the latest core observation.

### `tools/SpaceAdventure.SimCli`

A console application references the core and runs rule-level scenarios without launching Godot. It provides `bootstrap` and `station-route` scenarios and emits JSON Lines containing metadata, command results, events, bounded advancement results, assertions, and a final snapshot. The station-route scenario loads the same versioned content as Godot but uses a deterministic fixture layout and pathfinder. Versioned external scenario inputs remain a later extension. The CLI is optimized for fast agent iteration, reproduction, and regression—not for validating the real navigation mesh, rendering, or input.

### `tests/SpaceAdventure.Core.Tests`

Fast tests construct small fixtures directly and do not load Godot. They cover the Phase 1 pause clock and bounded event retention plus Phase 2 content validation, layout-ID validation, fixed-tick movement, pause and pending-order replacement, unreachable-order atomicity, authored dialogue response, optional terminal behavior, objective gating, and completion with or without the optional terminal. Combat, inventory, recruitment, and generated-dialogue rules remain future test work.

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

The cached-AABB approach is deliberately a greybox POC. It is deterministic, cheap, and inspectable for the current static axis-aligned boxes, but it over-approximates non-box silhouettes and becomes invalid for rotated, animated, deforming, concave, streamed, or multi-floor production environments. Scaling the same mesh also compresses any future trim, texture, window, or doorway detail and provides a whole-panel collapse rather than a local view hole. Production levels should move to separately authored upper/base presentation, occlusion volumes and room/floor visibility metadata, with a dithered or otherwise sorting-safe visual transition, while retaining stable occluder IDs and structured observation.

## Command contract

Commands are explicit C# types under a common gameplay-command contract. Phase 2 currently implements:

- `SetPauseCommand`.
- `MoveActorCommand` with a stable actor ID and world destination.
- `InteractCommand` with stable actor and interaction IDs; it includes approach movement when needed.
- `ChooseDialogueResponseCommand` with stable actor, interaction, and authored response IDs.

Expected later POC commands include:

- Select or inspect entities through the appropriate adapter state.
- Assign an attack target.
- Use an ability with an entity or position target.
- Use a carried item.
- Restart the scenario.

Selection may remain player-control state in the Godot host rather than world simulation state, but the resulting orders always name stable actor IDs. Automation never depends on which portrait happens to be selected in a UI.

The dispatcher validates identity, scenario phase, control ownership, target shape, range, path availability, cooldown, resource cost, and other preconditions before applying a command. It returns an acknowledgement containing the command ID and accepted result or a stable rejection code with safe details. Rejections also enter the event stream.

The external JSON envelope contains a schema version, command ID, command type, and command-specific payload. Serialization DTOs translate into typed commands; raw dictionaries do not flow through gameplay code.

## Actions and active pause

Each party member has a current action and at most one pending primary action in the POC. While paused, a newly accepted primary order replaces that character's prior pending order. On resume, pending actions become eligible in stable order. There is no arbitrary action queue, timeline scripting, or programmable behavior system.

Phase 2 exercises this with movement and contextual interaction. A running order replaces the current primary action. A paused order replaces only the pending primary action, so the character remains stationary until resume; the newest accepted pending order wins. Development-only paused stepping executes the same fixed-tick rule path for deterministic scenarios and is capped at 3,000 ticks per call.

Basic attacks repeat against an explicitly assigned target until invalidated or replaced; active abilities remain explicit orders. Combat start triggers one automatic pause. All subsequent POC pausing is manual.

## Events and observations

Events are immutable facts with at least an event sequence, simulation tick, event type, and typed data. The runtime retains a bounded recent buffer for tools and presentation. Implemented route events include command acceptance/rejection, primary-action assignment/failure, movement arrival, dialogue start and response, interaction completion, objective change, and scenario completion. Recruitment, combat, healing, cooldown, victory, and defeat events remain later work.

The current observation contains clock, pause state, latest event sequence, scenario/content identity, route phase, protagonist identity/position/current and pending actions, objective, all relevant interactions and approach points, and the optional active authored dialogue. The external automation projection is JSON-safe, snake-case, and versioned at its command boundary.

Use observations and events to answer what happened. Use screenshots and live play to judge composition, readability, animation, art, and input feel.

## Content boundary

The POC uses small versioned text definitions for gameplay data and Godot scenes for spatial layout and presentation references. The implemented `game/content/station-route.json` defines the schema/revision, protagonist speed, objectives, interaction kinds/radii/effects, survivor line and fixed response, and terminal/airlock result text. `station_route.tscn` owns positions, approach markers, collision, and navigation. Both are validated into core records and joined by stable IDs before the scenario starts.

Do not add a database, spreadsheet import service, generic graph editor, or mod framework for the POC. If direct text authoring becomes the dominant bottleneck, add an authoring adapter while preserving the validated core contracts.

## Dialogue boundary

A dialogue provider receives a bounded request containing only relevant world facts, conversation state, allowed intents, and permitted moves. It returns a structured proposal. Deterministic validators check identity, revisions, knowledge, references, size limits, intents, and state-transition preconditions. Accepted effects become ordinary gameplay commands or authored state transitions.

The POC uses the scripted provider. Phase 2 implements only one authored survivor line and one authored response, selected through `ChooseDialogueResponseCommand`; choosing it advances the objective and unlocks the airlock. This is intentionally not a branching dialogue framework. An optional local development provider may later invoke the official Codex CLI with ChatGPT sign-in; manual-inbox and recorded providers support inspection and deterministic replay. These are experiment tools, not dependencies of the playable scenario. Model, reasoning effort, and Fast mode are independent per-request profile settings and never enter authoritative or saved gameplay state. See `DIALOGUE-AI.md`.

## Automation boundary

The Godot game exposes a stable C# runtime node named `AutomationBridge`. It returns complete route observations, retained events since a sequence with explicit gap metadata, and schema-v1 command acknowledgements. Its JSON command adapter supports `set_pause`, `move_actor`, `interact`, and `choose_dialogue_response`. Explicit helpers provide pause/resume, exact paused stepping, advancement until a named event with a maximum 3,000-tick budget, stable-ID-to-screen projection, a bounded known-target context-click input hook, and clean shutdown. A fresh process resets the current scenario; there is no mutation-oriented reset backdoor. The bridge does not expose arbitrary coordinates, property setters, or code evaluation.

The optional external `godot-ai-plugin` may call those methods and additionally provides scene inspection, real input injection, runtime inspection, and ad hoc viewport capture. Enabling it can temporarily add editor-plugin and autoload entries to `project.godot`; those local entries are not part of the base project. Automated tests and the shipped game do not require the plugin.

The built-in `wall-cutaway` visual-capture mode is a narrower deterministic diagnostic boundary. It reaches a stable gameplay event through normal commands, pauses gameplay, and drives live camera yaw through settled blocking, clear-view, and re-blocking states. It then returns to the original camera view, records the cutaway lifecycle and gameplay observations, captures a 1280×720 graphical frame, writes an atomic schema-v1 JSON/PNG pair, and exits. `scripts/dev.ps1 capture -Name wall-cutaway` independently verifies the lifecycle result, fixed identity, pass flag, dimensions, byte length, and PNG SHA-256. Capture mode does not expose arbitrary scene mutation, establish perceived transition quality, or participate in normal player flow.

## Diagnostics

Both CLI scenarios record game and runtime versions, content revision, scenario ID, seed, command results, events, assertions, duration, and final outcome. The station-route scenario additionally records its deterministic pathfinder and tick budgets. The real-Godot smoke prints navigation readiness, a structured final summary, and per-step diagnostics on failure. Failures should produce enough structured output to reproduce them without reading a screenshot.

Godot automation uses the console executable and isolated user-data directories so parallel runs do not share editor state or logs. Deterministic graphical captures use their own validated user-data scope beneath the ignored artifact root rather than sharing the existing automated or interactive directories.

## Deliberate non-architecture

The POC has no ECS, networking stack, event-sourcing framework, dependency-injection container, message broker, generic quest engine, procedural generator, save-migration framework, model-provider framework, or custom engine build. Add an abstraction only at a demonstrated replacement boundary.

## Parallel-change boundaries

Core command and event contracts, `project.godot`, shared scenes, imported-asset registries, and binary Blender sources are merge hotspots and have one owner at a time. Good parallel units are an isolated core rule, test fixture, UI scene, automation scenario, asset ID, or documentation decision. See `AGENT-AUTOMATION.md`.
