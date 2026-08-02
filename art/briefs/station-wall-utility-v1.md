# Asset brief — station wall utility v1

Status: technically validated candidate retained; owner approval and Phase 5
integration pending

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `prop.station.wall_utility.v1` |
| Role | Repeating noninteractive wall dressing |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/station-wall-utility-turnaround-v1.png` |
| Visual envelope | 1.20 m wide × 0.80 m high × 0.22 m deep |
| Axes | `+Y` up; visible geometry extends toward local `-Z` |
| Pivot | Bottom center of the rear mounting plane at `Z = 0` |
| Budget | 3,000 triangles, 3 material slots, one 1024² texture set maximum |

This is a static prop, not a structural wall, collider, interactable, rig, or
animation source.

## Visual requirements

- One strong enclosure and one readable vent, tank, grille, or pipe-manifold
  focus.
- Broad clamps and two or three thick utility runs; no thin loose cables.
- Dark station shell, neutral metal, and at most one subordinate cyan strip.
- The mounting back stays flat and the role remains readable at 14.5 m and
  20 m without resembling a terminal or hostile device.
- No violet, green, hostile red, legible labels, dense greebles, heavy damage,
  baked lighting, or generated collision.

## Retained candidate

Tripo candidate 01 and its Blender reconstruction passed the static geometry,
material, orientation, mounting-plane, and isolated Godot readability checks.
The compact active record is under
`art/generated/prop.station.wall_utility.v1/prod-tripo-v31bq-20260724-01/`.

Keep that source and exact reviewed GLB. Do not send this asset through the
humanoid Quad-10k, Mixamo, skinning, or animation pipeline, and do not
regenerate it merely because character production changes. The project owner
must still approve the candidate visually before Phase 5 can replace any live
greybox wall dressing.
