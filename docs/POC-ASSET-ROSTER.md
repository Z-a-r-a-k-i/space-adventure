# POC asset roster

Status: approved target inventory; live integration remains roadmap-scoped

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
| Operator | `character.crew.operator.v1` | Fixed-outfit protagonist with pistol sockets | 3 |
| Protector | `character.crew.protector.v1` | Fixed-outfit companion with shotgun sockets | 3 |
| Survivor | `character.npc.station_survivor.v1` | Fixed-outfit humanoid NPC | 3 |
| Vanguard carbine | `weapon.crew.vanguard_carbine.v1` | Separate two-handed carbine with primary/support grips and muzzle | 3 |
| Operator pistol | `weapon.crew.operator_pistol.v1` | Separate pistol with primary grip and muzzle | 3 |
| Protector shotgun | `weapon.crew.protector_shotgun.v1` | Separate shotgun with primary/support grips and muzzle | 3 |
| Security Enforcer | `character.enemy.security_enforcer.v1` | Fixed-body humanoid security android with reinforced-forearm body attack | 4 |
| Gun sentry | `machine.security.gun_sentry.v1` | Fixed or floating rigid sentry with aim pivot, recoil axis, and muzzle | 4 |
| Service terminal | `prop.station.service_terminal.v1` | Readable optional-interaction hero prop | 5 |
| Wall utility | `prop.station.wall_utility.v1` | Repeatable noninteractive wall dressing | 5 |
| Field aid | `item.healing.field_aid.v1` | Hand-scale item only if visibly handled | 4 |
| Station kit | `kit.station.structure.v1` | Exact-grid floors, cutaway walls, junctions, route strips, and lights | 5 |
| Evacuation airlock | `assembly.station.evacuation_airlock.v1` | Frame, opening leaves, status panel, and readable open state | 5 |

The station kit and airlock are dimensionally authored in Blender. Tripo may
only supply decorative forms that survive cleanup.

## Humanoid production and animation

Every humanoid follows the same sequence:

1. unrigged T-pose generation;
2. Tripo Smart Low-Poly v2, Quad, target 10,000;
3. human-approved Mixamo marker placement;
4. Mixamo neutral rig downloaded with skin;
5. Blender weight repair and sockets;
6. Mixamo library animations downloaded without skin; and
7. Blender cleanup plus exact GLB review in Godot.

Required party coverage:

- holstered idle and locomotion;
- draw and holster with attachment-transfer markers;
- armed idle, locomotion, raise/aim, fire/recoil, and recovery;
- dialogue idle, speaking gesture, and listening;
- terminal interaction and healing use;
- hit reaction and down.

Vanguard and Protector require two-hand support validation. Combat clips are
in-place. Ability-specific clips wait for accepted gameplay abilities.

The station survivor selects only dialogue and interaction coverage from the
shared humanoid contract. The Security Enforcer selects idle, locomotion,
close-combat stance, wind-up, reinforced-forearm strike, recovery, hit reaction,
and down. It carries no weapon and publishes
`socket.attack.contact.primary` at the reviewed striking surface.

## Static props and environment assemblies

The service terminal, wall utility, field aid, station kit, and evacuation
airlock use their own brief-specific budgets. They do not use T-pose,
Tripo Quad-10k retopology by default, Mixamo, skinning, or the humanoid motion
library.

Structural station modules and the airlock are dimensionally authored in
Blender. Tripo is optional for decorative props only. Static candidates advance
through mesh/material cleanup, scale and pivot normalization, exact GLB import,
contextual Godot review, and human visual approval. A door leaf, terminal
screen, or status light may be a gameplay-driven rigid node without requiring
a skeleton.

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

The approved 2D references remain active. The previous rigged Vanguard and its
generated/retargeted animations were removed. The retained neutral-pose Tripo
Smart Low-Poly v2 Quad mesh is recorded under
`art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260723-01/`.
It predates the T-pose contract and is comparison material, not an authorized
production Mixamo input. Produce and retopologize a conforming unrigged T-pose
source before the human Mixamo marker gate and Blender weight correction. No
grandfathered exception is approved, and no character GLB is published until
all reviews pass.

## Integration order

1. Finish and approve Vanguard plus carbine without replacing the greybox.
2. Prove one cleaned Mixamo locomotion clip and weapon-handling sequence in
   Godot.
3. Reuse the accepted humanoid contract for Operator, Protector, survivor, and
   Security Enforcer.
4. Define gameplay attacks and abilities before final combat timing or VFX.
5. Produce the gun sentry only as a simple rigid enemy machine.
6. Reuse retained validated environment candidates where they pass owner
   review; replace remaining environment greybox only where Phase 5 readability
   requires it.

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
