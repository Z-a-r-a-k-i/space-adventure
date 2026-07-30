# POC asset roster and animation plan

Status: approved POC target inventory; offline source production is
brief-scoped and gameplay integration remains phase-scoped

Revision: 2026-07-24

## Authority and milestone status

This document is the inventory and dependency plan for the complete first
playable. `POC.md` remains authoritative for gameplay acceptance,
`ATTACK-PRESENTATION.md` for combatant presentation interfaces, `ROADMAP.md`
for when work may begin, `ART-PIPELINE.md` for publication and review, and
`TRIPO-PRODUCTION-HANDOFF.md` for the dedicated generation agent's operational
Studio, Blender, rigging, and animation sequence.

Approval of this roster authorizes reserved asset IDs, production briefs,
reference sheets, estimates, and dependency planning. It does not by itself
authorize execution. An approved roster asset may enter offline source
production under ADR 0016 when it also has an approved visual reference, an
accepted production-ready art brief, assigned ownership, and resolved licensing
and privacy prerequisites. “Accepted” requires a recorded status, date,
approver, asset ID, authorized offline scope, phase-blocked fields, and
dedicated worktree. Only the project owner or an explicitly delegated art owner
may grant it; the brief author or production agent cannot infer approval.

The Phase 2 human-playthrough gate passed on 2026-07-24. The separate,
non-gating provider comparison remains limited to the three entries in the
bounded generator bake-off:

- `prop.station.service_terminal.v1`;
- `prop.station.wall_utility.v1`; and
- `machine.security_drone.body.v1`.

The third entry is an isolated, disposable body candidate. It is not the
production ram drone reserved below. Work on other approved roster items is
recorded as offline production rather than added to the experiment. Before the
owning gameplay phase, that lane may spend generation credits, model, complete
parts and topology, texture, create provider rig or animation donors, finish
Blender sources, publish staged review GLBs, and use the isolated Godot asset
gallery. It must not replace live greybox content, wire gameplay scenes,
invent unresolved mechanics, or finalize ability-specific and attack-timing
presentation.

## Approved presentation decisions

1. Vanguard, Operator, Protector, and the station survivor are four distinct
   human presentations. They share one normalized humanoid skeleton. A
   playthrough still controls exactly two characters: the selected Vanguard or
   Operator protagonist plus the Protector companion. The survivor is an NPC.
2. Vanguard carries a separate two-handed carbine, Operator a separate compact
   pistol, and Protector a separate two-handed shotgun.
3. The hostile group uses at most two production archetypes: a compact
   body-source ram drone and a taller integrated-firearm sentry.
4. Party weapons are holstered during exploration and transferred
   deterministically to hand attachments when combat presentation begins.
5. The final evacuation airlock visibly opens before completion is presented.
6. The three active abilities remain POC requirements, but their exact
   mechanics are intentionally deferred. Do not invent ability-specific props,
   clips, icons, or effects before those definitions are accepted.
7. Each human appears in one fixed complete outfit in the POC. Blender retains
   a fitted undersuit and major armor pieces as named editable objects on the
   shared skeleton, but Godot does not expose them as equipment slots.

The shield depicted in the crew concept is not assigned to a character,
weapon, or active ability by this roster.

## Deferred Vanguard production status

Status recorded 2026-07-24: postpone the current Vanguard 3D implementation
until the rest of the Phase 3 functionality has been exercised.

- The approved 2D Vanguard and carbine reference sheets remain valid visual
  anchors. This decision does not reject the character concept or remove the
  Vanguard kit from the POC.
- The current generated Vanguard character and carbine candidates remain
  revise-required evidence, not accepted production assets. Their complete
  character-plus-weapon assembly, rig, animations, and live replacement are
  not approved.
- Keep the authored Vanguard greybox in the live game while protagonist
  selection, recruitment, conversation, formation movement, and both kit paths
  are implemented and playtested.
- A later production pass must decide whether to repair or replace the current
  character source, then finish topology and outfit fitting, shared-skeleton
  skinning, hand and holster fit, carbine grip/support/muzzle markers,
  exploration and weapon-handling animation, tactical-camera review, and final
  human approval.
- Only that later approval authorizes replacing the live Vanguard greybox.
  Postponement is not implicit approval and does not authorize ability-specific
  art or final attack-timing integration.

## Required production asset groups

The roster contains fourteen production brief groups. Modular station and
airlock groups may publish several GLBs, so the final runtime-file count will be
higher than fourteen. The healing-item group may instead conclude that its
required presentation is 2D and VFX only.

