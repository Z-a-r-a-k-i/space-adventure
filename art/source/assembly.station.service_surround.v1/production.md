# Production record — station service surround

Status: integrated and technically validated; owner visual review pending.

| Field | Value |
| --- | --- |
| Asset ID | `assembly.station.service_surround.v1` |
| Source | `service-surround-v1.blend` — 194,777 bytes |
| Publication | `game/Assets/Published/assembly.station.service_surround.v1.glb` — 657,904 bytes |
| Builder | Blender 5.2 LTS; `tools/blender/build_station_environment_v2.py --asset service-surround` |
| Topology | 9,060 / 12,000 triangles; 4 joined meshes; 4 / 4 materials |
| Godot-space bounds | `(-128, -5.25, -128)` to `(128, -0.225, 128)` |
| Provenance / rights | Locally authored geometry using the project's approved structure reference; no external assets or provider inputs |
| Exact GLB fresh import | Blender and Godot passed |

The [brief](../../briefs/station-service-surround-v1.md) owns the design and
integration constraints. Foundations, ventilation banks, coolant reservoirs,
and pipe bundles sit below the playable deck. The exported meshes contain no
collision, navigation, animation, interaction IDs, or wall-cutaway metadata.
Low material emission supplies a restrained fill in shadow. The station scene
owns distant depth fog, which hides the far clipping edge at low camera pitch.

Rebuild with the command above and `--replace`. Reviewed at 7.5, 14.5, and 20 m,
including both camera pitch limits and alternate yaw. Route victory/defeat,
wall cutaway, and solo/party graphical input checks passed. Review evidence is
ignored under `artifacts/party-review/` and `artifacts/visual/captures/`.
