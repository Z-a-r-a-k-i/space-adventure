# Station escape extension

Asset ID: `kit.station.escape.v1`. Owner: Codex; authorized by the owner's
2026-09-12 station expansion and existing-design selection.

Extend the accepted station structure family with service access, security
checkpoint, dock concourse, launch bay, short connecting passages, and a dock
apron. Keep the existing dimensional palette, beveled deck panels, modular
walls, sparse route lighting, and camera cutaway metadata. Service doors gate
the route; the evacuation airlock appears only before the dock apron.

Reference: the approved station structure kit and frontier-station visual bible.
The exterior cutter uses its separate brief. No new external source or license.

`tools/station-layout.json` owns room bounds, deck openings, and staggered
three-metre door/passages. `tools/blender/build_station_escape.py` and its shared
`station_tactical_geometry.py` helper author the visible geometry from it.
Maximum 100,000 triangles and four material slots. Editable Blender
source and exact fresh-imported GLB must pass the shared production validator.
Godot owns all invisible navigation, collision, interaction, and light wrappers.
Inspect the four areas in Godot at 7.5, 14.5, and 20 m, including door traversal,
wall cutaway, crowded combat readability, and the final view toward the cutter.

The owner's 2026-09-12 tactical-layout revision adds a wide coolant loop,
offset scanner wells, broad lowered freight lift, and two launch service
recesses. They are open deck cuts with visible machinery below walking height,
low caution coping, and floor marks that distinguish flanking lanes. They
restrict movement only; firearms may shoot across the openings. Keep machinery
below the deck and rims under 0.30 m, inside the manifest's 0.40 m clearance.
The service, scanner, and loading wall landmarks face inward from each room's
far south wall, joined into its existing cutaway mesh.
