# Asset brief — station structure kit v2

Status: accepted by the project owner for PR 14 production on 2026-08-04

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `kit.station.structure.v2` |
| Role | Production presentation for the five original station areas |
| Gameplay wrapper | `StationRoute/Environment` |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/station-structure-kit-reference-v1.png` |
| Footprint | Original outer rooms and walls retained; solo and party inner decks follow `tools/station-layout.json` |
| Axes | `+Y` up, `-Z` front |
| Pivot | Scenario origin on the finished floor plane |
| Budget | 36,000 triangles and 4 material slots maximum |

The static GLB contains presentation geometry only. Godot owns navigation,
collision, lights, interaction identity, door state, and camera behavior.
The connected four-fight extension and boarding apron use the separate
`kit.station.escape.v1` publication. The owner's 2026-09-12 tactical-layout
revision authorizes the original solo damaged-deck opening and party power
trench; their recess lining and machinery justify the 6,000-triangle allowance
increase. Recesses restrict movement only; firearms may shoot across them.

## Required parts

- Individually named start-room, solo-arena, Protector-room, main-arena, and
  final-approach floors.
- Individually named wall segments with unique stable `occluder_id` metadata.
- Sparse cyan route strips that make the serpentine critical path readable.
- Openings sized for the entry service door, solo-exit service door, and final
  evacuation airlock at the approved route coordinates.
- Broad manufactured panels, warm-gray caps, restrained bevels, and no dense
  greeble field.
- Open rectangular deck cuts matching the shared layout, with damaged power
  trunks in solo and a central conduit trench in party. Machinery stays below
  the deck; hazard coping is below 0.30 m and inside navigation clearance.
- Route strips turn around the openings and keep both flanking lanes legible.

## Production gate

Author dimensionally in Blender 5.2, verify the footprint, origin, axes,
material count, triangle count, wall metadata, and fresh reimport of the exact
GLB. Inspect the same publication in Godot at 14.5 m and 20 m. Only after this
revision passes should it replace the prior inner-deck presentation. Preserve
all original wall geometry and `occluder_id` metadata during this revision.
