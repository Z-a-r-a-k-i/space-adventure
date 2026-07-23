# Asset brief — station wall utility v1

Status: generator-bake-off brief; not approved production art

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `prop.station.wall_utility.v1` |
| Category | Environment prop / non-interactive wall dressing |
| Gameplay wrapper | None |
| Intended role | Break up repeated station wall panels with readable utility infrastructure |
| Primary reference | `art/concepts/frontier-station-v1/station-route-key-art.png` |
| Secondary reference | `art/concepts/frontier-station-v1/tactical-pause-combat.png` |
| Visual bible | `art/bible/frontier-station-v1.md` |

This is a benchmark prop, not a structural wall module. It must attach to an
authored wall without changing navigation, collision, cutaway bounds, or wall
stable IDs.

## Bounds and coordinates

The greybox does not currently define this prop. The following envelope is the
experiment target:

| Property | Requirement |
|---|---|
| Visual envelope | 1.20 m wide × 0.80 m high × 0.22 m deep |
| Scale tolerance after normalization | ±2% |
| Published up | `+Y` |
| Published front | `-Z`, away from the mounting wall |
| Pivot | Bottom center of the rear mounting plane |
| Mounting plane | `Z = 0` |
| Transform | Applied scale and rotation in the editable source |

Visible geometry extends toward local `-Z`. The rear must remain sufficiently
flat to mount without intersections. A small controlled recess is acceptable;
no component may extend behind the mounting plane.

## Silhouette and construction

- One strong rectangular or chamfered mounting enclosure.
- One readable vent, grille, junction box, tank, or pipe-manifold focus.
- Two or three thick utility runs rather than many thin cables.
- Broad clamps and reinforced connections.
- Enough negative space to distinguish the components from above.
- A deliberately manufactured object that can repeat along the station.

The candidate should complement a 1 m modular wall grid. It may span more than
one panel visually, but its mounting envelope remains independent of structural
seams.

## Materials

Required provisional material regions:

1. `mat.station.wall.dark` — primary enclosure.
2. Neutral worn metal — pipe, grille, clamp, or edge separation.
3. `mat.station.trim.cyan` — at most one small utility-status strip.

This prop is non-interactive. Violet, green, and hostile red state colors are
forbidden. Emission is optional and must remain subordinate to route and
interaction cues.

Prefer shared palette materials. A generated-texture candidate may use at most
one 1024×1024 base-color, normal, and packed roughness/metallic/occlusion set for
evaluation.

## Budgets

| Budget | Target | Hard limit |
|---|---:|---:|
| Triangles | 800–1,500 | 3,000 |
| Mesh objects | 1–3 | 4 |
| Material slots | 1–3 | 3 |
| Unique texture sets | 0–1 | 1 |
| Maximum texture resolution | 1024×1024 | 1024×1024 |
| Bones / animations | 0 | 0 |

No collider is required for the bake-off. If a later placement needs click or
movement blocking, it will use an authored simple Godot shape rather than
generated mesh collision.

## Tactical readability

At 14.5 m and 20 m:

- the enclosure and main utility feature must read as two distinct masses;
- the asset must not look like an interactable terminal, door control, or
  hostile device;
- pipes and clamps must remain visible as broad forms; and
- disappearance of bolts, grille slots, and surface wear must not harm the
  composition.

Review at 7.5 m for intersections, unsupported pieces, noisy normals, and
repetition-unfriendly details.

## Forbidden traits

- Structural wall, doorway, or floor geometry.
- Loose wires, dangling hoses, thin antennae, or paper-thin grilles.
- Human-readable labels, logos, warning text, or invented faction marks.
- Purple, green, or red state lighting.
- Dense sci-fi noise distributed evenly across every surface.
- Heavy rust, leaks, gore, or catastrophic damage.
- Baked room lighting, contact shadows, or camera-facing gradients.

## Deliverables and review

- Raw provider output and provenance record.
- Editable normalized Blender source.
- Published GLB candidate.
- GLB validation report.
- Blender front/back/side/three-quarter/wireframe views.
- Godot captures mounted on a representative 2.60 m wall at 7.5 m, 14.5 m, and
  20 m.
- Triangle, mesh, material, texture, file-size, generation-time, and cleanup-time
  measurements.

The experiment must not add this prop to the live station route or alter a
camera-occluder mesh.

