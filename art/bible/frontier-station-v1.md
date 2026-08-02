# Frontier station visual bible

Status: approved POC visual baseline

Revision: 2026-08-02
Scope: `station-route-v1` and the approved POC roster

## Purpose and authority

This document translates the approved concept artwork and current greybox into
repeatable art constraints. It governs presentation only. `docs/PRODUCT.md`,
`docs/POC.md`, and `docs/DECISIONS.md` remain authoritative for product scope,
gameplay, character kits, and mechanics. The current Godot scene remains
authoritative for navigation, collision, interaction placement, and stable IDs
until an explicitly reviewed replacement is integrated.

The visual target is stylized, low-poly, industrial science fiction that stays
clear from an elevated tactical camera. It should feel authored and cohesive,
not like unrelated generated objects placed in the same room.

## Approved visual references

- [Station route key art](../concepts/frontier-station-v1/station-route-key-art.png)
  is the primary environment, lighting, modularity, terminal, and destination
  reference.
- [Tactical-pause combat](../concepts/frontier-station-v1/tactical-pause-combat.png)
  is the primary encounter-readability, cover, hostile-machine, telegraph, and
  effect-color reference.
- [POC crew lineup](../concepts/frontier-station-v1/poc-crew-lineup.png) is the
  primary character proportion, armor, material, and silhouette reference.
- [Approved Vanguard turnaround](../reference-sheets/frontier-station-v1/poc-models/vanguard-character-turnaround-v1.png)
  freezes Vanguard's face, body proportions, fixed outfit, palette, and broad
  silhouette. Its empty thigh carry hardware remains provisional until the
  approved carbine and character are fitted as a normalized 3D assembly.
- [Approved Vanguard carbine](../reference-sheets/frontier-station-v1/poc-models/vanguard-carbine-turnaround-v1.png),
  [Operator pistol](../reference-sheets/frontier-station-v1/poc-models/operator-pistol-turnaround-v1.png),
  and [Protector shotgun](../reference-sheets/frontier-station-v1/poc-models/protector-shotgun-turnaround-v1.png)
  freeze the three handheld-weapon directions. Grip, support-hand, carry, and
  muzzle interfaces remain subject to normalized 3D assembly checks.
- [Approved Operator turnaround](../reference-sheets/frontier-station-v1/poc-models/operator-character-turnaround-v1.png)
  and [Protector turnaround](../reference-sheets/frontier-station-v1/poc-models/protector-character-turnaround-v1.png)
  freeze their identities, proportions, fixed outfits, palettes, and broad
  silhouettes. The Protector has no shield.
- [Approved station survivor](../reference-sheets/frontier-station-v1/poc-models/station-survivor-turnaround-v1.png)
  freezes the NPC identity. The
  [approved Security Enforcer](../reference-sheets/frontier-station-v1/poc-models/security-enforcer-turnaround-v1.png)
  freezes the humanoid close-range hostile direction. The rigid gun-sentry
  reference remains pending.
- [Approved wall utility](../reference-sheets/frontier-station-v1/poc-models/station-wall-utility-turnaround-v1.png)
  and [field aid](../reference-sheets/frontier-station-v1/poc-models/field-aid-turnaround-v1.png)
  freeze their prop directions. The field aid uses selected alternative 2 from
  its exploration sheet.
- [Approved structure kit](../reference-sheets/frontier-station-v1/poc-models/station-structure-kit-reference-v1.png)
  and [evacuation airlock](../reference-sheets/frontier-station-v1/poc-models/evacuation-airlock-reference-v1.png)
  freeze environment visual direction only; Blender remains authoritative for
  dimensions, grid connections, parts, pivots, travel, and clearances.
- [POC UI and effects directions](../reference-sheets/frontier-station-v1/poc-ui/README.md)
  define the retained portrait, equipment-icon, tactical-marker, hostile-
  telegraph, combat/healing-effect, station-state, party-HUD, and authored-
  dialogue presentation language. They do not define gameplay state,
  targeting, range, timing, collision, damage, dialogue availability, or
  outcomes.
- [POC animation key-pose directions](../reference-sheets/frontier-station-v1/poc-animation/README.md)
  retain silhouette direction for the three humanoid party profiles. They do
  not approve a rig, clip timing, root motion, socket transform, hit shape,
  projectile, damage, or ability.

