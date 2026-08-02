# Escape-cutter ship-combat proof of concept

Status: approved direction for Phase 7; implementation remains gated behind
the station POC and its production-hardening exit.

## Purpose and boundary

This proof of concept tests whether SpaceAdventure's party-control identity,
fixed simulation tick, and active pause remain enjoyable when the party escapes
the station aboard a small ship. It is a separate post-POC experiment, not an
expansion of the current station POC.

The station POC still ends when the evacuation airlock opens and presents its
completion summary. Phases 3 through 6 and their exit gates remain unchanged.
Once those gates pass, the ship prototype may begin with a short authored
launch-bay transition, entering the escape cutter, and one fixed battle.
Entering the cutter is a fixed scenario handoff, not a reusable boarding
mechanic.

The approved composition reference is
[`ship-combat-separated-clean-direction-v4.png`](../art/concepts/station-escape-ship-combat-v1/ship-combat-separated-clean-direction-v4.png).
It establishes a strict overhead view with the player ship on the left, the
enemy ship on the right, both bows pointing upward, and a clear central
tactical divider. The ships are not physically side by side. Cyan and red
movement or trajectory lines are not part of the direction.

The concept is a visual anchor only. It does not approve final geometry, room
topology, UI, balance, 3D production, or live integration.

## Player journey

1. Begin in the escape cutter with exactly two party members and one fixed
   hostile ship already detected.
2. Inspect both ships, crew locations, hull state, system state, power
   allocation, weapon target, and current threats.
3. Enter combat in tactical pause.
4. Allocate a fixed reactor budget among weapons, engines, and shields.
5. Move either party member between authored rooms to operate or repair a
   system.
6. Select one enemy system as the cutter's weapon target.
7. Resume the fixed simulation tick and observe weapon charge, incoming fire,
   shields, system damage, repairs, and hull damage.
8. Pause and revise orders until the enemy is disabled, the cutter is
   destroyed, or the encounter is restarted.

The encounter is deliberately short and should support a complete
pause-plan-resume-replan loop in roughly three to five minutes.

## Content budget

- One authored player escape cutter.
- One authored hostile interceptor.
- Exactly two controllable party members.
- One deterministic enemy controller; enemy crew are not simulated.
- Weapons, engines, and shields as the only authoritative ship systems.
- A maximum of six readable rooms per ship.
- One fixed weapon per ship.
- One encounter, one victory state, one defeat state, and restart.
- One readable visual damage state per system plus hull damage feedback.

The humanoid station-boarder concept is not part of this battle. Adding a
boarding encounter or simulated enemy crew requires a later decision.

## Authoritative state

The pure C# gameplay core owns:

- stable ship, crew, room, system, and weapon identifiers;
- the fixed reactor budget and validated system-power allocation;
- crew room occupancy and movement progress;
- hull, shield, system-damage, repair, and weapon-cooldown state;
- the selected weapon target;
- deterministic enemy decisions;
- battle pause, tick, victory, defeat, and restart state; and
- structured command rejections, gameplay events, and snapshots.

The separated ship views are a presentation abstraction. They do not imply
world-space proximity, free-flight navigation, collision, formation movement,
or ship physics.

## Command and simulation rules

Human input, automation, CLI scenarios, and future replay support dispatch the
same typed commands. The initial command surface is:

- the existing `SetPauseCommand`;
- `MoveCrewToShipRoomCommand`;
- `SetShipSystemPowerCommand`;
- `SetShipWeaponTargetCommand`; and
- `RestartShipCombatCommand`.

Every command is completely validated before mutation. Invalid crew, room,
power, target, timing, or battle-state requests produce structured rejections
and no partial effects.

The prototype uses the existing explicit fixed tick. Combat automatically
pauses once when the encounter begins; all later pause changes are manual.
Pausing stops gameplay advancement while input, camera control, observation,
command entry, and UI remain available.

Initial combat resolution is deterministic and contains no random variance:

- total assigned power cannot exceed the cutter's fixed reactor budget;
- an unpowered or disabled system cannot provide its effect;
- weapons require power, a valid target, and a completed cooldown before
  firing;
- powered engines reduce the deterministic rate of incoming weapon pressure;
- shields absorb incoming damage before hull and recover only while powered;
- system damage reduces or disables the owning system;
- a party member in the affected room can repair its system over fixed ticks;
  and
- cutter hull depletion causes defeat, while hostile hull depletion causes
  victory.

Exact integer values and room adjacency belong in the authored encounter
definition and tests, not in presentation scenes.

## Presentation requirements

- Strict overhead combat view.
- Player ship on the left and hostile ship on the right.
- Both ships point upward.
- A visually quiet central divider communicates separation and distance.
- No movement, trajectory, or decorative targeting lines between the ships.
- Exactly two player crew figures remain individually readable.
- Rooms, occupied locations, powered systems, weapon target, shields, damage,
  repairs, pause state, victory, and defeat are understandable without external
  explanation.
- Presentation reads authoritative snapshots and events and never mutates ship
  state directly.

## Explicit non-goals

No sector map, travel simulation, free-flight controls, ship physics,
procedural encounters, campaign integration, saving, progression, upgrades,
loot, economy, multiple weapons, ammunition, missiles, drones, oxygen, fire,
hull breaches, hostile-ship or generalized boarding mechanics, enemy-crew
simulation, crew recruitment, generalized inventory, metagame, runtime model
call, or multiplayer belongs in this proof of concept.

## Verification and exit gate

1. Pure .NET tests cover power, movement, targeting, cooldown, shields,
   damage, repair, pause, victory, defeat, restart, and atomic rejection.
2. A deterministic CLI scenario completes the real encounter through typed
   commands, fixed ticks, events, and snapshots.
3. Godot headless smoke proves scene, adapter, command, and observation
   integration.
4. A graphical playtest and direct inspection in Godot prove the approved
   separated composition and the readability requirements above.
5. The owner completes the encounter using physical input and confirms that
   pausing to reassign crew, power, and target is clear and tactically useful.

The slice passes only when all five layers pass. Final ship models and
production UI remain blocked until the greybox proves the room layout and
combat information hierarchy.
