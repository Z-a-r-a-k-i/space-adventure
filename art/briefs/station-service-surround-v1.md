# Asset brief — station service surround

Scope authorized by the owner's request to fill the station's surrounding
black space on 2026-09-09. Implementation remains subject to visual review.

| Field | Requirement |
| --- | --- |
| Asset ID | `assembly.station.service_surround.v1` |
| Role | Recessed engineering infrastructure around the five playable rooms |
| Reference | [Approved structure kit](../reference-sheets/frontier-station-v1/poc-models/station-structure-kit-reference-v1.png) and [visual bible](../bible/frontier-station-v1.md) |
| Owner / source | Project owner; dimensional Blender authoring, no third-party inputs |
| Axes / pivot | +Y up, -Z front; station origin at the finished floor |
| Budget | 12,000 triangles, 4 materials, 4 joined meshes |

Support the room silhouettes with deep foundations, broad ventilation banks,
bundled pipes, structural ribs, and sparse dim utility lamps. Keep colors and
detail quieter than the playable deck. All geometry stays below y=-0.20 m;
there are no walkable extensions, collision, navigation, interaction, animation,
or camera-occluder metadata. Existing room and doorway contracts remain intact.

Validate bounds, topology/material budgets, and exact GLB fresh import. Inspect
in Godot at 7.5, 14.5, and 20 m, including low pitch and alternate yaw. Check
route traversal, input picking, wall cutaway, and real-time frame pacing.
