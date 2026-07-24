# Attack presentation and weapon asset contract

Status: accepted preproduction contract; gameplay implementation remains in
Phases 3 and 4.

Revision: 2026-07-24

## Purpose

Every production combatant must visibly explain how it attacks before its model
or animations are approved. A combatant does not necessarily need a separate
weapon, but it always needs at least one credible attack source and a complete
presentation plan.

This document joins combat design, concept art, modeling, rigging, animation,
and Godot presentation. It does not begin Phase 4 combat implementation or add
an equipment system to the POC.

The approved POC roster now selects the presentation loadouts summarized below.
Those selections do not define stable gameplay attack identities, ranges,
timings, damage, active abilities, or encounter tuning. An offline
source-production brief may use the approved source class, visible delivery,
parts, interfaces, and articulation to authorize static modeling, shared-rig
work, assembly fitting, generic reusable motion, and provider animation donors.
It marks unresolved gameplay fields explicitly rather than inventing them.
Stable gameplay identities and timing remain Phase 3 and 4 dependencies and
must exist before final attack clips, synchronization, production combatant
approval, or live integration.

Disposable shape or topology experiments may explicitly exclude this contract.
They do not become production combatants until a complete assembly passes it.

## Authority boundary

The pure C# gameplay core owns:

- stable combatant and attack identities;
- target validity, range, affected area, line-of-sight rules, approach
  movement, and facing rules that affect play;
- projectile travel when it has authoritative gameplay consequences;
- fixed-tick wind-up, resolution, recovery, repetition, cooldown, damage,
  interruption, victory, and defeat; and
- the observations and events from which presentation is synchronized.

Godot presentation owns:

- the visual source class described below;
- character, weapon, and machine scenes;
- attachment nodes, rig articulation, animation playback, telegraph rendering
  and style, muzzle flashes, tracer and projectile visuals, hit effects, audio,
  and camera feedback; and
- mapping a stable gameplay attack identity to its reviewed presentation
  profile.

Meshes, node paths, sockets, animation names, and physical weapon collisions
never determine damage or enter authoritative saved state. Animation callbacks
may place purely cosmetic cues after observed gameplay state authorizes them,
but they never apply damage, advance an attack, or tell the core that a hit
occurred.

## Attack-source classes

These are presentation classifications, not separate gameplay architectures or
a required core enum.

| Source | Typical use | Asset relationship | Required visual interface |
|---|---|---|---|
| `handheld` | Humanoid pistol, rifle, or shotgun | Character and weapon are separate presentation assets | Hand and holster attachments; primary grip; support grip when two-handed; muzzle or strike marker |
| `integrated` | Robot turret, arm cannon, or built-in gun | Weapon is a permanent part or rigid child of the combatant assembly | Visible attack direction and muzzle; named aim and recoil articulation when needed |
| `body` | Head-bump, charge, claw, bite, or chassis slam | No separate weapon is required | Reinforced striking surface, contact marker, and enough articulation for a readable strike |

A fixed handheld weapon remains separate from the humanoid mesh so it can align
correctly in the hand, move between carried and ready states, and be revised
independently. This does not imply weapon pickup, swapping, ammunition, reload,
equipment slots, or an inventory UI.

An integrated weapon may share a mesh or armature with its machine. It still
needs an unambiguous muzzle or attack direction and clearance through its full
aim and recoil range.

A body attacker must look capable of surviving and delivering its attack. A
red sensor alone does not explain a head-bump; the model needs a readable
bumper, armored brow, claw, horn, ram plate, or other contact mass.

## Approved POC presentation profiles

| Profile | Source | Approved visible delivery | Required relationship |
|---|---|---|---|
| Vanguard | `handheld` | Broad two-handed carbine | Separate character and weapon assets; primary hand, holster, support grip, and muzzle interfaces |
| Operator | `handheld` | Compact pistol | Separate character and weapon assets; primary hand, holster, and muzzle interfaces |
| Protector companion | `handheld` | Broad two-handed shotgun | Separate character and weapon assets; primary hand, holster, support grip, and muzzle interfaces |
| Compact security ram drone | `body` | Reinforced forward ram with brace, decisive contact, rebound, and recovery | Complete production machine with a contact marker; the disposable smoke-test walker and isolated bake-off body do not satisfy this profile |
| Tall security gun sentry | `integrated` | Built-in ranged gun with visible aim direction, muzzle, wind-up, recoil, and recovery | Complete production machine with aim/recoil articulation and a muzzle marker |

All three party profiles show their firearm holstered during exploration,
perform a deterministic draw/attachment transfer before armed presentation,
and provide the corresponding holster transfer. This visual state change does
not alter equipment or authoritative gameplay state. The POC has no reload
mechanic.

The active abilities remain deliberately undefined. Do not derive an ability
from the concept-art shield or produce ability-specific props, clips, icons, or
effects until the Phase 3 ability definitions are accepted. See
`POC-ASSET-ROSTER.md` for the complete planned inventory and phase ownership.

## Required brief fields

Every production combatant brief lists:

- the combatant presentation asset ID and the stable gameplay attack reference
  once it exists;
- one source class for its basic attack and any additional weapon or body
  attack already defined by gameplay;
- the separate weapon asset ID or permanent integrated part, when applicable;
- holstered, carried, ready, aiming, attacking, and recovery appearance as
  applicable;
- attachment, grip, muzzle, ejection, telegraph-origin, or contact markers;
- the rig profile and any aim, recoil, support-hand, or striking joints;
- the required wind-up, release or contact, and recovery landmarks;
- whether gameplay permits movement while ready, aiming, winding up, or
  recovering;
- the intended attack direction and the cue that survives the tactical camera;
- VFX and audio anchors that will be authored in Godot; and
- the exact character-plus-weapon or complete-machine assembly used for
  Blender and Godot validation.

