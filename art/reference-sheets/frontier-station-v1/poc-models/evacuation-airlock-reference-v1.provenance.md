# Evacuation airlock reference v1 — provenance

Status: approved airlock visual reference

Asset ID: `assembly.station.evacuation_airlock.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

## Output

- File: `evacuation-airlock-reference-v1.png`
- Dimensions: 1672 × 941
- SHA-256:
  `27dca5bb57d4087188feb3423efad4b5288623c8301a06d64f4f92754baabd3b`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This is an assembly visual-direction sheet, not generated structural geometry
or an animation-authoritative mechanism. Blender owns exact dimensions, parts,
pivots, travel, and clearance.

## Input image roles

1. `../../../concepts/frontier-station-v1/station-route-key-art.png` (SHA-256:
   `a540d65afb6144030c1177478a0f2b4653146f76ef4eb9a4daf65fbc7e7294b1`) —
   destination-door context, station palette, shape language, and green
   completion-state semantics.
2. `station-structure-kit-reference-v1.png` (SHA-256:
   `f518b123fc6b2f67dda71cb846cad76aa334617c72bfec5e52f4fd817e4e3738`) —
   proposed station structure-kit family, navy panels, warm-gray caps, bevels,
   wall thickness, and modular construction.

## Generation prompt

```text
Use case: stylized-concept
Asset type: evacuation-airlock assembly reference sheet for dimensionally authored Blender modeling and animation, `assembly.station.evacuation_airlock.v1`
Primary request: Create a brand-new model-ready visual-direction sheet for the frontier station's final evacuation airlock. Image 1 is the authoritative destination-door, station palette, shape-language, and green completion-state reference; isolate and refine the far-right green door into a coherent opening assembly without reproducing the room. Image 2 is the approved-in-direction station structure-kit family reference: match its navy panels, warm-gray caps, bevels, wall thickness, and modular construction. This sheet defines appearance and part relationships only—Blender will author exact dimensions, pivots, travel, clearance, collision, and attachment markers.
Scene/backdrop: perfectly clean pale cool-gray studio sheet divided into four spacious panels; no cinematic room, wall extension, floor scenery, text, labels, dimensions, arrows, blueprint grid, UI, or characters
Subject: one heavy retro-industrial frontier-station evacuation airlock assembly, roughly 3.0 m wide × 2.8 m high × 0.45 m deep before dimensional finalization. A thick chamfered dark navy frame with broad warm-gray corner caps and reinforced side pockets surrounds a clear human-scale opening. Two rigid armored door leaves meet at a strong central vertical seam when closed and slide horizontally into the left and right frame pockets when open. Each leaf has two broad recessed panels and a restrained central geometric locking relief that splits cleanly across the seam; no circular vault wheel. A compact separate control/status panel mounts on the right frame at hand height. Protected green destination/status strips sit above and beside the opening and are engine-controlled rather than painted glow. Thick manufacturable edges, simple tracks concealed within the frame, clear open-state clearance, one dominant frame mass and restrained panel detail.
Style/medium: polished stylized low-poly 3D hard-surface concept render, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; authored modular-game-assembly realism, controlled bevels, weighted-normal appearance, broad tactical-camera readability, consistent with both supplied project references
Composition/framing: four-panel production sheet showing the exact same assembly and materials: top-left strict front closed state with both leaves meeting; top-right strict front fully open state with leaves visibly retracted into side pockets and the opening completely clear; bottom-left right-side cutaway/three-quarter technical view revealing frame depth, leaf thickness, pocket clearance, and horizontal travel relationship without arrows or labels; bottom-right clean isolated part lineup or exploded three-quarter arrangement containing exactly the outer frame, left leaf, right leaf, and small control/status panel, fully visible and not duplicated. Near-orthographic cameras and consistent scale.
Lighting/mood: even neutral studio illumination designed to reveal construction, seams, depth, leaf travel, and materials; minimal soft grounding only; no dramatic glow, fog, bloom, baked room lighting, or cast shadows obscuring clearances
Color palette: dark navy/charcoal frame and leaves, broad warm-gray structural caps, restrained neutral metal tracks and edges, small destination-green state strips and control indicator; no optional violet, hostile red, dominant crew cyan, or amber emergency lighting
State presentation: closed view uses dark or very restrained green status surfaces without glow spill; fully open view may show brighter destination green strips to communicate completion. Geometry and base materials remain identical between states. Do not change door identity or invent an energy barrier.
Constraints: one assembly shown only in required states/views; exact same frame, leaves, seams, locking relief, panel shapes, control panel, colors, and proportions across all panels; two leaves must visibly fit inside side pockets when open; clear floor-to-header opening; control panel separate and reachable; no swing hinges, rotating iris, roll-up shutter, circular vault door, single slab, visible gears crossing the opening, stairs, ramp, terminal pedestal, structural corridor, pipes, cables, hologram, force field, warning text, logo, lettering, numbers, watermark, character, collision visualization, navigation mesh, heavy damage, rust, or random greebles
Avoid: fantasy gate, bank vault, submarine hatch, modern elevator, garage door, smooth white spaceship, giant circular door, photorealistic industrial grime, complete environment screenshot, inconsistent open/closed geometry
```

## Review boundary

Review the closed/open identity, two-leaf travel concept, side-pocket clearance,
separate control panel, destination-green state, and compatibility with the
structure-kit family. Approval would not freeze the displayed approximate
dimensions or generated mechanism. Blender must prove exact clearances,
pivots, travel, collision interfaces, and the readable opening animation.
