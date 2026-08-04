# POC asset roster

Status: approved active inventory. Vanguard and the initial station candidates
are integrated; the service door, full-route presentation, active NPC bases,
and hostile bases are the remaining Phase 3 gate before combat gameplay.

## Presentation rules

- Vanguard, Operator, Protector, the station survivor, and the Security
  Enforcer are humanoids using the shared Mixamo/Blender contract in
  `art/rigs/crew-humanoid-v1.md`.
- Each humanoid has one fixed complete outfit. Armor source objects are not
  gameplay equipment slots.
- Party firearms are separate assets and remain holstered during exploration.
- Skeletal animation is limited to humanoids.
- Non-humanoid enemies are simple floating or stationary rigid machines with
  only a few aim, recoil, hover, impact, or shutdown pivots.
- Quadrupeds, walkers, creatures, and complex deforming machines are outside
  the POC.
- Abilities, attack timing, damage, and VFX remain gameplay-owned.

## Production assets

| Group | Asset ID | Required result | Phase |
|---|---|---|---|
| Vanguard | `character.crew.vanguard.v1` | Fixed-outfit protagonist with carbine hand and holster sockets | 3 |
| Operator | `character.crew.operator.v1` | Deferred fixed-outfit protagonist candidate with pistol sockets | Deferred |
| Protector | `character.crew.protector.v1` | Fixed-outfit post-solo-combat recruit with shotgun sockets | 3 |
| Survivor | `character.npc.station_survivor.v1` | Fixed-outfit humanoid NPC | 3 |
| Vanguard carbine | `weapon.crew.vanguard_carbine.v1` | Separate two-handed carbine with primary/support grips and muzzle | 3 base / 4 motion |
| Operator pistol | `weapon.crew.operator_pistol.v1` | Deferred pistol with primary grip and muzzle | Deferred |
| Protector shotgun | `weapon.crew.protector_shotgun.v1` | Separate shotgun with primary/support grips and muzzle | 3 base / 4 motion |
| Security Enforcer | `character.enemy.security_enforcer.v1` | Fixed-body humanoid security android with reinforced-forearm body attack | 3 base / 4 motion |
| Gun sentry | `machine.security.gun_sentry.v1` | Fixed or floating rigid sentry with aim pivot, recoil axis, and muzzle | 3 base / 4 motion |
| Service terminal | `prop.station.service_terminal.v1` | Readable optional-interaction hero prop | 3 |
| Wall utility | `prop.station.wall_utility.v1` | Repeatable noninteractive wall dressing | 5 |
| Field aid | `item.healing.field_aid.v1` | Hand-scale item only if visibly handled | 4 |
| Station kit | `kit.station.structure.v1` | Exact-grid floors, cutaway walls, junctions, route strips, and lights | 3 |
| Evacuation airlock | `assembly.station.evacuation_airlock.v1` | Frame, opening leaves, status panel, and readable open state | 3 |
| Service door | `assembly.station.service_door.v1` | Ordinary rigid room-boundary door derived dimensionally from the station kit | 3 |

The station kit, service door, and airlock are dimensionally authored in
Blender. Tripo may only supply decorative forms that survive cleanup.

## Humanoid production and animation

Every humanoid follows the same sequence:

1. one approved front-view T-pose seed and one direct single-image, unrigged
   Tripo HD source using v3.1 Best Quality, Ultra, Triangle 2M, and 4K PBR;
2. Tripo Smart Low-Poly v2, Quad, target 10,000;
3. human-approved Mixamo markers with symmetry and Standard Skeleton (65);
4. Mixamo neutral rig downloaded with skin after human approval of the motion
   preview;
5. Blender weight repair and sockets;
6. Mixamo library animations downloaded without skin, using `Standard Walk`
   in-place as the default exploration walk; and
7. Blender cleanup plus exact GLB review in Godot.

Required active-party coverage:

- holstered idle and locomotion;
- draw and holster with attachment-transfer markers;
- armed idle, locomotion, raise/aim, fire/recoil, and recovery;
- dialogue idle, speaking gesture, and listening;
- terminal interaction and healing use;
- hit reaction and down.

Vanguard and Protector require two-hand support validation. Operator requires
one-hand pistol-grip validation only if its deferred scope is reactivated.
Combat clips are in-place and are selected and finalized alongside Phase 4
action timing. Ability-specific clips wait for accepted gameplay abilities.

The station survivor selects only dialogue and interaction coverage from the
shared humanoid contract. The Security Enforcer selects idle, locomotion,
close-combat stance, wind-up, reinforced-forearm strike, recovery, hit reaction,
and down. It carries no weapon and publishes
`socket.attack.contact.primary` at the reviewed striking surface.

