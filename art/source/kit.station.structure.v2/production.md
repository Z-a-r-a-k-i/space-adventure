# Production record — station structure kit v2

Status: technically validated and integrated; owner visual approval pending

| Field | Value |
|---|---|
| Asset ID | `kit.station.structure.v2` |
| Builder | Blender 5.2.0 LTS, `tools/blender/build_station_environment_v2.py --asset structure` |
| Source | `art/source/kit.station.structure.v2/structure-v2.blend` |
| Publication | `game/Assets/Published/kit.station.structure.v2.glb` |
| Source size | 155,749 bytes |
| Publication size | 823,292 bytes |
| Mesh objects | 42 |
| Triangles | 11,772 / 30,000 |
| Materials | 3 / 4 |
| Godot-space bounds | `(-15.34, -0.20, -4.34)` to `(12.34, 2.80, 13.34)` |
| Fresh Blender GLB reimport | Passed |
| Godot 4.7.1 import and headless traversal | Passed |
| Contextual Godot inspection at 14.5 m and 20 m | Passed |

The source contains five individually named floors, 23 individually named wall
segments with unique `occluder_id` metadata, eight junction posts, and six cyan
route-strip segments. It contains no collision, navigation, lighting, gameplay
state, rig, or animation. The one retained checkpoint render is ignored at
`artifacts/visual/blender/station-structure-v2.png`.
