# Station survivor turnaround v1 — provenance

Status: approved human visual reference

Asset ID: `character.npc.station_survivor.v1`

Generated: 2026-07-23

Approved: 2026-07-23 by the project owner

## Output

- File: `station-survivor-turnaround-v1.png`
- Dimensions: 1254 × 1254
- SHA-256:
  `3b54a69c21a563fd9685737d44ac5e55330e8ca1a0e06186655ee96f780ad169`
- Generator: built-in Codex image-generation tool
- Provider model, seed, and job identifier: not exposed by the built-in tool

This is a proposed visual anchor, not an approved production brief or generated
3D asset.

## Input image roles

1. `operator-character-turnaround-v1.png` (SHA-256:
   `9cc780e475d427792562566f9a080646ac7fb27005a7605d1a89259c3e245b31`) —
   layout, render quality, crew scale, and shared visual language only; the
   Operator's identity and equipment were not requested.
2. `../../../concepts/frontier-station-v1/station-route-key-art.png` (SHA-256:
   `a540d65afb6144030c1177478a0f2b4653146f76ef4eb9a4daf65fbc7e7294b1`) —
   station context, material language, and amber emergency/caution mood only.

## Generation prompt

```text
Use case: stylized-concept
Asset type: production game-character turnaround sheet for 3D modeling, `character.npc.station_survivor.v1`
Primary request: Create a brand-new model-ready reference sheet for one distinct noncombatant frontier-station survivor. Image 1 is a layout, rendering-quality, crew-scale, and shared visual-language reference only; do not copy the Operator's identity, hair, armor, or equipment. Image 2 is the station environment and tiny survivor-context reference: use its amber emergency/caution mood and maintenance-station setting, but do not reproduce the room.
Scene/backdrop: perfectly clean pale cool-gray studio background divided into four equal panels by thin neutral divider lines; no environment, props, text, labels, or UI
Subject: the exact same unarmed middle-aged female station maintenance engineer in every panel, lean practical build and visibly older than the party, warm brown skin, short salt-and-pepper textured hair with one shaved practical side, tired intelligent face and calm guarded expression; dark navy padded maintenance coverall rather than combat armor; worn warm-gray reinforced work vest with two broad protective chest panels; rolled or fitted sleeves with thick utility cuffs; compact tool belt and two closed pouches; sturdy broad work boots; one small amber caution shoulder lamp and restrained amber piping, with only one tiny cyan station-interface tab; believable layered fabric and removable rigid work panels; light practical wear, no damage or grime overload. Her silhouette must read as civilian technical staff and a dialogue NPC, clearly less armored than the crew.
Style/medium: polished stylized low-poly 3D character concept render, late-1990s Fallout-inspired retro-industrial science-fiction RPG with restrained cyberpunk accents; broad readable forms, model-ready construction, consistent with the supplied reference quality
Composition/framing: four consistent full-body views of exactly the same person at identical scale: front, strict left profile, straight back, and front-right three-quarter; neutral symmetrical A-pose with arms slightly away from torso, hands relaxed, feet shoulder-width; near-orthographic level camera; entire head and boots visible with generous padding
Lighting/mood: even neutral studio lighting that reveals construction and materials; minimal soft grounding; no dramatic light, fog, glow, or cast shadows crossing the figure
Color palette: dark navy fabric, warm-gray work protection, restrained amber caution accents; no hostile red, destination green, optional violet, or dominant crew cyan
Constraints: one human only; identical identity, face, hair, outfit, proportions, pouches, and colors across all panels; continuous deforming undersuit/coverall under visibly separable rigid work panels; major joints unobstructed; clear back and side construction; no weapon, firearm, shield, helmet, backpack, loose tool, handheld object, exposed midriff, sexualized design, logos, lettering, numbers, watermark, pedestal, scenery, duplicate person, cropped body, or extra limbs
Avoid: combat stance, military armor, superhero silhouette, pristine corporate uniform, doctor lab coat, hazmat suit, anime proportions, photorealistic skin, tiny noisy greebles, hologram, floating UI
```

## Review boundary

Review the survivor's identity, age, civilian silhouette, fixed outfit, palette,
and suitability for dialogue staging. Approval would not define gameplay,
dialogue content, a separate equipment system, or final skinning on the shared
humanoid skeleton.
