# Production record — station structure kit v2

Status: technically validated and integrated; owner visual approval pending

| Field | Value |
|---|---|
| Asset ID | `kit.station.structure.v2` |
| Builder | Blender 5.2.0 LTS, `tools/blender/build_station_environment_v2.py --asset structure` |
| Source | `art/source/kit.station.structure.v2/structure-v2.blend` |
| Publication | `game/Assets/Published/kit.station.structure.v2.glb` |
| Source size | 223,669 bytes |
| Publication size | 2,052,984 bytes |
| Mesh objects | 42 |
| Triangles | 28,120 / 30,000 |
| Materials | 4 / 4 |
| Godot-space bounds | `(-15.35, -0.20, -4.35)` to `(12.34, 2.80, 13.35)` |
| Fresh Blender GLB reimport | Passed |
| Godot 4.7.1 import and headless traversal | Passed |
| Contextual Godot inspection at 14.5 m and 20 m | Passed |

The owner-requested environment polish on 2026-09-09 adds framed floor plates,
matte deck inserts, wall panels, ribs, and inset cyan luminaires. These remain
joined into the original five floors and 23 walls with unchanged `occluder_id`
metadata; eight junction posts and six route strips retain their identities.
All geometry is authored locally from the approved structure reference; no
third-party input was added. Reproduce with the builder above and `--replace`.

The source contains no collision, navigation, light nodes, gameplay state,
rig, or animation. Godot owns floor-trim/route emission tuning, localized light
pools, and floor lettering in the station scene. Review captures remain ignored
under `artifacts/party-review/` and `artifacts/visual/captures/`.
