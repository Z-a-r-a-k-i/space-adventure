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
a deterministic observed phase boundary recorded by the presentation profile.
This is presentation state, not inventory or weapon switching. The POC has no
reload mechanic unless gameplay later adds one.

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

Draw and holster may publish these presentation marker identities when a clip
needs an authored landmark:

- `event.weapon.transfer_to_hand`
- `event.weapon.transfer_to_holster`

Godot reconstructs the correct attachment after pause, resume, reset, seek, or
resynchronization. Animation callbacks never apply gameplay effects.

## Implemented solo-tutorial timing

| Action | Authority | Presentation |
|---|---:|---|
| Vanguard ready/draw | 54 ticks / 1.8 s | Blender-authored reach and lift around the fitted rifle; transfer at 25%, support hand joins from 60–90% |
| Vanguard carbine shot | 9-tick wind-up + 21-tick recovery | fitted armed pose, upper-body aim and release-driven restrained recoil; lower-body locomotion remains available |
| Suppressive Fire | 6-tick wind-up + 24-tick recovery, 240-tick cooldown | same release-driven recoil, moving muzzle bolt/sound and 2 m target pulse on visual arrival |
| Enforcer body strike | 15 damage; 42-tick wind-up + 36-tick recovery | full `Right Hook`: gradual anticipation through the first 75% of wind-up, contact at source 0.30 s, remaining follow-through during recovery |
| Victory secure/holster | 54 ticks / 1.8 s | reverse authored handling path; transfer at 75% |

ADR 0027 supersedes the earlier donor timing recipe. The original firing donor
remains in the source as provenance, but is not selected for runtime shooting.
The weapon uses an orthonormal world transform below the scaled Mixamo rig;
the published body is 0.82 m long and its actual muzzle points along local -Z.
Authored grip fit plus Godot's built-in two-bone support-hand IK keeps each
palm within 3 cm of its intended grip in fully armed poses. The sampler
restores the authored pose before each frame so procedural offsets cannot
accumulate on animation-compressed constant bones.

Explicit target intent resumes after ability or aid. A released attack's
recovery deadline survives replacement, movement and Stop. Presentation uses
action identity, phase start, tick and fraction; exact paused stepping uses the
same sampler and event clock. See `SOLO-REVIEW.md` for bounded visual and
real-time checks.

Vanguard also publishes `Rifle Aiming Idle`, in-place `Rifle Walk`, and `Rifle
Death`. The Enforcer publishes `Falling Back Death`. Presentation reads
observations and typed release, damage, interruption, victory, and defeat
events. Damage never selects a hit-reaction animation: floating values, health
changes, and impact flashes provide the feedback for normal and special hits.
Carbine shots and Suppressive Fire launch a bright cyan bolt from the actual
muzzle, with a short tapered trail that grows behind it. Visual travel uses
28 m/s clamped to 0.14–0.24 s for readability at close range. The bolt samples
the same tick/fraction clock as recoil, freezes during tactical pause, and
clears on retry. Impact flashes, floating damage numbers, and the suppression
pulse wait for visual arrival; health, interruption, and combat resolution retain
their authoritative release timing. The destination is fixed at release;
these effects do not perform collision checks or resolve damage.

Field Aid follows the same authority boundary: `HealingApplied` spawns a short
green pulse and a floating positive value at the target. The effect validates
the current healing presentation without requiring a handled prop or skeletal
healing clip, and it never changes health itself.

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
