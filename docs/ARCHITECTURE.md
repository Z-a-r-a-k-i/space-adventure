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
commands remain available. Combat pauses when a hostile notices the crew and
readying begins, and defeat pauses for retry. Other pausing is manual. Development stepping requires pause
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
cannot erase. New offense waits for recovery; defensive abilities, including
Barrier, Taunt, Heal, and Healing Field, may begin during it.
See [GameSession.Orders.cs](../src/SpaceAdventure.Core/GameSession.Orders.cs).

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
completion remove it. Hostile rifle and sentry projectiles have
fixed-tick flight and a release-fixed destination; the core checks their
segments against the shield's fixed silhouette before applying arrival damage. Crew
bolts remain presentation only. Taunt temporarily overrides nearby enemies'
target choice, preserving released projectiles and recovery. Stationary sentry
range/sector limits still apply. Burst releases separately timed shots and
revalidates the target for each; cancellation retains spent cooldown/recovery.
Hostiles retain targets through wind-up; sentry releases also check its firing
sector. Ranged Enforcers approach rifle range, stop, telegraph, and fire without
automatic retreat, sharing projectile, Interrupt, and Taunt rules while leaving
the sentry's stationary firing limits intact.

Heal accepts a living crew target, including Medic herself, during combat.
Validate ownership, recipient, range, and cooldown before mutation and recheck
the recipient and range at release. Clamp healing to maximum health; it cannot
revive fallen crew. Healing Field accepts a ground position and periodically
heals living crew currently inside its radius. Its fixed world position and
lifetime are independent of its caster; a successful new deployment replaces
the one existing field. Pause freezes field time. Both abilities preserve
assigned basic-attack intent and spend cooldown only on release.

All hostiles down means victory; all recruited crew down means defeat. Secured
victory restores recruited crew health, fallen members, and cooldowns and clears
combat orders, barriers, fields, and projectiles before travel. Retry returns
the crew to the positions and headings captured when the fight started, the
enemies to their placements, and clears its temporary combat state.

## Content and spatial boundary

[station-route.json](../game/content/station-route.json) owns content revision,
stats/timings, loadouts, ordered encounters, individual crew health,
required crew IDs, authored dialogue, interactions, and objectives.
[station_route.tscn](../game/scenes/station_route.tscn) owns spatial placement,
navigation, collision, lights, and presentation. Its [offline layout input](station-layout.md)
also rebuilds the matching Blender decks and recessed foundations. Both validate and join through
stable IDs; node paths and object IDs never identify gameplay entities.
Loading also rejects routes that could not be completed: encounter objectives
must be `combat_objective`, `main_combat_objective`, then distinct IDs, and the
layout must place every encounter's hostiles.

`ISpatialPathfinder` accepts pure positions and returns validated waypoints.
Godot checks navigation readiness, finite endpoints, bounded paths, and reachability;
the core advances actors along those paths. Views interpolate observations.
Root motion, physics bodies, and navigation agents cannot become movement authority.
A point stopped inside a doorway lies on an enabled door link between two floors;
Godot routes paths from or to it through the link's nearer endpoint, so crew
spotted mid-passage can move and be reached. Pits and walls stay unreachable.

An available service door enables its navigation link. Core approach movement
completes the door interaction atomically; completion drives collision and leaf
presentation. The solo-exit gate derives from victory. Recruitment and later
gates derive from explicit content effects, without a generic quest framework.
Encounters start by sight ([encounter flow](#encounter-flow)). Completed
encounters cannot restart or be skipped. Retry resets only the active attempt,
preserving recruitment, dialogue consequences, inspection, and prior victories.

Final airlock access follows launch-bay victory. Cutter boarding uses validated
group approach movement. One interaction validates all three paths before
assigning any order, waits for every crew member inside the boarding zone,
revalidates their presence at completion, and completes the route once. Godot presents the boarding,
entrance closure, and departure before showing the completion summary; it does
not supply authoritative completion or implement ship simulation.

The ship battle is a separate pure `ShipCombatSession` with its own typed
commands, observations and content ([spec](future/ship-combat-poc.md)). A pure
`ShipContinuation` enters it exactly once after the departure presentation and
threaded load both finish; the Godot `ShipBattleHost` only presents
observations, samples crew poses from the battle tick and sends commands.
Its evasion and hazard-chance rolls come from one seeded stream per attempt
(content seed, overridable in tests), so a seed, attempt and command log replay
exactly; effects and audio variation use separate presentation-only randomness.

## Encounter flow

Hostiles stand at their placements from session start and never idle-walk. The
next encounter in authored order (its objective is current and its required crew
are recruited and alive) starts on the first tick one of its living hostiles has
clear sight of a living recruited crew member within `vision.hostile_detection_meters`,
which content keeps below crew sight so the crew usually see a room first. The
whole encounter alerts together; the game pauses where everyone stands. Crew keep
their positions and headings, their orders are cleared, and weapons draw during
readying after resume. The start records the spotter and the crew member it saw
(encounter observation and start event), and snapshots every recruited crew
position and heading for retry. Godot never repositions actors or snaps the camera:
it eases toward the contact only when the spotter or spotted crew is off-screen,
fades newly revealed hostiles in, and briefly marks the spotter's line of sight.

## Shared crew vision

Enemy perception belongs to the core. Any living recruited crew member supplies
360-degree sight; content currently sets `vision.range_meters` to 18 and
`vision.eye_height_meters` to 1.4.
Sight is shared across the crew, regardless of selection. Full wall geometry
and closed doors block sight. A door stops blocking only when its interaction
is completed, not merely available. Godot supplies immutable world-space wall
and door AABBs through the spatial layout; the core evaluates them without
camera, rendered cutaway, or physics-query authority. Godot rejects off-axis
walls and doors, whose enclosing boxes would over-block sight, and the core
rejects any spawn position whose eye point lies inside a blocker.

Sight may reveal hostiles from a dormant encounter before they notice the crew;
only a hostile's own sight within detection range starts a fight (see
[encounter flow](#encounter-flow)). Enemies hide again when no living recruited crew member
can see them. Enemy-target commands require shared sight; knowing a diagnostic
entity ID does not grant targeting permission. This is current sight without
stealth, facing cones, or persistent discovery memory.
Shared sight permits target assignment, but each shooter needs an unobstructed
line of fire: basic attacks approach a clear angle and Burst rejects an obstructed
cast. Sentries wait, and mobile melee and rifle Enforcers approach, when a wall
or closed door blocks the line; melee contact needs the same clear line.
Already released projectiles retain their existing flight and impact rules.

`VisibleHostiles` is the player-perception collection and supplies enemy models,
health bars, picking, and target previews. Each hostile identifies its own
encounter, phase, attempt, and facing so dormant or previously encountered
actors do not inherit the active encounter's presentation state. `Hostiles`,
`Encounter.HostileIds`, and sequenced events retain diagnostic encounter state;
they are not proof that the player can currently see an enemy. Automation's
external field names are documented in [Testing](testing.md#live-automation).

## Presentation and observation

Events are immutable sequenced facts with tick and typed details. Observations
expose state, current/pending actions, phase timing, recovery eligibility,
resources, healing and field state, and ordered encounter progression. Content
and automation use schema 11; bundled content, fixtures, and tools migrate
together. `UseAbilityCommand` retains its existing entity/position targets.
The retained event buffer reports gaps. Inspect
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