The brief references gameplay range and timing; it does not duplicate them as
art-authored authority.

For offline source production before the owning gameplay phase, the brief may
mark the stable gameplay attack reference, exact timing, movement permissions,
and telegraph/VFX synchronization as phase-blocked. The approved source class,
separate weapon relationship, visible attack direction, required parts,
markers, clearances, articulation, and review assembly must already be fixed.
This limited approval supports geometry, shared rigs, attachments, stress
poses, generic reusable clips, and animation-donor evaluation. It cannot pass
final animation or Godot combatant approval.

Every separate handheld weapon also receives its own asset brief. It cross-links
the compatible combatant and rig profiles and owns its bounds, material and
geometry budgets, primary and support grips, muzzle, moving parts, and any
provider provenance. The combatant brief still owns draw/holster behavior,
complete-assembly animation, and final Godot validation.

The three source classes cover the POC basic attacks. A later offensive ability
that is deployed, emitted, or otherwise does not fit them receives an explicit
presentation extension and recorded decision instead of being forced into the
wrong class.

Recommended marker names follow the existing dotted convention:

- character: `socket.weapon.hand_primary` and
  `socket.weapon.holster_primary`;
- handheld weapon: `socket.grip.primary`, optional `socket.grip.support`, and
  `socket.attack.muzzle.primary`;
- integrated weapon: `socket.attack.muzzle.primary` plus brief-specific aim
  and recoil joints; and
- body attack: `socket.attack.contact.primary`.

All published frames use `+Y` up and `-Z` forward, have unique names, unit
scale, and no shear, and are parented to the correct bone or rigid part.
Position and rotation offsets are intentional rather than zeroed blindly. A
weapon root is published coincident with its `socket.grip.primary` frame; the
character hand and holster sockets encode the final weapon-root transforms, so
runtime attachment does not apply a second grip offset. Muzzle and contact
frames point local `-Z` outward along the attack direction with local `+Y` up.

These frames are presentation interfaces, not gameplay hitboxes.
`telegraph-origin` is only a visual anchor: it never defines authoritative
range, affected area, target membership, or timing.

## Animation contract

Every basic attack has three readable landmarks:

1. **Wind-up / telegraph** — the player can identify the attacker, source,
   direction, and imminent danger.
2. **Release / contact** — the decisive pose aligns with authoritative
   resolution for an immediate attack or with authoritative projectile release.
3. **Recovery** — the attacker visibly exits the decisive pose and communicates
   when the action is over.

Minimum source-specific coverage is:

- Handheld firearm: holstered or carried idle, armed idle, armed locomotion,
  weapon raise or other wind-up, aim pose when required, fire/recoil, and
  recovery. Draw and holster are required for all three approved party
  profiles. A two-handed weapon such as the carbine or shotgun also requires a
  support-grip marker and two-hand pose validation.
- Integrated firearm: ready or track, wind-up, fire/recoil, and recovery, with
  moving-while-aimed validation when gameplay permits it.
- Body attack: brace or wind-up, strike/contact pose, rebound, and recovery.

Reload animation is required only if reload becomes a real mechanic. The POC
does not add it merely because a firearm is visible.

An ability-specific animation is reserved for each party profile but remains
blocked until its gameplay ability, target shape, source, and timing are
accepted. A generated animation must not silently choose those mechanics.

When draw or holster is shown, the presentation profile names an attachment
transfer landmark. The animated hand and weapon frames align at that landmark,
then Godot transfers the visual weapon between the holster and hand sockets
without changing equipment or gameplay state. The transfer is deterministic
under pause, resume, scenario reset, and animation resynchronization.

The presentation profile maps its clip landmarks to observed gameplay phases
and resolution timing. Playback may stretch or hold the corresponding visual
segments within reviewed limits; a timing change outside those limits returns
the clip for animation review. No animation callback advances the authoritative
phase.

Combat clips are in-place for the POC. Gameplay position remains authoritative;
a charge or lunge follows observed core movement rather than moving the actor
through animation root motion. Combat animation playback and its cues freeze
with the fixed simulation while tactical pause is active.

Do not bake muzzle flashes, tracers, projectiles, telegraph shapes, hit sparks,
selection indicators, gameplay collision, or damage volumes into a published
combatant or weapon GLB.

## Production gates

1. **Concept approval:** choose the source class and show carried or ready,
   wind-up, release or contact, and recovery silhouettes.
2. **Model approval:** validate the exact assembly, attachment clearance, attack
   direction, required markers, and deforming or rigid parts.
3. **Rig approval:** prove hand placement, support grip, aim/recoil pivots, or
   body-strike articulation before producing final clips.
4. **Animation approval:** review all required landmarks without clipping at
   full speed, stepped frame by frame, and while paused on the key poses.
5. **Godot approval:** import the exact published GLBs, bind the reviewed
   presentation profile, play it against a target dummy, and inspect the default
   14.5 m and maximum 20 m tactical views.

A production combatant is revised when viewers cannot identify its attack
source, facing, wind-up, release or contact, or recovery from the tactical
camera.

## Small walker example

The current **Utility Walker Test Unit** remains a disposable Tripo smoke test,
not an accepted enemy. If a later production design inspired by it uses a
head-bump or ram:

- reinforce the forward shell around the sensor as a bumper rather than using
  the sensor lens itself as the contact point;
- put the contact marker on that armored surface;
- compress or lean backward for the wind-up;
- present a decisive forward strike followed by an obvious rebound; and
- let authoritative gameplay movement and resolution drive the translation and
  impact.

That is a valid body-source attack and needs no gun. Choosing it requires a new
complete-machine assembly brief; it does not change or approve the smoke-test
candidate.