These images establish visual direction, not exact geometry or gameplay
contracts. The approved roster now assigns Vanguard's carbine, Operator's
pistol, and Protector's shotgun as presentation constraints. The depicted
shield, party arrangement, and effects do not assign or change any character
ability; the shield remains outside the approved roster. Character mechanics
remain as documented in the gameplay specifications.

## Visual pillars

1. **Readable at tactical distance.** A player must distinguish doors,
   interactables, threats, cover, and character roles without zooming in.
2. **Chunky modular construction.** Large panels, structural ribs, chamfered
   frames, exposed utility runs, and broad armor plates dominate the silhouette.
3. **Dark shell, purposeful color.** Most surfaces are restrained navy and
   neutral metal. Saturated color communicates route, interaction, destination,
   allegiance, or danger.
4. **Controlled detail density.** Each asset gets one dominant mass and a small
   number of medium details. Tiny greebles never carry gameplay meaning.
5. **Replaceable production inputs.** Shared dimensions, materials, pivots, and
   stable wrapper IDs matter more than preserving any generator's raw output.

## Tactical-camera constraints

The current implementation uses:

| Property | Current value |
|---|---:|
| Perspective field of view | 48 degrees |
| Camera distance | 7.5–20.0 m |
| Default distance | 14.5 m |
| Camera pitch | 0.45–1.15 radians |
| Default pitch | 0.90 radians |
| Reference review frame | 1280×720 |

Review every candidate at the default camera and at both zoom limits. A
gameplay-significant shape or color cue must survive the 20 m view. Do not rely
on text, thin wires, tiny lamps, surface scratches, or features smaller than
approximately 0.10 m for identification. Dominant silhouette breaks should
normally be at least 0.20 m.

## Environment module rules

The current route establishes the initial dimensional language:

| Element | Current value |
|---|---:|
| Authoring grid | 1.0 m, with 0.5 m trim subdivisions |
| Clear corridor width | 4.0 m |
| Floor slab thickness | 0.20 m |
| Main wall height | 2.60 m |
| Main wall thickness | 0.30 m |
| Junction post | 0.38 × 2.80 × 0.38 m |
| Retained wall-cutaway base | 0.45 m |
| Wall-cutaway transition | 0.15 seconds |

Structural floors, walls, doors, posts, and cutaway pieces are dimensionally
authored in Blender. Do not use generated meshes as structural modules. Tripo
may provide decorative candidates only under the current art pipeline; this
does not authorize live gameplay integration before the owning phase.

The production evacuation airlock visibly opens before completion presentation.
Its frame, moving leaves, clearance, pivot, and collision interfaces are
dimensionally authored. A generated candidate may contribute only a decorative
panel or insert that survives the normal cleanup and review pipeline.

Modules snap on the 1 m grid. Half-grid offsets are allowed for trim, attached
utilities, and set dressing, not for structural connections. Large wall panels
use a small number of deliberate seams. A repeated module may vary through
attached props, light state, decals, or damage masks without changing its
connection geometry.

Current camera occluders are world-up meshes that collapse to a 0.45 m base.
Any asset tested in the live route must preserve the existing occluder IDs and
must remain readable throughout that transition. Production walls may later
split upper and lower presentation, as described in `docs/ARCHITECTURE.md`.

## Shape language

### Frontier station

- Rectangular and chamfered primary forms.
- Thick structural ribs and capped junction posts.
- Recessed wall panels and floor plates with broad borders.
- Utility boxes, vents, tanks, and pipe bundles grouped into readable clusters.
- Corners and openings reinforced rather than razor thin.
- Repetition is intentional; asymmetry comes from set dressing and state.

### Crew

- Human proportions with mild heroic exaggeration.
- Broad boots, gloves, shoulder forms, and readable equipment silhouettes.
- Layered dark fabric beneath light neutral armor plates.
- Each human has one fixed complete outfit for the POC. The editable source
  uses a fitted navy technical undersuit with clearly separable major armor
  pieces; those seams support production and possible future whole-outfit
  variants, not current equipment slots.
- Hidden undersuit geometry may be masked or omitted under rigid armor to avoid
  clipping. A literal complete unclothed body is unnecessary.
- Cyan identity and equipment accents used sparingly.
- Faces and role-specific silhouettes matter more than small costume detail.
- Vanguard reads through a broad two-handed carbine, Operator through a compact
  pistol and lighter technical silhouette, and Protector through a broad
  two-handed shotgun. These are separate weapon assets.
