# Production record — station service surround

Status: integrated and technically validated; owner visual review pending.

| Field | Value |
| --- | --- |
| Asset ID | `assembly.station.service_surround.v1` |
| Source | `service-surround-v1.blend` — 185,155 bytes |
| Publication | `game/Assets/Published/assembly.station.service_surround.v1.glb` — 722,104 bytes |
| Builder | Blender 5.2 LTS; `tools/blender/build_station_environment_v2.py --asset service-surround` |
| Topology | 7,360 / 12,000 triangles; 4 joined meshes; 4 / 4 materials |
| Godot-space bounds | `(-128, -5.25, -128)` to `(128, -0.225, 128)` |
| Provenance / rights | Locally authored geometry using the project's approved structure reference; no external assets or provider inputs |
| Exact GLB fresh import | Blender and Godot passed; base factors and vertex colors preserved |

The [brief](../../briefs/station-service-surround-v1.md) owns the design and
integration constraints. Foundations, ventilation banks, coolant reservoirs,
and pipe bundles sit below the playable deck. The exported meshes contain no
collision, navigation, animation, interaction IDs, or wall-cutaway metadata.
The 2026-09-09 composition revision concentrates seven unequal ventilation banks
and three coolant racks near the room silhouettes. Taller extraction plant
backs the main arena; smaller nearby housings leave deliberate open recesses.
Dimmer framing, sparse utility lamps, and darker distant pipes/seams let the
background recede. The station scene owns distant depth fog, which hides the
far clipping edge at low camera pitch.

Rebuild with the command above and `--replace`. Agent Godot review passed at
7.5, 14.5, and 20 m, including sampled pitch/yaw limits and 720p/1080p. Review
evidence stays ignored under `artifacts/`.
