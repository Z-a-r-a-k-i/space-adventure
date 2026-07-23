# Asset brief — security drone body v1

Status: generator-bake-off brief; concept-only machine presentation

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `machine.security_drone.body.v1` |
| Category | Hostile-machine body candidate |
| Gameplay wrapper | None accepted yet |
| Intended role | Compact malfunctioning security-machine body for visual and topology evaluation |
| Primary reference | `art/concepts/frontier-station-v1/tactical-pause-combat.png` |
| Visual bible | `art/bible/frontier-station-v1.md` |

The POC establishes a mandatory group of non-sapient malfunctioning security
machines, but it does not yet establish a drone asset, dimensions, behavior
assignment, rig, or stable runtime ID. This brief must not invent those
gameplay contracts.

The combat concept shows both a tall machine and squat machines. This bake-off
targets the central armored body of a squat ground variant. Locomotion parts,
rigging, animation, weapons, and behavior remain outside the experiment.

This isolated body is an explicit exception to the production combatant gate
in `docs/ATTACK-PRESENTATION.md`. Any later complete security machine receives
a new assembly asset ID and brief that chooses a `handheld`, `integrated`, or
`body` attack source before rigging or animation. Do not add speculative weapon
or contact markers to this body-only candidate.

The approved POC roster reserves `machine.security.ram_drone.v1` for a new
complete body-source machine. That reservation does not promote or assign this
isolated bake-off body to the production enemy.

## Bounds and coordinates

Because no runtime drone exists, these are provisional benchmark dimensions:

| Property | Requirement |
|---|---|
| Body envelope | 0.75 m wide × 0.35 m high × 0.90 m long |
| Scale tolerance after normalization | ±2% |
| Published up | `+Y` |
| Published front | `-Z`, aligned with the main sensor |
| Pivot | Ground-plane center below the body's center of mass |
| Body clearance | Lowest body surface at least 0.15 m above `Y = 0` |
| Transform | Applied scale and rotation in the editable source |

The full future machine may exceed this envelope after locomotion is added.
This body must remain compact enough for a later assembled footprint around
1.0 m wide and 1.1 m long.

## Silhouette and construction

- Low, forward-weighted armored body with a clear front.
- One unmistakable main sensor housing.
- A broad central shell plus at most two side housings.
- Four robust locomotion attachment regions placed symmetrically.
- Chamfered hard-surface construction consistent with station technology but
  more aggressive in stance.
- Front/back distinction visible from above without relying only on color.

Required authoring markers in the editable Blender source:

- `socket.locomotion.front_left`
- `socket.locomotion.front_right`
- `socket.locomotion.rear_left`
- `socket.locomotion.rear_right`
- `socket.sensor.main`

The bake-off GLB may represent these as named empties/nodes. They do not define
gameplay hardpoints or require a generalized socket system.

## Materials

Required provisional material regions:

1. `mat.station.wall.dark` or a darker related armor shell.
2. Neutral worn metal for edge and mechanism separation.
3. `mat.state.threat.red` for the main sensor only.

The red sensor must remain readable at tactical distance and must not be baked
as a large glow across the armor. Additional glow, telegraph cones, selection
rings, damage flashes, and status effects are authored in Godot.

Prefer shared palette materials. A generated-texture candidate may use at most
one 1024×1024 base-color, normal, and packed roughness/metallic/occlusion set for
evaluation.

## Budgets

| Budget | Target | Hard limit |
|---|---:|---:|
| Triangles | 1,500–3,000 | 5,000 |
| Mesh objects | 1–4 | 6 |
| Material slots | 2–3 | 4 |
| Unique texture sets | 0–1 | 1 |
| Maximum texture resolution | 1024×1024 | 1024×1024 |
| Bones / animations | 0 | 0 |

No generated rig, skin weights, animation, collider, weapon, projectile,
particle system, or gameplay metadata is accepted in this body candidate.

## Tactical readability

At 14.5 m and 20 m:

- the low armored mass, front direction, and red sensor must remain legible;
- the machine must not be confused with a crate or neutral wall utility;
- major attachment regions should imply plausible locomotion without thin
  protrusions; and
- the threat cue must remain clear against the dark station floor.

Review at 7.5 m for asymmetry, melted panel boundaries, thin shells, fused
attachment regions, unusable normals, and UV distortion.

## Forbidden traits

- Humanoid face, eyes, mouth, or friendly character expression.
- Built-in weapon not present in the reference brief.
- Complete generated legs, tracks, wheels, rig, or animation.
- Fragile antennae, fins, wires, or needle-like supports.
- Purple optional-interaction or green destination lighting.
- Logos, serial text, faction names, military insignia, or invented lore.
- Heavy battle damage, gore, rust, baked muzzle flash, or baked sensor bloom.
- Soft organic sculpting that obscures manufactured panel boundaries.

## Deliverables and review

- Raw provider output and provenance record.
- Editable normalized Blender source with named socket markers.
- Published GLB candidate.
- GLB validation report.
- Blender front/back/side/top/three-quarter/wireframe views.
- Godot captures on the station floor at 7.5 m, 14.5 m, and 20 m.
- Triangle, mesh, material, texture, file-size, generation-time, and cleanup-time
  measurements.

This body cannot enter the live game until the combat milestone defines its
presentation wrapper, locomotion, collider, behavior, telegraphs, and stable
runtime identity, and its complete assembly passes the attack-presentation
contract.
