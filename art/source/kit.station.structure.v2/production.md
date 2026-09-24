# Production record — station structure kit v2

Original dimensional Blender geometry and vertex colors derived from the
approved station-structure reference. No external source or license was added.
The owner's 2026-09-12 tactical-layout revision changes only the original solo
and party inner decks and their route strips; other floors, all original walls,
posts, and wall-landmark geometry remain retained. The four-fight extension is
published separately as `kit.station.escape.v1`.

| Field | Value |
|---|---|
| Asset ID | `kit.station.structure.v2` |
| Builder | Blender 5.2.0 LTS, `tools/blender/build_station_environment_v2.py --asset structure --replace` |
| Shared dimensions | `tools/station-layout.json`, `station-layout-v2` |
| Shared deck helper | `tools/blender/station_tactical_geometry.py` |
| Source | `art/source/kit.station.structure.v2/structure-v2.blend` (327,077 bytes) |
| Publication | `game/Assets/Published/kit.station.structure.v2.glb` (3,335,192 bytes) |
| Geometry | 34,092 / 36,000 triangles; 46 meshes; four materials |
| Godot bounds | `(-15.35, -1.42, -4.35)` to `(12.34, 2.80, 13.35)` |
| Fresh GLB reimport | Passed topology, bounds, material factors, vertex colors, and all 23 original occluder IDs |
| Open-deck check | Passed 25 interior ray probes per pit across the two revised floors |
| Current visual review | Blender and contextual Godot inspection passed; owner acceptance remains open |

The solo opening exposes a damaged power trunk; the party opening contains a
central conduit trench. The floor slabs and panels are actually cut around
these rectangles. Shaft linings and machinery stay below the deck, with coping
tops at 0.09 m and caution paint at 0.097 m inside the 0.40 m navigation setback.
Ten sparse route strips turn around openings. The retained recruitment lockers
and arena extractor bank belong to their existing wall cutaways.

The source contains no collision, navigation, lights, gameplay state, rig, or
animation. Godot owns movement restrictions, floor picking, route emission,
local lighting, and lettering. Openings do not introduce ballistic cover.
Review media stays ignored under `artifacts/art-expansion/`.
