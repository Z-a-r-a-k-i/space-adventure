# Attack presentation contract

The core resolves attacks. Godot presents their observed source, facing,
wind-up, release/contact, and recovery. Stats and fixed-tick durations have one
source: [station-route.json](../game/content/station-route.json). Order and
pause rules live in [Architecture](ARCHITECTURE.md).

## Asset interface

| Source | Current role | Required presentation |
| --- | --- | --- |
| Handheld | Vanguard carbine; Protector shotgun; Medic pistol; ranged Enforcer rifle | Separate weapon, primary grip, optional support grip, muzzle, hand/holster attachments |
| Body | Security Enforcer | Reinforced strike surface and contact socket |
| Integrated | Rigid sentry | Muzzle with bounded aim and recoil pivots |

Character attachments are `socket.weapon.hand_primary` and
`socket.weapon.holster_primary`. Weapons expose `socket.grip.primary`, optional
`socket.grip.support`, and `socket.attack.muzzle.primary`. Body attacks expose
`socket.attack.contact.primary`. Weapon root coincides with the primary grip;
published frames use local `-Z` outward and `+Y` up.

Handheld profiles cover holstered idle/walk, draw, armed idle/walk, aim/recoil,
recovery, holster, and down. Rifles and shotgun require two-handed armed
contact, with authored one-handed transfer portions. Medic's pistol is
one-handed: keep her left hand clear and do not invent a support grip.
Draw/holster landmarks may be named
`event.weapon.transfer_to_hand` and `event.weapon.transfer_to_holster`.
Attachments must reconstruct correctly after pause, seek, retry, or resync.
World movement remains core-owned; combat animation is in-place.

## Playback pacing

`AnimationPacing` in [SkeletalPosePlayer.cs](../game/scripts/SkeletalPosePlayer.cs)
owns the shared presentation rate. Locomotion instead derives playback from
the actor's configured travel speed and the source clip's fitted stride speed.
Draw, holster, Enforcer contact, and Barrier deployment map to observed phase
durations; do not multiply those normalized samples again. Down playback uses
the faster rate while clamping to the original clip length, so the full fall
still completes. Blends, recoil, and short impact effects use the same pacing.

Combat phase durations and hostile projectile travel remain content-owned
fixed-tick rules. Preserve the Enforcer's practical Interrupt reaction window
when tuning its wind-up. Cooldowns, status lifetimes, pause, and readable labels
do not inherit the animation multiplier.

## Current combat mapping

- Armed humanoids use `ArmedHumanoidPresentation`: fitted authored handling,
  upper-body aim and release-driven recoil, with support-hand IK only for
  two-handed weapons. Protector uses
  stronger shotgun recoil. The older Firing Rifle donor
  remains provenance, not the runtime shooting driver. Draw transfers at 25%,
  support joins over 60–90%, and holster transfers at 75% of observed duration.
- Preserve metric weapon size below the scaled Mixamo rig with an orthonormal
  world transform. Review checks each weapon's metric length and at most 3 cm
  armed palm-proxy-to-grip error. Restore the authored bone pose before applying
  procedural corrections so repeated sampling cannot accumulate offsets.
  If locomotion carries the foregrip beyond support-arm reach, fold the weapon
  arm inward before support IK, preserving wrist orientation and segment lengths.
- Enforcer samples the full Right Hook: source 0–0.17 s during the first 35%
  of wind-up, sustains the loaded shoulder through source 0.17–0.19 s until
  75%, then commits through 0.19–0.30 s during the final quarter. Recovery
  presents the remaining follow-through. The authoritative reaction window
  and contact tick are unchanged. Dashed ground links point from each winding
  hostile to its target; a contracting ring gains four corner marks in the final
  quarter. Sentry and rifle Enforcer use ranged threat cues. The rifle
  derivative must remain distinguishable from melee Enforcers by its armed
  silhouette, even when several enemies overlap in the camera view.
- Sentry aim turns around the gun mount with bounded yaw/pitch and turn speed;
  close or rear targets cannot flip the head. Its adapter corrects the retained
  GLB's floor-level empty pivots and reconstructs the barrel-tip muzzle.
