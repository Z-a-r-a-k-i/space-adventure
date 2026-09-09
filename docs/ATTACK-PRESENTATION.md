# Attack presentation contract

The core resolves attacks. Godot presents their observed source, facing,
wind-up, release/contact, and recovery. Stats and fixed-tick durations have one
source: [station-route.json](../game/content/station-route.json). Order and
pause rules live in [Architecture](ARCHITECTURE.md).

## Asset interface

| Source | Current role | Required presentation |
| --- | --- | --- |
| Handheld | Vanguard carbine; Protector shotgun | Separate weapon, primary/support grips, muzzle, hand/holster attachments |
| Body | Security Enforcer | Reinforced strike surface and contact socket |
| Integrated | Rigid sentry | Muzzle with bounded aim and recoil pivots |

Character attachments are `socket.weapon.hand_primary` and
`socket.weapon.holster_primary`. Weapons expose `socket.grip.primary`, optional
`socket.grip.support`, and `socket.attack.muzzle.primary`. Body attacks expose
`socket.attack.contact.primary`. Weapon root coincides with the primary grip;
published frames use local `-Z` outward and `+Y` up.

Handheld profiles cover holstered idle/walk, draw, armed idle/walk, aim/recoil,
recovery, holster, and down. Two-handed contact is required when armed, with
authored one-handed transfer portions. Draw/holster landmarks may be named
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

- Both crew use `ArmedHumanoidPresentation`: fitted authored handling,
  upper-body aim, release-driven recoil, and support-hand IK. Protector uses
  stronger shotgun recoil. The older Firing Rifle donor
  remains provenance, not the runtime shooting driver. Draw transfers at 25%,
  support joins over 60–90%, and holster transfers at 75% of observed duration.
- Preserve metric weapon size below the scaled Mixamo rig with an orthonormal
  world transform. Review checks each weapon's metric length and at most 3 cm
  armed palm-proxy-to-grip error. Restore the authored bone pose before applying
  procedural corrections so repeated sampling cannot accumulate offsets.
- Enforcer samples the full Right Hook: source 0–0.10 s during the first 75%
  of wind-up, 0.10–0.30 s during the final 25%, then remaining follow-through
  during recovery. This presents the authoritative contact tick.
- Sentry aim turns around the gun mount with bounded yaw/pitch and turn speed;
  close or rear targets cannot flip the head. Its adapter corrects the retained
  GLB's floor-level empty pivots and reconstructs the barrel-tip muzzle.
- Animation and effects share simulation tick/fraction time, including pause
  and exact stepping. Crew bolts launch from the actual muzzle; their impact
  effects wait for visual arrival while core damage resolves at release.
  Sentry bolts instead follow core launch/block/arrival events. A barrier hit
  removes the bolt and flashes the shield without a damage number.
- Barrier expands upward from its placed ground base. Its clipped outline
  matches the core hit plane. The preview faces from Protector toward the pointer
  and shows the protected side; one click fixes that pose, preserved by the queued tint.
  Burst presents each release from the carbine muzzle. Taunt uses a
  radius pulse and threat countdowns. Crew cards and target lines show attack state.
  Damage uses health, values, and effects. Shared projectile resources and
  cues warm at startup.
- Crew down clips retain the Rifle Death donor's pelvis rotation and bake
  evaluated mesh contact with the floor. They start at the observed defeat tick. Ordinary pause freezes
  them; after total defeat, a bounded presentation-only clock lets the final
  fall finish while the core stays paused. Retry resets that clock and pose.

Crew projectile speed/trail/delay details belong in
[CarbineProjectile.cs](../game/scripts/CarbineProjectile.cs) and
[GameHost.cs](../game/scripts/GameHost.cs), rather than another tuning table.
Review full-speed draw, repeated fire, contact, recovery, and retry using
[Testing](testing.md). Recordings establish motion; real-time profiles measure
frame pacing. Asset production and publication follow [Art pipeline](ART-PIPELINE.md).
