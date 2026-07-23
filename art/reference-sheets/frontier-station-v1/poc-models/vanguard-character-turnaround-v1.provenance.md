# Vanguard character turnaround v1 — provenance

Status: approved human visual reference

Asset ID: `character.crew.vanguard.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

## Output

- File: `vanguard-character-turnaround-v1.png`
- Dimensions: 1254 × 1254
- SHA-256:
  `66858ffda50cb37a113d6a3eeb66165fb57de02d60372a7b1551008bd349d0db`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This sheet is the approved Vanguard visual anchor. Approval freezes the face,
body proportions, fixed outfit, armor language, palette, and broad silhouette.
It is not a production-ready character brief and does not authorize Tripo
generation.

## Input image roles

1. `../../../concepts/frontier-station-v1/poc-crew-lineup.png` (SHA-256:
   `2f2363d10defd95c4ee8bfe4a89decb168698f491415632b05cba24ec9544dd5`) —
   subject identity, armor language, palette, proportions, and rendering-style
   reference. Only the left-most Vanguard informed the subject.
2. `../station-service-terminal-turnaround-v1.png` (SHA-256:
   `b433bae19a05a506257692b0e9e5c13235295cb3306acfb56a2166eb15c85503`) —
   clean 2 × 2 turnaround layout reference only; its terminal design did not
   inform the character.

## Generation prompt

```text
Use case: stylized-concept
Asset type: production game-character turnaround sheet for 3D modeling and image-to-3D reference
Primary request: Create a brand-new reference sheet for the Vanguard character only. Image 1 is a subject and visual-style reference: preserve the identity, facial structure, sturdy proportions, armor language, navy-and-warm-gray palette, chunky boots and restrained cyan accents of the LEFT-MOST bearded male Vanguard. Do not include or copy the other two characters. Image 2 is a layout reference only: use a similarly clean 2x2 turnaround presentation, not its terminal subject.
Scene/backdrop: perfectly clean pale cool-gray studio background divided into four equal panels by thin neutral divider lines; no environment, no props, no text
Subject: the same unarmed adult male Vanguard in every panel, sturdy athletic build, short dark textured undercut hair, trimmed beard, stern but neutral expression; dark navy padded utility suit; broad retro-industrial warm-gray chest, shoulder, forearm, knee and shin armor; chunky reinforced boots and gloves; sparse cyan equipment accents; believable layered construction and restrained wear; one clearly modeled but EMPTY primary hip/thigh holster mount, identical in every view
Style/medium: polished stylized low-poly 3D character concept render, late-1990s retro-industrial science-fiction RPG flavor with restrained cyberpunk accents, readable broad shapes, game-production realism, no painterly background
Composition/framing: four consistent full-body views of exactly the same character at identical scale: front, strict left profile, straight back, and front-right three-quarter; neutral symmetrical A-pose with arms slightly away from torso and hands relaxed, feet shoulder-width; near-orthographic level camera; entire boots and head visible with generous padding
Lighting/mood: even neutral studio lighting designed to reveal form and materials; minimal soft grounding only; no dramatic rim light, fog, glow, or cast shadows crossing the figure
Constraints: character body and clothing only; NO firearm, NO weapon, NO shield, NO loose handheld object; empty holster mount must remain empty; identical face, hair, outfit, armor shapes, colors, pouches, and proportions across all four panels; clear back and side construction; model-ready silhouette; no logos, lettering, numbers, labels, watermark, pedestal, scenery, extra limbs, duplicate person, cropped body, or perspective exaggeration
Avoid: heroic action pose, weapon-ready stance, bulky exoskeleton, modern military camouflage, tiny noisy greebles, photorealistic skin, anime proportions, helmet, cape, shield, muzzle flash, hologram, floating UI
```

## Review boundary

Approval covers the Vanguard's face, body proportions, fitted navy undersuit,
fixed complete armor outfit, palette, and broad equipment silhouette. It does
not define separate runtime armor items or equipment slots. The Blender source
should retain the undersuit and major armor pieces as named editable objects on
the shared skeleton when practical, following
`../../../../docs/ART-PIPELINE.md`.

The separate carbine sheet is now approved. The empty thigh hardware remains
provisional until the character and weapon exist at normalized 3D scale; revise
or confirm the carry solution during the complete-assembly fit check.

Reject or revise if the four views are not perceived as the same person, the
Vanguard is too close to Protector's heavy silhouette, the armor blocks major
joints, the cyan accents dominate, or the amount and placement of pouches and
holster hardware are unsuitable.
