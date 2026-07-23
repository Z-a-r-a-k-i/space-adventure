# Vanguard carbine turnaround v1 — provenance

Status: approved human visual reference

Asset ID: `weapon.crew.vanguard_carbine.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

## Output

- File: `vanguard-carbine-turnaround-v1.png`
- Dimensions: 1254 × 1254
- SHA-256:
  `fe6cb280507202cd63e1b72ebf6f1e6329ad165ab1ea96e0b0e517d195c9b099`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This is the approved Vanguard-carbine visual anchor, not a production-ready
asset brief.

## Input image roles

1. `../../../concepts/frontier-station-v1/poc-crew-lineup.png` (SHA-256:
   `2f2363d10defd95c4ee8bfe4a89decb168698f491415632b05cba24ec9544dd5`) —
   visual language and rough weapon-subject reference; only the left-most
   Vanguard and his carbine informed the subject.
2. `vanguard-character-turnaround-v1.png` (SHA-256:
   `66858ffda50cb37a113d6a3eeb66165fb57de02d60372a7b1551008bd349d0db`) —
   approved character scale, palette, outfit, and equipment reference; the
   character was not copied into the output.

## Generation prompt

```text
Use case: stylized-concept
Asset type: production reference sheet for the separate 3D game weapon `weapon.crew.vanguard_carbine.v1`
Primary request: Create a brand-new model-ready reference sheet for the Vanguard's fixed two-handed carbine. Image 1 is a visual-language and rough weapon-subject reference: use the LEFT-MOST Vanguard's carbine as inspiration, but refine it into one coherent manufacturable weapon. Image 2 is an approved character scale, palette, and equipment reference; do not redesign the character.
Scene/backdrop: perfectly clean pale cool-gray studio background divided into four equal panels by thin neutral divider lines; no environment
Subject: exactly one broad retro-industrial science-fiction carbine, medium-long and clearly two-handed, with a sturdy squared receiver, short thick barrel, compact stock, primary pistol grip, clearly reachable forward support grip, inset vent cuts, restrained warm-gray armor plates over a dark navy/charcoal mechanical body, tiny sparse cyan status accents, mild practical wear. Preserve a clear line from stock through barrel and a readable muzzle opening. No loose magazine, ammunition, sling, scope, bayonet, shield, full character, or hands holding the weapon; the only permitted human element is the small neutral forearm-and-hand scale silhouette specified below.
Style/medium: polished stylized low-poly 3D hard-surface concept render, late-1990s Fallout-inspired industrial science-fiction with restrained cyberpunk accents, model-ready construction, broad readable shapes rather than tiny greebles
Composition/framing: four consistent views of the exact same weapon at identical scale: strict left side profile, top view, front/muzzle view, and front-right three-quarter view; near-orthographic camera; entire weapon visible with generous padding. Include one small simple neutral human forearm-and-hand scale silhouette beside the side view only, not holding the weapon.
Lighting/mood: even neutral studio lighting that reveals form and material separation; minimal soft grounding; no dramatic glow or fog
Constraints: identical geometry, colors, vents, grips, muzzle, and asymmetry in all views; believable grip spacing for the approved Vanguard; broad silhouette readable from an elevated tactical camera; clean separable hard-surface parts suitable for Tripo reference and Blender cleanup; no logos, letters, numbers, labels, dimensions, watermark, scenery, projectile, muzzle flash, hologram, or extra weapon
Avoid: modern real-world assault-rifle copying, thin fragile barrel, oversized sniper scope, fantasy ornament, excessive rails, tiny noisy greebles, white ceramic sci-fi gun, toy proportions, impossible grip, warped orthographic views
```

## Review boundary

Approval covers the carbine's silhouette, proportions, palette, major parts,
grip arrangement, and muzzle language. The complete Vanguard assembly must
still validate hand spacing, support-hand reach, shoulder placement, tactical
readability, and the character's provisional holster or carry hardware.
