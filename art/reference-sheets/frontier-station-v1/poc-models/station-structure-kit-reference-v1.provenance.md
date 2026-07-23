# Station structure kit reference v1 — provenance

Status: approved structure-kit visual reference

Asset ID: `kit.station.structure.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

## Output

- File: `station-structure-kit-reference-v1.png`
- Dimensions: 1672 × 941
- SHA-256:
  `f518b123fc6b2f67dda71cb846cad76aa334617c72bfec5e52f4fd817e4e3738`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This is a modular visual-direction sheet, not generated structural geometry.
Blender owns the exact grid, dimensions, connections, pivots, and editable
source.

## Input image roles

1. `../../../concepts/frontier-station-v1/station-route-key-art.png` (SHA-256:
   `a540d65afb6144030c1177478a0f2b4653146f76ef4eb9a4daf65fbc7e7294b1`) —
   authoritative environment shape language, palette, modularity, route
   lighting, wall, floor, post, and cutaway context.
2. `../../../concepts/frontier-station-v1/tactical-pause-combat.png` (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   encounter-space structural and material language only.

## Generation prompt

```text
Use case: stylized-concept
Asset type: modular environment-kit reference sheet for dimensionally authored Blender modeling, `kit.station.structure.v1`
Primary request: Create a brand-new model-ready visual-direction sheet for the frontier station's reusable structural kit. Image 1 is the authoritative environment shape-language, palette, modularity, route-lighting, wall, floor, post, and cutaway-context reference; do not reproduce the whole room. Image 2 is the encounter-space structural and material reference; do not reproduce combatants, props, cover layout, telegraphs, or effects. This sheet defines appearance only—Blender will author exact dimensions, pivots, grid snapping, collision, and cutaway behavior.
Scene/backdrop: perfectly clean pale cool-gray studio sheet with a spacious upper module lineup and one isolated assembled cutaway example below; no cinematic room, void background, text, labels, numbers, dimensions, arrows, blueprint grid, UI, or people
Subject: one coherent dark frontier-station structural family containing exactly six reusable visual module types at consistent scale and construction: (1) a 1 m square floor slab with 0.20 m thickness, broad recessed center plate and restrained edge border; (2) a 1 m wide × 0.45 m high retained lower-wall/base module with thick capped top edge; (3) a separate 1 m wide upper/cutaway wall module that combines with the lower base to reach a 2.60 m wall, using two broad recessed panels and a reinforced top rail; (4) a chunky 0.38 m square × 2.80 m tall junction/end post with chamfered cap and strong vertical ribs; (5) a thin replaceable 1 m route/emissive-strip insert housed in a protected dark channel; (6) a compact wall-or-ceiling light fixture with thick protective hood. Dark navy/charcoal matte station shell, warm-gray structural caps, restrained worn metal edges, very sparse cyan utility/route strips. One dominant mass and two to five medium forms per module; broad manufactured seams, no tiny greeble field.
Assembled example: below the module lineup, show a small clean front-right isometric L-shaped corridor corner assembled only from those exact modules: three floor slabs, retained lower walls visible continuously, two upper wall pieces installed on one side and intentionally absent on the camera-facing side to demonstrate the cutaway, one junction post, one route-strip insert, and one wall light. It is a construction example on the same pale studio background, not a gameplay room.
Style/medium: polished stylized low-poly 3D hard-surface concept render, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; authored modular-game-kit realism, controlled 0.03–0.08 m-looking bevels, weighted-normal appearance, broad tactical-camera readability, consistent with the supplied project art
Composition/framing: top section presents all six module types as separate isolated objects, evenly spaced and fully visible, using near-orthographic front or three-quarter views chosen to reveal thickness; bottom section presents the assembled L-shaped cutaway example. Preserve identical material language and repeated seam/rib motifs across separate pieces and assembly. No duplicate variants beyond pieces needed in the assembly.
Lighting/mood: even neutral studio illumination designed to reveal thickness, connections, panel depth, and materials; minimal soft grounding only; no dramatic glow, fog, baked room lighting, cast shadows obscuring joints, or atmospheric effects
Color palette: dark navy/charcoal primary shell, warm-gray structural caps, restrained neutral metal, small cyan route/utility light only; no optional violet, destination green, hostile red, or dominant amber
Constraints: structural kit only; clear split between retained lower wall and removable upper wall; thick manufacturable parts; modules must visually snap on a simple grid; floor, lower wall, upper wall, post, route strip, and light must be distinguishable without text; no door, airlock, terminal, pipes, freestanding prop, cover crate, furniture, character, weapon, collision visualization, navigation surface, logo, lettering, numbers, watermark, damage, heavy rust, floating parts, or random greebles
Avoid: complete cinematic environment, organic architecture, smooth white spaceship, gothic cathedral, industrial realism with dense cables, paper-thin walls, irregular non-modular shapes, photorealistic grime, isometric game screenshot, exploded technical diagram with labels
```

## Review boundary

Review the shared visual language, six module types, retained-lower versus
removable-upper wall read, grid-family coherence, and assembled cutaway example.
Approval would not freeze diffusion-inferred dimensions or connections. The
production source must use the dimensional contracts in the visual bible and
be authored directly in Blender.
