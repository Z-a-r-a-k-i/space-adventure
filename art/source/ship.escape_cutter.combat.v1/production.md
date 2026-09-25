# Cutter combat derivative

Original dimensional Blender presentation from the existing project-owned cutter
builder and [ship brief](../../briefs/ship-battle-v1.md). Existing station source
and publication are unchanged. No provider assets, samples, or external textures.

Reproduce with Blender 5.2:
`blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_ship_battle.py -- --replace --render`.

The builder stages source/GLB/manifest, fresh-imports the exact GLB, validates
budgets, hierarchy/anchors, unit scale, supporting floor samples and a 0.6m crew
clearance envelope at four work positions, then promotes all three together.
`manifest.json` owns measured dimensions and anchor coordinates. Low partitions
are intentional permanent overhead cutaways. Door leaves remain separate nodes.
v2 (ADR 0034) keeps every anchor and room while darkening the palette, cutting
decks into plates and adding cyan light lines on the hull walls and passage.

The Blender overhead review was inspected and the Godot asset scene loaded both
exports successfully. A 1280x720 in-engine capture with all three existing crew
sampled in their normal idle poses was inspected: all fit at full scale, and
both ships fit the frame. The scripted idle-wrapper smoke also passed without
errors. Integrated battle captures at 720p/1080p, work presentation, input/resize,
departure handoff and ordinary-command vent/seal emergencies were inspected.
Hazard anchors were adjusted after the engine exposed a floor occlusion defect.
Owner handling and visual acceptance remain open.
Review media stays ignored under `artifacts/reviews/ship-battle/v02/`.
