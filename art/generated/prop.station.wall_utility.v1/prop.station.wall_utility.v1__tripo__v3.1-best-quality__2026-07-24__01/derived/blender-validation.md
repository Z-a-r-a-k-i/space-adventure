# Blender validation — station wall utility v1

Status: **PASS**; provisional pending owner visual review

- Asset: `prop.station.wall_utility.v1`
- Blender: `5.2.0 LTS`
- Raw SHA-256: `7D1B87029212C9DA8757DABBA7643B7811808F124FFEC7E80C4A7E546F969059`
- Derived GLB SHA-256: `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2`
- Active cleanup time: `27.853 s`
- Standardized render time: `3.375 s`

| Check | Result | Requirement |
|---|---|---|
| `bounds` | PASS | 1.20 x 0.22 x 0.80 m in Blender (±2%) |
| `pivot_and_mounting_plane` | PASS | bottom-center rear pivot at origin; Blender rear Y=0 (published rear Z=0) |
| `published_front_and_depth_sign` | PASS | published AABB Z=[-0.22,0], detailed geometry extends toward -Z from the rear mounting plane |
| `triangles` | PASS | ≤ 3000 |
| `mesh_objects` | PASS | 1–4 |
| `materials` | PASS | ≤ 3 |
| `texture_sets` | PASS | ≤ 1 |
| `texture_resolution` | PASS | ≤ 1024 |
| `uvs` | PASS | UV layer on every textured candidate mesh |
| `topology` | PASS | no physical boundary, non-manifold, wire, loose, or zero-area geometry after diagnostic weld of glTF attribute-seam duplicates |
| `no_rig_or_animation` | PASS | 0 armatures and 0 actions |
| `no_collision` | PASS | no generated collision objects |

## Rear reconstruction

The generated duplicate detailed rear and its blue baked artifact were removed with one planar depth cut. Blender closed the retained front with a new flat, untextured dark mounting face. No generated rear pixels remain in the derived candidate.

## Decision

Passed Blender hard gates for isolated Godot review; remains provisional pending owner visual review.
