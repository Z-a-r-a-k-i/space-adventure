# Asset brief — station service door v1

Status: accepted by the project owner for PR 14 production on 2026-08-04

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `assembly.station.service_door.v1` |
| Role | Reusable ordinary rigid boundary door around the solo-combat arena |
| Visual family | Derived dimensionally from the approved station structure kit |
| Bounds | 3.00 m wide × 2.65 m high × 0.35 m deep |
| Axes | `+Y` up, `-Z` front |
| Pivot | Center of the threshold on the ground plane |
| Budget | 4,000 triangles and 3 material slots maximum |

The assembly must expose exactly the independently addressable top-level mesh
parts `Frame`, `Door_Left`, `Door_Right`, `Status_Strip`, and `Control_Panel`.
Both leaves slide horizontally and remain rigid. The GLB owns no collision,
navigation, gameplay state, rig, or animation.

## State presentation

- Locked/default: restrained amber status strip.
- Open/route: cyan status strip through a Godot material override.
- State remains readable through the central seam, leaf travel, and label; it
  never relies on color alone.
- The control panel stays subordinate and contains no legible baked text.

## Production gate

Verify exact bounds, ground pivot, part names, axes, three-material limit,
triangle budget, clear leaf travel, and a fresh Blender reimport of the exact
GLB. Reuse one publication for the entry and solo-exit instances and inspect
both in the live Godot route.
