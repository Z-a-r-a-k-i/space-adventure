# Production record — station structure kit v2

Status: technically validated and integrated; owner visual approval pending

| Field | Value |
|---|---|
| Asset ID | `kit.station.structure.v2` |
| Builder | Blender 5.2.0 LTS, `tools/blender/build_station_environment_v2.py --asset structure` |
| Source | `art/source/kit.station.structure.v2/structure-v2.blend` |
| Publication | `game/Assets/Published/kit.station.structure.v2.glb` |
| Source size | 232,745 bytes |
| Publication size | 2,696,408 bytes |
| Mesh objects | 42 |
| Triangles | 27,212 / 30,000 |
| Materials | 4 / 4 |
| Godot-space bounds | `(-15.35, -0.20, -4.35)` to `(12.34, 2.80, 13.35)` |
| Fresh Blender GLB reimport | Passed, including base factors and vertex colors |
| Godot 4.7.1 import and headless traversal | Passed |
| Contextual Godot inspection | Pending for the current composition revision |

The 2026-09-09 composition revision gives recruitment a warm three-locker wall
assembly and the transit arena a teal twin-extractor bank. Large panel accents
separate security, shelter, and evacuation; broad matte arena plates keep
combat floors quiet. Worn threshold inserts identify crossings. The landmarks
are joined into existing wall cutaways outside the navigable deck: five floors,
23 unchanged `occluder_id` values, eight junction posts, and six route strips.
The main landmark faces into the arena from the southeast wall so it stays
behind the actors in ordinary camera framing.
All geometry and vertex colors are authored locally from the approved structure
reference; no third-party input was added. Reproduce with the builder above
and `--replace`.

The source contains no collision, navigation, light nodes, gameplay state,
rig, or animation. Godot owns floor-trim/route emission tuning, localized light
pools, and floor lettering in the station scene. Review captures remain ignored
under `artifacts/party-review/` and `artifacts/visual/captures/`.
