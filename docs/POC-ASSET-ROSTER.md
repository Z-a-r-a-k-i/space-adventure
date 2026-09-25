# POC asset inventory

This is the approved bounded inventory, not authorization to generate or
integrate everything now. [Roadmap](ROADMAP.md) owns sequencing;
[Art pipeline](ART-PIPELINE.md) owns production and approval steps. Asset briefs
hold unique design/budget requirements; source records/manifests hold provenance.

| Asset ID | Role / scope | Brief |
| --- | --- | --- |
| `character.crew.vanguard.v1` | Active protagonist; base and solo presentation integrated | [Vanguard](../art/briefs/vanguard-character-v1.md) |
| `weapon.crew.vanguard_carbine.v1` | Separate two-handed carbine; integrated | [Carbine](../art/briefs/vanguard-carbine-v1.md) |
| `character.crew.protector.v1` | Fixed post-solo recruit; fitted combat presentation integrated | [Protector](../art/briefs/protector-character-v1.md) |
| `weapon.crew.protector_shotgun.v1` | Separate two-handed shotgun; integrated | [Shotgun](../art/briefs/protector-shotgun-v1.md) |
| `character.npc.station_survivor.v1` | Noncontrollable survivor with dialogue; integrated | [Survivor](../art/briefs/station-survivor-v1.md) |
| `character.enemy.security_enforcer.v1` | Mobile humanoid melee throughout the route | [Enforcer](../art/briefs/security-enforcer-character-v1.md) |
| `character.enemy.ranged_enforcer.v1` | Rifle-equipped derivative integrated in the four new fights | [Ranged Enforcer](../art/briefs/ranged-enforcer-character-v1.md) |
| `machine.security.gun_sentry.v1` | Floor-bolted ranged hostile in party, security, and launch encounters | [Sentry](../art/briefs/security-gun-sentry-v1.md) |
| `kit.station.structure.v2` | Dimensional station kit; connected route extension authorized | [Structure](../art/briefs/station-structure-kit-v2.md) |
| `assembly.station.service_surround.v1` | Owner-requested recessed station surroundings | [Service surround](../art/briefs/station-service-surround-v1.md) |
| `assembly.station.service_door.v1` | Ordinary victory-controlled route doors | [Service door](../art/briefs/station-service-door-v1.md) |
| `prop.station.service_terminal.v1` | Optional inspection; integrated | [Terminal](../art/briefs/station-service-terminal-v1.md) |
| `assembly.station.evacuation_airlock.v1` | Final destination leading to cutter boarding | [Airlock](../art/briefs/station-evacuation-airlock-v1.md) |
| `prop.station.wall_utility.v1` | Retained technical candidate; separate owner approval/integration remains pending | [Wall utility](../art/briefs/station-wall-utility-v1.md) |
| `character.crew.operator.v1` / `weapon.crew.operator_pistol.v1` | Third recruit, Medic; fitted rig, one-handed pistol and live healing integrated | [Character](../art/briefs/operator-character-v1.md), [pistol](../art/briefs/operator-pistol-v1.md) |
| `ship.escape_cutter.v1` | Approved exterior for station boarding and departure; entrance and engine presentation, no interior | [Cutter](../art/briefs/escape-cutter-v1.md) |
| `ship.escape_cutter.combat.v1` | Roof-off combat derivative, four systems, five crew spaces; v2 plated decks and light lines, owner visual acceptance pending | [Ship battle](../art/briefs/ship-battle-v1.md) |
| `ship.interceptor.v1` | Unmanned roof-off hostile with three targetable system rooms (v2); owner visual acceptance pending | [Ship battle](../art/briefs/ship-battle-v1.md) |
| `fx.ship_fire.v1` / `fx.ship_breach.v1` | Fire and torn-deck breach presentation; owner visual acceptance pending | [Ship battle](../art/briefs/ship-battle-v1.md) |

Humanoids use the [shared rig contract](../art/rigs/crew-humanoid-v1.md), with
only the actions their role needs. Each human has one fixed outfit. Attack
sources, attachment interfaces, and live combat mapping are defined in
[ATTACK-PRESENTATION.md](ATTACK-PRESENTATION.md); do not reproduce them here.

Preserve validated inanimate candidates; a humanoid workflow change is not a
reason to regenerate them. Exact sources and accepted exports are recorded
under `art/source/<asset-id>/` and `art/generated/<asset-id>/`.

The station extension and cutter exterior are approved by the 2026-09-12
expansion. The owner requested ship battle implementation and parallel asset
work on 2026-09-24; the two new ship publications have a separate brief above.
Owner handling and visual acceptance remain open. Other dock props and boarder
identity remain exploratory in the
[reference pack](../art/concepts/station-escape-ship-combat-v1/README.md).

The station extension uses `kit.station.escape.v1`: [brief](../art/briefs/station-escape-v1.md).