| Group | Reserved asset ID | Required result | Candidate and final-authoring path | Owning phase |
|---|---|---|---|---|
| Vanguard | `character.crew.vanguard.v1` | Distinct selectable protagonist with one fixed outfit plus carbine-compatible hand and holster attachments | Tripo may provide a part-aware body/outfit candidate; Blender owns the modular editable source, normalized topology, shared rig, skinning, sockets, materials, and final fixed-outfit GLB | Phase 3 |
| Operator | `character.crew.operator.v1` | Distinct selectable protagonist with one fixed outfit plus pistol-compatible hand and holster attachments | Tripo candidate, then the same Blender modular-source normalization and shared rig | Phase 3 |
| Protector | `character.crew.protector.v1` | Distinct fixed recruitable companion with one fixed outfit plus shotgun-compatible hand and holster attachments | Tripo candidate, then the same Blender modular-source normalization and shared rig | Phase 3 |
| Station survivor | `character.npc.station_survivor.v1` | Distinct noncombatant NPC with one fixed outfit on the shared humanoid skeleton | Tripo candidate or an authored body/outfit variant, finalized as a modular Blender source and fixed runtime outfit | Phase 3 |
| Vanguard carbine | `weapon.crew.vanguard_carbine.v1` | Separate broad two-handed carbine with primary grip, support grip, and muzzle markers | Tripo candidate, then Blender hard-surface cleanup and marker placement | Phase 3 |
| Operator pistol | `weapon.crew.operator_pistol.v1` | Separate compact pistol with primary grip and muzzle markers | Tripo candidate, then Blender hard-surface cleanup and marker placement | Phase 3 |
| Protector shotgun | `weapon.crew.protector_shotgun.v1` | Separate broad two-handed shotgun with primary grip, support grip, and muzzle markers | Tripo candidate, then Blender hard-surface cleanup and marker placement | Phase 3 |
| Security ram drone | `machine.security.ram_drone.v1` | Complete compact machine with reinforced forward contact mass and enough articulation for brace, strike, and rebound | Tripo may provide a complete-form candidate; Blender owns production topology, rig, contact marker, and GLB | Phase 4 |
| Security gun sentry | `machine.security.gun_sentry.v1` | Complete tall sentry with an integrated gun, visible muzzle, aim clearance, and recoil articulation; Phase 4 decides whether it moves | Tripo may provide a complete-form candidate; Blender owns production topology, rig, muzzle marker, and GLB | Phase 4 |
| Service terminal | `prop.station.service_terminal.v1` | Optional-interaction hero prop preserving the existing stable gameplay wrapper | Current bake-off may select a Tripo, Meshy, Blender, or no-provider result; any production version still passes the full pipeline | Bounded bake-off, then Phase 5 |
| Wall utility | `prop.station.wall_utility.v1` | Repeatable noninteractive wall dressing | Current bake-off may select a candidate; Blender owns any accepted source and final GLB | Bounded bake-off, then Phase 5 |
| Healing item | `item.healing.field_aid.v1` | Readable hand-scale injector or field medkit if the item is visibly handled | Tripo candidate followed by Blender cleanup; omit the 3D prop if playtesting proves that icon and VFX presentation is sufficient | Phase 4 |
| Station structure kit | `kit.station.structure.v1` | Exact-grid floor, split cutaway walls, junction/end cap, route strip, and light-fixture modules | Dimensionally authored in Blender; structural geometry is not generated in Tripo | Phase 5 |
| Evacuation airlock | `assembly.station.evacuation_airlock.v1` | Frame, moving leaves, control/status panel, attachment markers, and a readable open state | Frame and motion are dimensionally authored in Blender; Tripo may only provide a decorative insert that survives cleanup | Phase 5 |

The station structure kit is expected to publish distinct reusable pieces for:

- the 1 m floor grid;
- a 0.45 m retained lower wall;
- a separate upper/cutaway wall;
- a junction post or end cap;
- a route/emissive-strip insert; and
- a wall or ceiling light fixture.

The airlock assembly may publish its frame, moving leaf or leaves, and
control/status panel separately. Collision, navigation, occlusion metadata, and
interactive wrappers are authored in Godot rather than accepted from a
generator.

## Humanoid source and runtime outfit policy

The four human Blender sources retain a fitted technical undersuit and major
armor, footwear, glove, and accessory pieces as separate named objects where
practical. They are fitted and weighted to one normalized skeleton. Hidden
undersuit or body polygons may be masked or omitted beneath rigid armor to
avoid clipping; no anatomically complete body under the outfit is required.

The POC publishes and reviews one fixed complete outfit per human. A GLB may
contain several named skinned meshes, but its reviewed combination is one
runtime presentation, not an inventory of equippable items. Weapons remain
separate because draw, holster, grip, and attack presentation require it.

