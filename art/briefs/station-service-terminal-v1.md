# Asset brief — station service terminal v1

Status: generator-bake-off brief; not approved production art

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `prop.station.service_terminal.v1` |
| Category | Environment prop / optional interactable presentation |
| Gameplay wrapper | `interaction.service_terminal` |
| Scenario | `scenario.station_route` |
| Intended role | Clearly optional station terminal that can be inspected |
| Primary reference | `art/concepts/frontier-station-v1/station-route-key-art.png` |
| Approved turnaround | `art/reference-sheets/frontier-station-v1/station-service-terminal-turnaround-v1.png` |
| Visual bible | `art/bible/frontier-station-v1.md` |

The asset is presentation inside the existing Godot interaction wrapper. It
does not own the gameplay stable ID, prompt, use radius, approach point,
collision, or interaction effect.

The turnaround was human-approved on 2026-07-23 for lossless input packaging.
External provider upload and credit use remain separate actions.

## Bounds and coordinates

| Property | Requirement |
|---|---|
| Visual envelope | 0.80 m wide × 1.30 m high × 0.42 m deep |
| Scale tolerance after normalization | ±2% |
| Published up | `+Y` |
| Published front | `-Z`, toward the current approach point |
| Pivot | Ground-plane center beneath the visual body |
| Ground contact | Lowest intended support at `Y = 0` |
| Transform | Applied scale and rotation in the editable source |

The current primitive is centered 0.70 m behind its interaction root and the
approach lies toward its inferred `-Z` face. Facing is therefore an art
publication contract, not an existing gameplay field.

The current Godot click envelope is deliberately larger than the visible
primitive: 1.80 × 2.20 × 1.40 m. Preserve the authored wrapper and collider
during this experiment rather than deriving collision from the new mesh.

## Silhouette and construction

- Freestanding, stable pedestal with a broad base.
- One raised or angled display hood that reads from above.
- A clear front face and one dominant screen region.
- Two or three large armor/panel breaks rather than dense console controls.
- Thick enough edges and supports to avoid paper-thin geometry.
- Mild asymmetry is allowed in a side module or maintenance access panel.
- It must remain identifiable without reading a label.

The design may borrow the chunky purple terminal silhouette from the route key
art, but it must fit the current envelope rather than reproduce the painting
literally.

## Materials

Required provisional material regions:

1. `mat.station.wall.dark` — primary housing.
2. `mat.station.trim.cyan` or a neutral metal trim — restrained structural
   separation only.
3. `mat.state.optional.violet` — screen and interaction accent.

The violet accent is mandatory and must remain visually dominant over cyan.
Emission is limited to the screen and one or two status strips. Do not include
legible text, logos, baked UI, baked bloom, cast shadows, or green destination
lighting in textures.

Prefer shared palette materials. A generated-texture candidate may use at most
one 1024×1024 base-color, normal, and packed roughness/metallic/occlusion set for
evaluation, but acceptance may replace it with shared materials.

## Budgets

| Budget | Target | Hard limit |
|---|---:|---:|
| Triangles | 1,500–2,500 | 4,000 |
| Mesh objects | 1–3 | 4 |
| Material slots | 2–3 | 3 |
| Unique texture sets | 0–1 | 1 |
| Maximum texture resolution | 1024×1024 | 1024×1024 |
| Bones / animations | 0 | 0 |

No collision mesh, armature, animation, light, camera, label, or gameplay
metadata belongs in the published model.

## Tactical readability

At 14.5 m and 20 m:

- the pedestal, display hood, and violet screen must form one clear silhouette;
- the front must remain distinguishable from the back;
- violet must read as optional/interactable without resembling the green
  airlock destination; and
- small buttons or surface lines may disappear without losing the asset's role.

Review at 7.5 m for distracting topology, texture seams, and excessive detail.

## Forbidden traits

- Photorealistic consumer computer, arcade cabinet, or contemporary kiosk.
- Floating hologram as part of the base mesh.
- Keyboards, cables, antennae, or controls extending outside the envelope.
- Random greeble noise, unreadable glyphs, logos, or faction names.
- Green-dominant lighting, hostile red sensors, or crew-selection cyan as the
  main state cue.
- Baked highlights, ambient glow, grime gradients, or camera-facing shadows.

## Deliverables and review

- Raw provider output and provenance record.
- Editable normalized Blender source.
- Published GLB candidate.
- GLB validation report.
- Blender front/back/side/three-quarter/wireframe views.
- Godot captures at 7.5 m, 14.5 m, and 20 m.
- Triangle, mesh, material, texture, file-size, generation-time, and cleanup-time
  measurements.

The candidate passes only through the bake-off experiment. Replacing the live
terminal requires a separate reviewed integration change.
