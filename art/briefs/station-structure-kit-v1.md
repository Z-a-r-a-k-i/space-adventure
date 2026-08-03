# Asset brief — station structure kit v1

Status: accepted by the project owner for Phase 3 production on 2026-08-02;
integrated candidate awaiting final owner visual approval

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `kit.station.structure.v1` |
| Role | Exact-route floors, cutaway walls, junction framing, and route strips |
| Gameplay wrapper | `StationRoute/Environment` |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/station-structure-kit-reference-v1.png` |
| Footprint | Match the authored station-route navigation and collision layout |
| Axes | `+Y` up, `-Z` front |
| Pivot | Scenario origin on the finished floor plane |
| Budget | 12,000 triangles and 4 material slots maximum |

The authored Godot scene owns navigation, collision, lights, interaction
identity, and camera behavior. The GLB is static presentation geometry only.

## Visual requirements

- Chunky modular frontier-station panels with broad structural trim.
- The north-south corridor, east service branch, and evacuation threshold must
  read immediately from the tactical camera.
- Cyan strips identify traversal without becoming gameplay authority.
- Named wall meshes remain independently addressable for camera cutaway.
- No baked lighting, generated collision, rig, animation, legible labels, or
  dense decorative greebles.

## Production gate

Author dimensionally in Blender, export one exact GLB, verify axes and named
wall parts in Blender, then inspect the same publication in the live Godot
route. Retain authored greybox collision and navigation independently.
