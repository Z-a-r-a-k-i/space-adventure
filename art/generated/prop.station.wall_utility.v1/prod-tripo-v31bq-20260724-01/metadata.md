# Station wall utility candidate 01

Status: technical validation passed; project-owner visual approval and Phase 5
integration remain pending

## Provenance

- Asset ID: `prop.station.wall_utility.v1`
- Provider: Tripo Studio signed-in web application
- Model: `v3.1 - Best Quality`
- Operation: HD multi-view image-to-3D, one candidate, Generate in Parts off
- Provider task ID: `162d5614-e586-4a2b-a9b6-bbfe71d8caf9`
- Approved reference:
  `art/reference-sheets/frontier-station-v1/poc-models/station-wall-utility-turnaround-v1.png`
- Raw export: 58,219,864-byte GLB; legacy ignored-cache locator
  `raw/prop.station.wall_utility.v1__raw__tripo-v3.1__candidate-01.glb`.
  Confirm local cache presence before attempting a new destructive
  reconstruction; provider recovery is best-effort.

## Retained production files

- Editable Blender source:
  `art/source/prop.station.wall_utility.v1/wall-utility-v1.blend`
- Exact reviewed GLB: `derived/wall-utility-v1.glb`
- Structural checks: `validation.md`

The retained candidate is a static prop. It has no armature, skin, animation,
Mixamo dependency, generated collision, or gameplay behavior. The humanoid
pipeline does not apply to it.

## Decision

Candidate 01 retained the approved enclosure, vent, broad utility runs,
clamps, compact junction box, restrained palette, and cyan status accent. Its
generated rear was replaced in Blender with a closed flat mounting face. The
result passed the static-prop technical gates and isolated Godot review.

Keep this source and reviewed GLB unless the project owner rejects the visual
result or a later approved revision supersedes it. Do not regenerate it merely
because the character rigging or animation pipeline changes.
