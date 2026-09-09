# Architecture

## Ownership

```text
Human input / automation / CLI scenarios
                ↓ typed commands
       SpaceAdventure.Core
                ↓ observations + events
        Godot presentation
```

| Location | Owns |
| --- | --- |
| `src/SpaceAdventure.Core/` | Pure `net8.0` rules, commands, stable IDs, validated content, fixed clock, actions, combat, dialogue, progression, observations/events |
| `game/` | Godot scenes, navigation/collision, input/JSON adapters, content loading, camera/HUD, models, animation/effects/audio |
| `tools/SpaceAdventure.SimCli/` | Deterministic rule scenarios with a fixture pathfinder and JSON Lines output |
| `tests/SpaceAdventure.Core.Tests/` | Fast fixtures and rule regression tests; no Godot |

The game, CLI, and tests reference the core. The core references none of them
and contains no Godot, scene, input, rendering, or serialization types. Use
small immutable boundary values, nullable types, and .NET analyzers. Introduce
frameworks or dependencies only for an observed current need.

## Commands, actions, and time

[Commands.cs](../src/SpaceAdventure.Core/Commands.cs) defines the typed command
catalogue. UI and automation submit to the same dispatcher. JSON is a versioned
adapter format. Validate identity, ownership, phase, target, path, cooldown,
and resource preconditions before mutation; rejection is structured and atomic.
Pending actions are validated again before execution. Costs and cooldowns
apply at release.

`GameSession` advances at 30 Hz in stable entity order; any randomness comes
from its explicit seed. Tactical pause stops gameplay, including movement,
AI, action phases, and cooldowns, while camera, UI, selection, observation, and
commands remain available. Combat pauses when readying begins, and defeat
pauses for retry. Other pausing is manual. Development stepping requires pause
and is bounded to 3,000 ticks; it executes the same rules as ordinary play.

Each actor has one current action and at most one replaceable pending primary
action. Waiting reasons are `TacticalPause`, `EncounterReadying`, and
`OffensiveRecovery`. Paused orders replace the pending slot without advancing
the current action. Stop follows those same waiting rules and clears explicit
attack intent. There is no arbitrary action queue.

Basic attacks remember an explicitly assigned target. Same-target orders
preserve the cycle; abilities preserve the target for automatic
resumption. Movement, interaction, Stop, target defeat, encounter completion,
and retry clear that intent. A running move can cancel an unreleased attack;
released offense owns an independent recovery deadline that replacements
cannot erase. New offense waits for recovery; Barrier and Taunt may begin
during it. See [GameSession.Orders.cs](../src/SpaceAdventure.Core/GameSession.Orders.cs).

Idle crew wait for an explicit attack order, including after their target falls.
Actor heading follows movement and combat targets at a bounded fixed-tick rate.
Godot interpolates observed body facing;
upper-body aim follows the assigned target.

Selection and ability focus belong to the Godot adapter. Drag selection collects
living crew; Tab cycles ability focus without changing a selected group. Group
orders and individual abilities still use the same validated core commands.
Party members own independent health, skill cooldowns, and orders.
Barrier validates a ground position and facing, then keeps that world pose
independently of Protector's movement, heading, or defeat. Expiry and encounter
completion remove it. Sentry projectiles have
fixed-tick flight and a release-fixed destination; the core checks their
segments against the shield's fixed silhouette before applying arrival damage. Crew
bolts remain presentation only. Taunt temporarily overrides nearby enemies'
target choice, preserving released projectiles and recovery. Stationary sentry
range/sector limits still apply. Burst releases separately timed shots and
revalidates the target for each; cancellation retains spent cooldown/recovery.
Hostiles retain targets through wind-up; sentry releases also check its firing
sector. All hostiles down means victory; all crew down means defeat. Retry
restores both sides and clears barriers and projectiles.

## Content and spatial boundary

[station-route.json](../game/content/station-route.json) owns content revision,
stats/timings, loadouts, authored dialogue, interactions, and objectives.
[station_route.tscn](../game/scenes/station_route.tscn) owns spatial placement,
navigation, collision, lights, and presentation. Both validate and join through
stable IDs; node paths and object IDs never identify gameplay entities.

`ISpatialPathfinder` accepts pure positions and returns validated waypoints.
Godot checks navigation readiness, finite endpoints, bounded paths, and reachability;
the core advances actors along those paths. Views interpolate observations.
Root motion, physics bodies, and navigation agents cannot become movement authority.

An available service door enables its navigation link. Core approach movement
completes the door interaction atomically; completion drives collision and leaf
presentation. The solo-exit gate derives from victory. Recruitment and later
gates derive from explicit content effects, without a generic quest framework.
Retry resets the combat attempt while preserving completed route progression.

## Presentation and observation

Events are immutable sequenced facts with tick and typed details. Observations
expose state, current/pending actions, phase timing, recovery eligibility,
resources, and progression. The retained event buffer reports gaps. Inspect
those facts to determine what happened; use graphical play to judge readability.

Animation, movement interpolation, and combat effects share a simulation
tick/fraction clock. Pause freezes it; exact stepping samples it deterministically.
Camera occlusion stays presentational: static wall AABBs drive opaque base
cutaway for the selected focus and visible living combatants; separate lintel
bounds drive fading with hysteresis. Neither changes
navigation, collision, or rules. Rotated or complex future geometry needs a
revisited occlusion approach.

Godot reconstructs attachments and samples authored poses before procedural
corrections. Sockets, animation callbacks, projectiles, effects, and audio
never resolve damage. The detailed asset interface and visual event timing
have one home: [ATTACK-PRESENTATION.md](ATTACK-PRESENTATION.md).

Authored dialogue uses the same validated command boundary. Player/model text
cannot grant authority or establish facts; future providers remain adapters.
Automation methods and verification live in [testing.md](testing.md).