## Static props and environment assemblies

The service terminal, wall utility, field aid, station kit, service door, and
evacuation airlock use their own brief-specific budgets. They do not use T-pose,
Tripo Quad-10k retopology by default, Mixamo, skinning, or the humanoid motion
library.

Structural station modules, the service door, and the airlock are dimensionally
authored in Blender. Tripo is optional for decorative props only. Static
candidates advance through mesh/material cleanup, scale and pivot
normalization, exact GLB import, contextual Godot review, and human visual
approval. A door leaf, terminal screen, or status light may be a gameplay-driven
rigid node without requiring a skeleton.

The project owner advanced the station structure, service terminal, and
evacuation airlock to Phase 3 on 2026-08-02. Their deterministic Blender
sources and exact GLBs are integrated into the live route over the retained
Godot collision, navigation, light, and interaction wrappers. All three pass
technical and live graphical review; final owner visual approval remains
pending. Their compact records are under `art/source/<asset-id>/production.md`.

The retained wall-utility candidate passed its brief-specific Blender and
isolated Godot technical checks. Its compact record and exact reviewed output
are under
`art/generated/prop.station.wall_utility.v1/prod-tripo-v31bq-20260724-01/`.
Owner visual approval and Phase 5 live integration remain pending; no new
generation is required merely because the humanoid pipeline changed.

## Machine motion

The sentry uses idle scan, one bounded aim pivot, one recoil axis, hit response,
and shutdown. It uses no walk cycle or skeletal deformation. The Security
Enforcer is a humanoid and therefore uses the shared Mixamo/Blender pipeline,
not the rigid-machine path.

## Current Vanguard status

The active run is
`art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260803-02/`:
direct single-image 4K unrigged T-pose, Smart Low-Poly v2 Quad target 10k,
geometry-only Mixamo upload, symmetry, Standard Skeleton (65), and
human-approved markers and Auto-Rigger preview. Blender publishes the accepted
`Unarmed Idle` and in-place `Standard Walk`, limits skinning to four
influences, preserves the animated Mixamo armature transform, and publishes
both weapon sockets. Vanguard's with-skin walk donor is a documented exception
because the tested no-skin baseline export did not preserve the accepted rest
pose.

The untouched walk FBX and exact exported GLB both passed sustained direct
Godot locomotion review: grounded motion, alternating feet, correct facing,
stable arrival, and tactical-pause freezing. Vanguard now replaces the
protagonist greybox and moves at 2.0 m/s. Protector is the fixed post-fight
recruit and requires its approved production model, rig, idle, and locomotion
before Phase 4 begins. The carbine, draw, aim, fire, recovery, and holster
sequence remains coupled to the Phase 4 weapon-handling and combat gate.

## Integration order

1. Obtain final owner visual approval for the integrated station structure,
   service terminal, and final evacuation-airlock candidates.
2. Author and integrate the ordinary service door at the first-room boundary;
   reserve the airlock for the final destination.
3. Extend reviewed station presentation across the full authored route,
   retaining only invisible primitive gameplay wrappers.
4. Reuse the accepted humanoid contract for the survivor, Protector, and
   Security Enforcer. Produce the sentry as a simple rigid machine. All visible
   NPC and combatant bases must be non-greybox before Phase 4 starts.
5. Finish the separate Vanguard carbine and Protector shotgun attachment fits.
6. Define the solo tutorial and main party-combat rules, then select and clean
   weapon-handling and fight animations against those authoritative timings.
7. Keep Operator and its pistol deferred unless a later owner decision
   reactivates them. Reuse the retained wall utility only after owner review.

## Post-POC visual anchors

The 2026-07-29 ship-combat concept pack remains outside this roster and does
not authorize 3D production or integration.

| Exploratory ID | Retained direction |
|---|---|
| `environment.station.escape_launch_bay.v1` | Possible station-to-cutter transition |
| `kit.station.dock_service.v1` | Exploratory dock prop family |
| `character.enemy.station_boarder.v1` | Exploratory humanoid enemy; excluded from the Phase 7 battle |
| `vehicle.ship.escape_cutter.v1` | Exterior and interior direction to test against a greybox |
| `vehicle.ship.hostile_interceptor.v1` | Opposing-ship silhouette only |
| `presentation.ship_combat.separated.v1` | Approved strict-overhead separated composition |

References and provenance are in
`art/concepts/station-escape-ship-combat-v1/README.md`. Production briefs,
geometry, room layouts, and UI remain blocked until the Phase 7 greybox proves
what is needed.
