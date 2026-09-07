# POC asset inventory

This is the approved bounded inventory, not authorization to generate or
integrate everything now. [Roadmap](ROADMAP.md) owns sequencing;
[Art pipeline](ART-PIPELINE.md) owns production and approval steps. Asset briefs
hold unique design/budget requirements; source records/manifests hold provenance.

| Asset ID | Role / scope | Brief |
| --- | --- | --- |
| `character.crew.vanguard.v1` | Active protagonist; base and solo presentation integrated | [Vanguard](../art/briefs/vanguard-character-v1.md) |
| `weapon.crew.vanguard_carbine.v1` | Separate two-handed carbine; integrated | [Carbine](../art/briefs/vanguard-carbine-v1.md) |
| `character.crew.protector.v1` | Fixed post-solo recruit; base integrated, combat pending | [Protector](../art/briefs/protector-character-v1.md) |
| `weapon.crew.protector_shotgun.v1` | Two-handed shotgun; finalize fit and combat with party slice | [Shotgun](../art/briefs/protector-shotgun-v1.md) |
| `character.npc.station_survivor.v1` | Noncontrollable survivor with dialogue; integrated | [Survivor](../art/briefs/station-survivor-v1.md) |
| `character.enemy.security_enforcer.v1` | Mobile humanoid melee; integrated in solo fight | [Enforcer](../art/briefs/security-enforcer-character-v1.md) |
| `machine.security.gun_sentry.v1` | Floor-bolted rigid ranged hostile; approved base, later combat | [Sentry](../art/briefs/security-gun-sentry-v1.md) |
| `kit.station.structure.v2` | Five-area dimensional station kit; integrated | [Structure](../art/briefs/station-structure-kit-v2.md) |
| `assembly.station.service_door.v1` | Two ordinary arena-boundary doors; integrated | [Service door](../art/briefs/station-service-door-v1.md) |
| `prop.station.service_terminal.v1` | Optional inspection; integrated | [Terminal](../art/briefs/station-service-terminal-v1.md) |
| `assembly.station.evacuation_airlock.v1` | Final destination; model integrated, completion gated | [Airlock](../art/briefs/station-evacuation-airlock-v1.md) |
| `item.healing.field_aid.v1` | One healing-item type; handled prop optional, current use is a pulse/value | No additional model required for the solo slice |
| `prop.station.wall_utility.v1` | Retained technical candidate; owner approval and Phase 5 integration pending | [Wall utility](../art/briefs/station-wall-utility-v1.md) |
| `character.crew.operator.v1` / `weapon.crew.operator_pistol.v1` | Deferred; do not resume production without owner direction | [Character](../art/briefs/operator-character-v1.md), [pistol](../art/briefs/operator-pistol-v1.md) |

Humanoids use the [shared rig contract](../art/rigs/crew-humanoid-v1.md), with
only the actions their role needs. Each human has one fixed outfit. Attack
sources, attachment interfaces, and live combat mapping are defined in
[ATTACK-PRESENTATION.md](ATTACK-PRESENTATION.md); do not reproduce them here.

Preserve validated inanimate candidates; a humanoid workflow change is not a
reason to regenerate them. Exact sources and accepted exports are recorded
under `art/source/<asset-id>/` and `art/generated/<asset-id>/`.

Ship/launch-bay and other exploratory concepts remain outside this production
inventory. Their [reference pack](../art/concepts/station-escape-ship-combat-v1/README.md)
preserves direction and IDs; final art follows the separately gated ship greybox.
