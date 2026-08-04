# Production record — station service door v1

Status: technically validated and integrated; owner visual approval pending

| Field | Value |
|---|---|
| Asset ID | `assembly.station.service_door.v1` |
| Builder | Blender 5.2.0 LTS, `tools/blender/build_station_environment_v2.py --asset service-door` |
| Source | `art/source/assembly.station.service_door.v1/service-door-v1.blend` |
| Publication | `game/Assets/Published/assembly.station.service_door.v1.glb` |
| Source size | 110,039 bytes |
| Publication size | 101,708 bytes |
| Mesh objects | 5 |
| Triangles | 1,404 / 4,000 |
| Materials | 3 / 3 |
| Godot-space bounds | `(-1.50, 0.00, -0.175)` to `(1.50, 2.65, 0.175)` |
| Fresh Blender GLB reimport | Passed |
| Godot 4.7.1 import and headless door traversal | Passed |
| Contextual Godot inspection | Passed for both instances |

The exact top-level meshes are `Frame`, `Door_Left`, `Door_Right`,
`Status_Strip`, and `Control_Panel`. The two door leaves are rigid parts. The
default status material is amber; Godot supplies the cyan open-state override.
The first render exposed recessed state elements, which were moved to a readable
front plane without changing the approved 3.00 × 2.65 × 0.35 m envelope. The
one checkpoint render is retained locally and excluded from version control at
`artifacts/visual/blender/station-service-door-v1.png`.