For a generated candidate, part-aware generation or segmentation is followed
by part completion, retopology or remeshing, cleanup, and fitting before the
shared rig is finalized. These mesh operations invalidate generated skeletons
and weights. Blender owns the final object boundaries, topology, rig, weights,
attachments, and export. A future phase may first add whole-outfit variants;
individual armor slots require a separate gameplay and compatibility design.

## Humanoid rig and animation matrix

The four humans use one normalized skeleton and scale band. Do not independently
accept four generated skeletons and then duplicate the animation library.
Generated rigs and clips are reference inputs until the shared Blender rig and
complete Godot assemblies pass review.

| Coverage | Vanguard | Operator | Protector | Survivor |
|---|:---:|:---:|:---:|:---:|
| Holstered idle | Required | Required | Required | — |
| Holstered locomotion | Required | Required | Required | Only if staging moves the NPC |
| Draw with attachment transfer | Required | Required | Required | — |
| Armed idle | Required | Required | Required | — |
| Armed locomotion | Required | Required | Required | — |
| Raise / wind-up / aim | Required | Required | Required | — |
| Fire / recoil | Required | Required | Required | — |
| Recovery | Required | Required | Required | — |
| Holster with attachment transfer | Required | Required | Required | — |
| Two-hand support validation | Required | — | Required | — |
| Dialogue idle | Required | Required | Required | Required |
| Reusable speaking gesture | Required | Required | Required | Required |
| Listening pose | Required | Required | Required | Required |
| Terminal interaction | Shared party clip | Shared party clip | Shared party clip | — |
| Healing-item use | Shared party clip | Shared party clip | Shared party clip | — |
| Hit reaction | Shared party clip | Shared party clip | Shared party clip | — |
| Incapacitated / down | Shared party clip | Shared party clip | Shared party clip | — |

Combat clips are in-place. Gameplay owns world movement, attack timing, contact,
damage, and interruption. Turning, strafing, and backpedal coverage are added
only if the Phase 4 movement presentation proves that procedural facing and the
base locomotion set are insufficient.

One active-ability clip per party profile is reserved but blocked until the
corresponding gameplay ability, target shape, source, and timing are accepted.
Deferring these clips does not remove the final POC ability requirement.

## Machine rig and animation matrix

The security ram drone requires:

- idle or scan;
- locomotion and turn coverage;
- alert or target acquisition;
- brace/compression wind-up;
- authoritative-movement-following charge and decisive contact pose;
- rebound and recovery;
- hit reaction; and
- disabled or shutdown.

The security gun sentry requires:

- idle or scan;
- locomotion and turn coverage when mobile;
- aim and track;
- weapon charge or wind-up;
- fire and recoil;
- recovery;
- hit reaction; and
- disabled or shutdown.

Neither machine clip may move authoritative gameplay through root motion.
Muzzle flashes, tracers, projectiles, telegraphs, impact effects, collision, and
damage volumes are never baked into the machine GLB.

## Environment animation

- The evacuation airlock opening is required and must read from the default
  tactical camera before completion presentation.
- Airlock closing is optional unless the final sequence visibly requires it.
- Terminal violet state, airlock green state, route strips, and warning lights
  use Godot-authored material or light animation.
- Wall cutaway remains Godot-controlled presentation over separately authored
  lower and upper structural pieces.

## Godot-authored 2D and effects

The POC also requires presentation that is not generated as 3D geometry:

- Vanguard, Operator, and Protector portraits, preferably rendered from the
  approved models; add a survivor portrait only if the accepted dialogue UI
  uses NPC portraits;
- three weapon icons, one healing-item icon, and health, cooldown, and
  pending-action UI;
- three ability icons only after the abilities are defined;
- selection rings, movement path, destination marker, target feedback, and
  interaction highlight;
- enemy-intent and attack telegraphs;
- muzzle flash, tracer or cosmetic projectile, weapon impact, body-contact,
  hit, healing, and eventual ability effects; and
- terminal, route, and airlock state feedback.

Godot owns these effects and their synchronization to observed gameplay state.
They are not baked into published character, weapon, machine, or environment
GLBs.

## Deferred and conditional assets

The following are not part of the required fourteen-brief package:

- a maintenance-berth prop or locker;
- a storage crate;
- a repeatable low barrier;
- a humanoid enemy;
- alternate weapons or cosmetic machine variants;
- ammunition, magazines, reloads, weapon swapping, loot, or generalized
  inventory presentation;
- a shield prop or shield ability;
- facial lip-sync, ragdolls, root-motion authority, damage variants, bespoke
  victory clips, vehicles, ships, or exterior-station art.

The first three may receive briefs only when the final traversal or encounter
layout demonstrates a readability need. They do not imply a loot, container, or
cover system.

