# Production record — Security gun sentry v1

Status: approved by the project owner in the combined Godot gallery on 2026-08-07

| Field | Value |
|---|---|
| Asset ID | `machine.security.gun_sentry.v1` |
| Builder | Blender 5.2.0 LTS, `tools/blender/build_gun_sentry_v1.py` |
| Source | `art/source/machine.security.gun_sentry.v1/gun-sentry-v1.blend` |
| Publication | `game/Assets/Published/machine.security.gun_sentry.v1.glb` |
| Source size | 150,364 bytes |
| Publication size | 287,144 bytes |
| Mesh objects | 4 / 8 |
| Triangles | 4,244 / 8,000 |
| Materials | 3 / 3 |
| Height | 2.15 m |
| Footprint | 1.00 × 1.00 m |
| Blender-space bounds | `(-0.50, -0.45, 0.00)` to `(0.50, 0.55, 2.15)` |
| Fresh Blender GLB reimport | Passed |

The rigid hierarchy is `Base → Aim_Pivot → Gun_Housing`, `Threat_Sensor`, and
`Recoil`; `Barrel` and `socket.attack.muzzle.primary` are children of
`Recoil`. The aim metadata limits yaw to ±60° and pitch to −15°/+25°. Recoil
uses local `+Z` with a maximum travel of 0.08 m. The muzzle publishes local
`-Z` forward and `+Y` up.

The asset has no armature, skinning, or authored actions. Phase 4 presentation
will drive the aim and recoil transforms from authoritative combat state.
