# Attack presentation contract

The core resolves attacks. Godot presents their observed source, facing,
wind-up, release/contact, and recovery. Stats and fixed-tick durations have one
source: [station-route.json](../game/content/station-route.json). Order and
pause rules live in [Architecture](ARCHITECTURE.md).

## Asset interface

| Source | Current role | Required presentation |
| --- | --- | --- |
| Handheld | Vanguard carbine; later Protector shotgun | Separate weapon, primary/support grips, muzzle, hand/holster attachments |
| Body | Security Enforcer | Reinforced strike surface and contact socket |
| Integrated | Later rigid sentry | Muzzle with bounded aim and recoil pivots |

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

## Current solo mapping

- Vanguard uses fitted authored handling, upper-body aim, restrained
  release-driven recoil, and support-hand IK. The older Firing Rifle donor
  remains provenance, not the runtime shooting driver. Draw transfers at 25%,
  support joins over 60–90%, and holster transfers at 75% of observed duration.
- Preserve metric weapon size below the scaled Mixamo rig with an orthonormal
  world transform. Review enforces 0.82 m ±2% length and at most 3 cm armed
  palm-proxy-to-grip error. Restore the authored bone pose before applying
  procedural corrections so repeated sampling cannot accumulate offsets.
- Enforcer samples the full Right Hook: source 0–0.10 s during the first 75%
  of wind-up, 0.10–0.30 s during the final 25%, then remaining follow-through
  during recovery. This presents the authoritative contact tick.
- Animation and effects share simulation tick/fraction time, including pause
  and exact stepping. Release events launch cyan bolts from the actual muzzle
  toward a destination fixed at release. Travel is visual, without collision
  or damage authority. Impact flashes, floating numbers, and suppression pulse
  wait for arrival; health and interruption still resolve at release.
- Healing events produce a green pulse and positive number. Ordinary damage
  uses health, values, and effects without hit-reaction clips. Shared projectile
  resources and prototype cues are prepared at startup to reduce first-use work.

Projectile speed/trail/delay details belong in
[CarbineProjectile.cs](../game/scripts/CarbineProjectile.cs) and
[GameHost.cs](../game/scripts/GameHost.cs), rather than another tuning table.
Review full-speed draw, repeated fire, contact, recovery, and retry using
[Testing](testing.md). Recordings establish motion; real-time profiles measure
frame pacing. Asset production and publication follow [Art pipeline](ART-PIPELINE.md).