## Post-POC exploratory visual anchors

These assets remain outside the approved fourteen-brief station POC roster.
The project owner approved the Phase 7 ship-combat composition direction on
2026-07-29, but did not authorize offline 3D production, production briefs,
live integration, or replacement of any current greybox.

| Exploratory ID | Concept | Current decision |
|---|---|---|
| `environment.station.escape_launch_bay.v1` | [`station-escape-launch-bay-key-art-v1.png`](../art/concepts/station-escape-ship-combat-v1/station-escape-launch-bay-key-art-v1.png) | Possible station-to-cutter transition; layout and production remain gated |
| `kit.station.dock_service.v1` | [`station-dock-service-prop-family-v1.png`](../art/concepts/station-escape-ship-combat-v1/station-dock-service-prop-family-v1.png) | Exploratory prop family only; not six approved production briefs |
| `character.enemy.station_boarder.v1` | [`station-boarder-humanoid-turnaround-v1.png`](../art/concepts/station-escape-ship-combat-v1/station-boarder-humanoid-turnaround-v1.png) | Exploratory humanoid enemy only; excluded from Phase 7 ship combat |
| `vehicle.ship.escape_cutter.v1` | [`escape-cutter-exterior-turnaround-v1.png`](../art/concepts/station-escape-ship-combat-v1/escape-cutter-exterior-turnaround-v1.png) and [`escape-cutter-interior-cutaway-v1.png`](../art/concepts/station-escape-ship-combat-v1/escape-cutter-interior-cutaway-v1.png) | Exterior and interior direction to test against the Phase 7 greybox |
| `vehicle.ship.hostile_interceptor.v1` | [`ship-combat-separated-clean-direction-v4.png`](../art/concepts/station-escape-ship-combat-v1/ship-combat-separated-clean-direction-v4.png) | Opposing-ship silhouette is a composition anchor, not a model brief |
| `presentation.ship_combat.separated.v1` | [`ship-combat-separated-clean-direction-v4.png`](../art/concepts/station-escape-ship-combat-v1/ship-combat-separated-clean-direction-v4.png) | Approved composition: strict overhead, player left, enemy right, bows up, central divider, no movement lines |

The complete concept pack, superseded compositions, prompts, provenance, and
review boundary are recorded in
[`art/concepts/station-escape-ship-combat-v1/README.md`](../art/concepts/station-escape-ship-combat-v1/README.md).
Final ship geometry, room layouts, production UI, and individual asset briefs
remain blocked until the deterministic Phase 7 greybox proves what the battle
actually needs. The boarder requires a separate gameplay decision because
boarding and enemy-crew simulation are explicit Phase 7 non-goals.

## Generation and approval order

1. **Before the Phase 2 exit:** keep the controlled provider comparison bounded
   to the service terminal, wall utility, and isolated drone body. Separately,
   approved roster items with production-ready art briefs may progress through
   offline source production and isolated review under ADR 0016. Do not report
   that work as bake-off evidence or integrate it into live gameplay. For the
   three bake-off IDs, freeze the entire experiment before starting any
   production-lane refinement.
2. **Phase 3:** implement and playtest the party and conversation functionality
   with the existing Vanguard greybox while its current generated 3D
   implementation is deferred. For non-deferred production assets, lock
   reference sheets, finish all part segmentation, completion, remeshing, and
   outfit fitting before the shared humanoid rig, and validate one fixed-outfit
   character-plus-weapon assembly. Return to Vanguard in a later Phase 3 pass
   and complete every gate in the deferred-status section before live
   replacement.
3. **Phase 4:** define authoritative attacks, timings, and abilities before
   production enemy rigs, combat clips, healing presentation, telegraphs, and
   effects are finalized.
4. **Phase 5:** replace only readability-critical greybox modules, author the
   visible airlock opening, and add the bounded environment polish needed for
   the complete POC.
5. **Phase 6:** harden the reusable publication and review tooling with a
   representative accepted asset.

Tripo output is always an untrusted candidate. Its untouched export is retained
in the ignored run-local workstation cache while its provider/task reference,
path, size, and provenance are committed. Large 3D binaries are not
content-hashed; tracked sources and outputs use their normal Git/LFS revision
identity. The candidate is normalized in Blender, validated, rendered in
Blender and Godot, and explicitly accepted or rejected. Subscription credits
should be used productively to complete selected assets rather than conserved
after the first viable result.
There is no general production attempt cap and no target number of variants:
select a promising source early, create alternatives only for named defects,
and invest in segmentation, completion, topology, textures, rig diagnostics,
and animation donors where they improve the finished work. Credits consumed
are not an acceptance criterion; every production brief and review gate still
applies.
