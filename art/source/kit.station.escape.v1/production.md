# Station escape structure

Original dimensional Blender work derived from the approved station kit and
visual bible. Authorized by the owner on 2026-09-12, including the tactical-layout
revision. No external source or license was added.

Reproduce with Blender 5.2.0 LTS and `tools/blender/build_station_escape.py -- --replace`.
`tools/station-layout.json` owns bounds, rectangular pits, staggered three-metre
door openings, and passages; `tools/blender/station_tactical_geometry.py` is the
shared visible-deck helper. The builder leaves the apron east edge open for
departure. Gameplay wrappers and floor picking are owned by Godot.

| Output | Current technical result |
|---|---|
| Editable source | `art/source/kit.station.escape.v1/escape.blend`; 703,844 bytes |
| Publication | `game/Assets/Published/kit.station.escape.v1.glb`; 7,812,124 bytes |
| Geometry | 81,356 / 100,000 triangles; 54 meshes; four materials |
| Godot bounds | `(11.80, -1.92, -4.35)` to `(92.00, 2.60, 20.35)` |
| Fresh GLB reimport | Passed topology, bounds, materials, colors, and occluder IDs |
| Open-deck check | Passed 25 interior ray probes per pit across six openings |
| Current visual review | Blender and contextual Godot inspection passed; owner acceptance remains open |

Service has a continuous recessed coolant loop, security two offset scanner
wells, dock a broad lowered freight lift, and launch two service recesses with
below-deck conduits. All machinery remains below walking height. Caution coping
tops at 0.09 m and paint at 0.097 m stay inside the manifest's 0.40 m navigation
setback. These recesses restrict movement only and do not imply ballistic cover.

South-wall conduits, scanner controls, and the freight loading facade remain
joined to their owning far-wall occluders. Passage walls now use stable
`south`/`north` suffixes, and the apron adds two west-wall sections around its
entry. Broad launch-pad and loading marks keep lane choices visible; ordinary
floor paint is at most 0.04 m high. Review media remains ignored under
`artifacts/art-expansion/`; Godot review and owner acceptance are recorded
separately.
