# Asset brief — station structure kit v2

Status: accepted by the project owner for PR 14 production on 2026-08-04

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `kit.station.structure.v2` |
| Role | Production presentation for the complete serpentine station route |
| Gameplay wrapper | `StationRoute/Environment` |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/station-structure-kit-reference-v1.png` |
| Footprint | Five authored areas on the 1 m grid defined by station route revision `station-route-v5` |
| Axes | `+Y` up, `-Z` front |
| Pivot | Scenario origin on the finished floor plane |
| Budget | 30,000 triangles and 4 material slots maximum |

The static GLB contains presentation geometry only. Godot owns navigation,
collision, lights, interaction identity, door state, and camera behavior.

## Required parts

- Individually named start-room, solo-arena, Protector-room, main-arena, and
  final-approach floors.
- Individually named wall segments with unique stable `occluder_id` metadata.
- Sparse cyan route strips that make the serpentine critical path readable.
- Openings sized for the entry service door, solo-exit service door, and final
  evacuation airlock at the approved route coordinates.
- Broad manufactured panels, warm-gray caps, restrained bevels, and no dense
  greeble field.

## Production gate

Author dimensionally in Blender 5.2, verify the footprint, origin, axes,
material count, triangle count, wall metadata, and fresh reimport of the exact
GLB. Inspect the same publication in Godot at 14.5 m and 20 m. Only after this
revision passes may the superseded v1 source, brief, record, and publication be
removed.
