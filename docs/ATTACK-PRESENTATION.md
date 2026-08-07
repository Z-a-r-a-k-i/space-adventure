# Attack presentation contract

## Purpose

Every combatant must visibly communicate its attack source, direction, wind-up,
release or contact, and recovery. Presentation never defines damage, range,
targeting, cooldowns, or authoritative resolution.

## Supported sources

| Source | POC use | Required interface |
|---|---|---|
| `handheld` | Humanoid pistol, carbine, or shotgun | Hand and holster sockets; weapon primary grip, optional support grip, and muzzle |
| `integrated` | Simple rigid sentry gun | Visible muzzle plus bounded aim and recoil pivots |
| `body` | Humanoid strike | Reinforced contact surface and contact marker |

Non-humanoid combatants are floating or stationary rigid machines. These
source classes do not authorize creatures, quadrupeds, walkers, organic
deformation, or transforming rigs.

## POC profiles

| Profile | Source | Delivery |
|---|---|---|
| Vanguard | `handheld` | Separate two-handed carbine |
| Operator | `handheld` | Deferred separate pistol |
| Protector | `handheld` | Separate two-handed shotgun; fixed POC recruit after the solo tutorial fight |
| Security Enforcer | `body` | Humanoid security android with a reinforced-forearm strike |
| Gun sentry | `integrated` | Fixed or floating chassis with aim pivot, muzzle, and recoil axis |

Party weapons are holstered during exploration and transferred to the hand at
a deterministic animation marker. This is presentation state, not inventory or
weapon switching. The POC has no reload mechanic unless gameplay later adds
one.

## Interfaces

Character:

- `socket.weapon.hand_primary`
- `socket.weapon.holster_primary`

Weapon:

- `socket.grip.primary`
- optional `socket.grip.support`
- `socket.attack.muzzle.primary`

Body attacker:

- `socket.attack.contact.primary`

Weapon roots coincide with the primary grip. Muzzle and contact frames point
local `-Z` outward with local `+Y` up after GLB publication.

## Required presentation

Handheld profiles require holstered idle and locomotion, draw and transfer,
armed idle and locomotion, raise/aim, fire or recoil, recovery, and holster.
Two-handed weapons require continuous primary and support-hand contact.

Integrated weapons require ready/track, wind-up, recoil, and recovery. Humanoid
body attacks require close-combat stance, wind-up, strike/contact, and recovery.
Gameplay owns world translation; combat animation is in-place.

Draw and holster use:

- `event.weapon.transfer_to_hand`
- `event.weapon.transfer_to_holster`

Godot reconstructs the correct attachment after pause, resume, reset, seek, or
resynchronization. Animation callbacks never apply gameplay effects.

## Brief requirements

Before final animation, each combatant brief records:

- stable presentation asset and separate weapon IDs;
- source class and visible direction;
- required sockets, grips, muzzle or contact marker;
- carried, ready, attack, recovery, and holster states where applicable;
- movement and in-place constraints; and
- the exact assembly used for Blender and Godot review.

Ability-specific animation waits for an accepted gameplay ability, target
shape, source, and timing.

The active route may prepare character rigs, idle, locomotion, and weapon fits
before combat, but fight clips are selected and finalized with the Phase 4
solo-tutorial and party-combat timings. Operator does not receive production
combat animation while its scope remains deferred.

## Approval gates

1. Concept silhouette and attack source.
2. Exact model assembly and clearances.
3. Validated humanoid rig markers or simple machine pivots.
4. Full-speed animation and key landmarks without clipping.
5. Exact GLB assembly in Godot at 14.5 m and 20 m.

Muzzle flashes, tracers, projectiles, telegraphs, hit effects, collision, and
damage volumes remain Godot-authored presentation or gameplay, never baked
into the character, weapon, or machine GLB.
