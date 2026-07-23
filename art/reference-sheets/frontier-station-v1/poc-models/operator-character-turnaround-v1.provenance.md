# Operator character turnaround v1 — provenance

Status: approved human visual reference

Asset ID: `character.crew.operator.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

## Output

- File: `operator-character-turnaround-v1.png`
- Dimensions: 1254 × 1254
- SHA-256:
  `9cc780e475d427792562566f9a080646ac7fb27005a7605d1a89259c3e245b31`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This is the approved Operator visual anchor, not a production-ready character
brief.

## Input image roles

1. `../../../concepts/frontier-station-v1/poc-crew-lineup.png` (SHA-256:
   `2f2363d10defd95c4ee8bfe4a89decb168698f491415632b05cba24ec9544dd5`) —
   subject identity, outfit language, palette, proportions, and rendering-style
   reference; only the center Operator informed the subject.

## Generation prompt

```text
Use case: stylized-concept
Asset type: production game-character turnaround sheet for 3D modeling and image-to-3D reference, `character.crew.operator.v1`
Primary request: Create a brand-new reference sheet for the Operator character only. Image 1 is the subject and visual-style reference: preserve the identity, facial structure, athletic proportions, high dark hair bun, lighter technical armor language, navy-and-warm-gray palette, and restrained cyan accents of the CENTER female Operator. Do not include or copy the two male characters. Her approved separate pistol is not shown here.
Scene/backdrop: perfectly clean pale cool-gray studio background divided into four equal panels by thin neutral divider lines; no environment, no props, no text
Subject: the same unarmed adult female Operator in every panel, athletic agile build distinct from Vanguard, dark hair tied in a practical high bun with one restrained side fringe, focused neutral expression; close-fitting dark navy padded technical undersuit; fitted warm-gray chest plates; compact asymmetric shoulder-mounted sensor/comms module with a small cyan lens; slim forearm bracers; light knee and shin armor; reinforced but less bulky boots and fingerless technical gloves; utility belt and compact pouches; sparse cyan equipment accents; believable layered construction and restrained wear; one clearly modeled but EMPTY compact pistol holster at the right hip/thigh, identical in every view
Style/medium: polished stylized low-poly 3D character concept render, late-1990s retro-industrial science-fiction RPG flavor with restrained cyberpunk accents, readable broad shapes, game-production realism, no painterly background
Composition/framing: four consistent full-body views of exactly the same character at identical scale: front, strict left profile, straight back, and front-right three-quarter; neutral symmetrical A-pose with arms slightly away from torso and hands relaxed, feet shoulder-width; near-orthographic level camera; entire boots and head visible with generous padding
Lighting/mood: even neutral studio lighting designed to reveal form and materials; minimal soft grounding only; no dramatic rim light, fog, glow, or cast shadows crossing the figure
Constraints: character body and fixed outfit only; NO firearm, NO weapon, NO shield, NO loose handheld object; empty holster must remain empty; identical face, hair, outfit, armor shapes, sensor module, colors, pouches, and proportions across all four panels; armor seams visibly separable from the navy undersuit for future Blender source organization; joints unobstructed; clear back and side construction; model-ready silhouette; no logos, lettering, numbers, labels, watermark, pedestal, scenery, extra limbs, duplicate person, cropped body, or perspective exaggeration
Avoid: heroic action pose, weapon-ready stance, heavy tank armor, sexualized armor, exposed midriff, high heels, modern military camouflage, tiny noisy greebles, photorealistic skin, anime proportions, helmet, cape, shield, muzzle flash, hologram, floating UI
```

## Review boundary

Approval covers the Operator's identity, body proportions, fitted undersuit,
fixed armor outfit, sensor module, palette, and broad silhouette. It does not
define runtime armor items. Final holster geometry remains conditional on the
approved pistol and complete assembly.