- Animation and effects share simulation tick/fraction time, including pause
  and exact stepping. Crew bolts launch from the actual muzzle; their impact
  effects wait for visual arrival while core damage resolves at release.
  Sentry and hostile rifle bolts instead follow core launch/block/arrival events. A barrier hit
  removes the bolt and flashes the shield without a damage number.
- Barrier expands upward from its placed ground base. Its clipped outline
  matches the core hit plane. The preview faces from Protector toward the pointer
  and shows the protected side; one click fixes that pose, preserved by the queued tint.
  Burst presents each release from the carbine muzzle. Taunt uses a
  radius pulse and threat countdowns. Crew cards and target lines show attack state.
  Damage uses health, values, and effects. Shared projectile resources and
  cues warm at startup.
- Contact effects use short radial sparks for carbine, a wider pellet fan for
  shotgun, three heavy streaks for melee, and a hexagonal shield ripple for
  blocks. Interrupt uses an area pulse and a separate interrupted-target cross;
  Burst marks each release. Barrier deployment and Taunt expand from their
  observed placement/radius. Their transient shapes fade while sustained state
  remains in the barrier silhouette and overhead status. All geometry samples
  the shared presentation clock with resources warmed before combat.
  Taunt releases a body-centered pulse without weapon recoil.
- Heal shows the actual healed recipient and amount from core events. Healing
  Field's preview shows its radius and currently affected living crew; its
  deployed shape follows observed world position and expiry. Healing uses
  distinct restorative feedback without firearm recoil or resurrection cues.
  Pause, replacement, encounter completion, defeat, and retry must leave no
  stale field geometry or healing feedback.
- Crew down clips retain the Rifle Death donor's pelvis rotation and bake
  evaluated mesh contact with the floor. They start at the observed defeat tick. Ordinary pause freezes
  them; after total defeat, a bounded presentation-only clock lets the final
  fall finish while the core stays paused. Retry resets that clock and pose.
  Hostile humanoids snap to their standing pose during encounter readying so
  retry cannot retain a frozen blend from a previous fall.
- Hostiles and the sentry carry a warm screen-space silhouette outline and
  crew a fainter cool one (crew weapons excluded), so dark armour reads against
  the deck. It fades from the observed defeat tick and returns on retry or
  victory recovery; tints and widths live in
  [GameHost.Atmosphere.cs](../game/scripts/GameHost.Atmosphere.cs).

Victory recovery returns fallen crew to travel presentation. The cutter's
boarding entrance and engines use observed route completion to stage boarding,
closure, and takeoff. That animation cannot complete gameplay or advance rules.

Crew projectile speed/trail/delay details belong in
[CarbineProjectile.cs](../game/scripts/CarbineProjectile.cs) and
[GameHost.CombatEffects.cs](../game/scripts/GameHost.CombatEffects.cs), rather than another tuning table.
Review full-speed draw, repeated fire, contact, recovery, and retry using
[Testing](testing.md). Recordings establish motion; real-time profiles measure
frame pacing. Asset production and publication follow [Art pipeline](ART-PIPELINE.md).

## Audio

[CombatAudio.cs](../game/scripts/CombatAudio.cs) serves the `station.*` cues of
the shared sampled CC0 library ([game audio](../game/audio/README.md)), built by
`tools/audio/build_game_audio.py`. Up to four takes per cue rotate without
touching simulation randomness.
Carbine, shotgun, sentry, Burst, Interrupt, hit types, shield block, Barrier,
and Taunt have separate recipes. Release cues start at the muzzle; hit cues
start at visual contact with the same delay as effects and damage numbers.
Shield block has its own metallic ring and never produces damage feedback.
Spatial attenuation and bounded cue levels limit simultaneous actions. A
periodic ventilation source locates the station's machinery. Tactical pause
suspends it and active combat audio; released hit cues finish with the bounded
fall animation after total defeat.

For local listening evidence, the library builder writes an ignored
`artifacts/audio-review/audition.wav` montage of every take at its in-game
level with `timing.json`. Setting `SPACE_ADVENTURE_AUDIO_REVIEW` to an ignored
directory before a development launch writes an inventory of available station
takes, levels, and buses to `timing.json`; it does not record playback events.
Judge the final spatial mix during live gameplay too.
