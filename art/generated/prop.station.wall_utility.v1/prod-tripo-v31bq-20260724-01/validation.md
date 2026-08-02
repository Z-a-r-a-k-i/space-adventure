# Station wall utility validation

Status: PASS; project-owner visual approval pending

| Check | Result |
|---|---|
| Exported envelope | 1.20 × 0.80 × 0.22 m |
| Orientation | `+Y` up, visible front toward `-Z` |
| Pivot | Bottom center of flat rear mounting plane at `Z = 0` |
| Geometry | 2,764 triangles, one mesh |
| Materials | Two materials, one 1024² texture set |
| Topology | Closed manifold after cleanup and diagnostic attribute-seam weld |
| Rig and animation | None |
| Collision | None generated |
| Godot review | Readable in isolated review at 7.5 m, 14.5 m, and 20 m |

The Blender cleanup removed the duplicated generated rear and its baked-color
artifact, then closed the asset with a flat dark mounting face. The detailed
front remains on the negative-Z side and no geometry extends behind the
mounting plane.

This validation approves the retained file as a technically viable static
candidate. It does not authorize replacement of the live Phase 2 greybox.
