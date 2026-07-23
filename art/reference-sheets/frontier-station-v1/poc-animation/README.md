# POC animation key-pose directions

Status: retained under owner-delegated continuation

Generated: 2026-07-23

These sheets close the accepted concept gate for basic weapon and machine
attack presentation without defining animation timing or gameplay behavior:

| Sheet | Coverage | Lifecycle |
|---|---|---|
| [Vanguard weapon handling](vanguard-weapon-handling-key-poses-v1.png) | Holstered, draw transfer, armed idle, aim, recoil, recovery, and holster transfer | Rigging and animation direction |
| [Operator weapon handling](operator-weapon-handling-key-poses-v1.png) | Right-thigh draw, one-hand pistol presentation, recoil, recovery, and holster transfer | Rigging and animation direction |
| [Protector weapon handling](protector-weapon-handling-key-poses-v1.png) | Back-mount draw, two-hand shotgun support, recoil, pump recovery, and holster transfer | Rigging and animation direction |
| [Security ram drone](security-ram-drone-key-poses-v1.png) | Idle, locomotion, turn, alert, brace, contact, rebound, and shutdown | Machine-rig and animation direction |
| [Security gun sentry](security-gun-sentry-key-poses-v1.png) | Idle, locomotion, turn, track, aim, recoil, recovery, and shutdown | Machine-rig and animation direction |

Exact prompts, source roles, hashes, and the Operator grip correction are in
`animation-direction-batch-06.provenance.md`.

These images are silhouette and articulation references. They are not accepted
animation frames, skeletons, weights, socket transforms, attack shapes, root
motion, timing, damage, projectiles, or gameplay authority. Blender owns final
rigging and clips; Godot presents observed gameplay state.

The three ability-specific clips remain blocked until the corresponding
gameplay abilities, target shapes, sources, and timings are accepted.