- Fixed handheld weapons remain separate assets, with plausible hand and
  holster attachment. They are holstered during exploration; their draw, ready,
  and firing silhouettes must read from above.
- Firearms use broad, retro-industrial masses and restrained high-tech accents;
  thin barrels and tiny attachments must not carry role readability.

### Hostile security profiles

- The Security Enforcer is a human-proportioned, Mixamo-compatible, non-sapient
  android with a sealed head, continuous undersuit, fixed armor, and reinforced
  forearm body attack.
- The gun sentry is a stationary rigid assembly with no skeletal deformation.
- Compact angular armor and one unmistakable red or red-orange threat focus.
- Silhouette, attack direction, and telegraph remain clear from above.
- The Enforcer exposes one reinforced forearm contact surface and uses only the
  humanoid idle, locomotion, strike, recovery, hit, and down coverage required
  by its brief.
- The sentry exposes one aim pivot, recoil axis, and muzzle.
- The sentry has no legs, walk cycle, skeletal deformation, or complex
  mechanical rig.

Faction names, logos, serial text, and final lore markings remain provisional.
Do not bake them into meshes or textures during this spike.

## Palette and state semantics

The current greybox colors are anchors, not a requirement to reproduce every
literal in a texture:

| Provisional material ID | Role | Current anchor |
|---|---|---|
| `mat.station.background` | Void/background | `#05070B` |
| `mat.station.floor.dark` | Floor shell | `#111B26` |
| `mat.station.wall.dark` | Wall shell | `#1F2B3B` |
| `mat.station.trim.cyan` | Route and utility trim | `#0F85AD` |
| `mat.crew.accent.cyan` | Player/selection identity | `#14A8E0` |
| `mat.state.caution.amber` | Survivor, emergency, caution | `#F58F29` |
| `mat.state.optional.violet` | Optional inspectable terminal | `#853DE0` |
| `mat.state.destination.green` | Destination and completion | `#29C76E` |
| `mat.state.threat.red` | Hostile sensor and telegraph | Defined in-engine |

Color semantics take precedence over concept-art decoration. Purple must not
read as the destination, green must not mark an optional prop, and hostile red
must not be reused as neutral station trim. Never make color the only signal:
pair it with silhouette, placement, iconography, or animation.

## Materials, bevels, and surface detail

- Prefer a controlled shared palette and reusable trim/material families over a
  unique texture set for every prop.
- Large station surfaces are moderately metallic and matte. The current floor
  and wall anchors use metallic values around 0.28–0.35 and roughness around
  0.68–0.74.
- Structural hard edges use visible bevels, normally 0.03–0.08 m with one or
  two segments and stable weighted normals. Small props normally use
  0.01–0.04 m bevels.
- Emission belongs on screens, route strips, sensors, and purposeful lamps.
  Bloom, holograms, shield surfaces, warning cones, and selection rings are
  authored in Godot rather than baked into base color.
- Generated base color must be de-lit. Reject baked highlights, cast shadows,
  ambient glow, camera-facing gradients, and fake depth.
- Use one large form, two to five medium forms, and restrained small detail.
  Avoid uniform panel noise and randomly scattered bolts.
- The frontier station may show light edge wear and maintenance history.
  Heavy grime, corrosion, gore, and catastrophic damage are outside this
  visual spike.

## Generated-asset policy

- Follow `docs/ART-PIPELINE.md`; providers are inputs, not production
  authority.
- Generate isolated assets, never a complete room or encounter as one mesh.
- Humanoids, including the Security Enforcer, use the approved T-pose, Tripo
  Quad retopology, Mixamo, and Blender sequence. The gun sentry remains a simple
  rigid assembly.
- Normalize scale, pivot, axes, topology, UVs, materials, and naming in Blender
  before Godot review.
- Preserve untouched provider exports in the ignored workstation cache and
  version only concise provenance and structural metrics.

## Approval bar

A POC asset is acceptable only when:

- its role reads at the default camera and at 20 m;
- it respects the palette semantics and neighboring detail density;
- its dimensions, pivot, forward direction, and material slots match its brief;
- a combatant satisfies `docs/ATTACK-PRESENTATION.md` through its complete
  character-and-weapon or machine assembly;
- it survives Blender and Godot review without malformed geometry or import
  warnings;
- its provenance and commercial-use status are recorded; and
- prominent assets receive explicit human visual approval.
