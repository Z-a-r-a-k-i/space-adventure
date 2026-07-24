# Field-aid turnaround v1 — provenance

Status: approved item visual reference

Asset ID: `item.healing.field_aid.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

Selected form: alternative 2 (top-right) from
`field-aid-form-exploration-v1.png`, selected by the project owner on
2026-07-23

## Output

- File: `field-aid-turnaround-v1.png`
- Dimensions: 1536 × 1024
- SHA-256:
  `c00a16542e4b97932f816fb65e157251ca3855c28b67424bcec545d3831bc48f`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This is the selected field-aid form's dedicated visual reference, not an
approved production brief or generated 3D asset.

## Input image roles

1. `field-aid-form-exploration-v1.png` (SHA-256:
   `316560aff4897f96ffcd9786fcf24d442b51549d8d7daa73bb2692b443bf9a90`) —
   subject identity and proportions; only the top-right handled medkit
   alternative informed the subject.
2. `../station-service-terminal-turnaround-v1.png` (SHA-256:
   `b433bae19a05a506257692b0e9e5c13235295cb3306acfb56a2166eb15c85503`) —
   clean turnaround layout, hard-surface material separation, and rendering
   quality only.

## Generation prompt

```text
Use case: stylized-concept
Asset type: final selected hard-surface turnaround sheet for 3D modeling and image-to-3D reference, `item.healing.field_aid.v1`
Primary request: Create a brand-new four-view model-ready reference sheet of ONLY the TOP-RIGHT alternative from Image 1: the compact rectangular handled field-medkit/cassette. Preserve that selected design's identity, proportions, integrated top carry handle, dark navy case, warm-gray reinforced corner caps, central front latch, broad closed-case seam, tiny amber front status inset, and tiny cyan side tab. Ignore the other three alternatives completely. Image 2 is a clean turnaround-layout, hard-surface material-separation, and rendering-quality reference only; do not copy the terminal silhouette or violet screen.
Scene/backdrop: perfectly clean pale cool-gray studio background divided into four equal panels by thin neutral divider lines; no environment, floor, text, labels, UI, or scenery
Subject: the exact same single closed compact frontier-station field medkit in every panel, approximately 0.38 m wide × 0.27 m high × 0.13 m deep; sturdy rectangular dark navy shell with a broad integrated top handle sized for a gloved hand; four warm-gray chamfered protective corner/edge caps; one strong horizontal opening seam; one centered rugged front latch module with a tiny amber status inset; one tiny muted cyan interface tab on the right side; restrained worn neutral-metal edges and mild practical wear. The form must read unmistakably as portable field aid rather than a weapon, ammunition box, generic toolbox, or large suitcase. Keep it closed with no visible contents.
Style/medium: polished stylized low-poly 3D hard-surface concept render, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; chunky manufacturable construction, controlled bevels, broad tactical-camera readability, matching the supplied project's visual quality
Composition/framing: four consistent views of the exact same object at identical scale: strict front, strict right side, straight rear, and front-right three-quarter; near-orthographic cameras; full object and handle visible with generous padding. Include one small neutral simplified gloved-hand scale silhouette beside the strict front view only, not holding the medkit. No captions, arrows, letters, or numbers.
Lighting/mood: even neutral studio lighting designed to reveal shell construction, handle clearance, seam, latch, rear, side, and materials; minimal soft grounding only; no dramatic glow, fog, liquid effects, treatment VFX, or cast shadows crossing the object
Color palette: dark navy/charcoal housing, warm-gray protective caps, restrained worn neutral metal, tiny amber front accent and tiny muted cyan side tab; no hostile red, destination green, optional violet, pharmacy white, or dominant emission
Constraints: preserve the TOP-RIGHT selected form only; identical proportions, handle, caps, latch, seam, status insets, colors, and wear across all four views; one medkit repeated only as required views; thick supported handle with believable gloved-hand clearance; flat stable base; no open panel, visible medical contents, loose cartridges, needles, pills, bottles, bandages, cables, hologram, medical cross, logo, lettering, numbers, watermark, blood, scenery, floating parts, or extra accessories; no pistol grip, muzzle, barrel, trigger, magazine, blade, grenade silhouette, or firearm proportions
Avoid: any of the top-left, bottom-left, or bottom-right alternatives; modern plastic first-aid box, military ammunition can, ordinary toolbox, oversized suitcase, backpack, sci-fi gun, potion container, photorealistic grime, tiny noisy greebles, anime prop, glowing magic object
```

## Review boundary

Review the selected medkit's proportions, handle, latch, opening seam, palette,
and one- or two-hand usability. Approval would freeze the visual direction but
would not yet approve generated geometry, internal contents, opening
articulation, dimensions after assembly fitting, or the healing-use animation.
