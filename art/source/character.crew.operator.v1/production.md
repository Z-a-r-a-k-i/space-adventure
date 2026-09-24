# Operator / Medic production

The owner promoted the retained Operator design to the third Medic on
2026-09-12. Reference and outfit identity are owned by the
[brief](../../briefs/operator-character-v1.md). Final owner handling review
is separate from the technical checks recorded here.

## Source identity and rights

Retained source: owner-approved Tripo Operator task
`dd18ffbe-b4bb-4035-9a82-d87da93d9d8a`, recovered from
`operator-character-v1.blend1` (12,181,513 bytes). The original file was read
only and copied to ignored `artifacts/art-expansion/raw/operator.blend` before
production. The existing project/provider rights and Sharing Only authorization
remain unchanged; no provider request, regeneration, new upload or purchase
occurred. Packed body texture and original identity are retained.

The prior 59-bone generic rig, unbound one-frame poses and unreliable skin
weights were discarded. The new 33-bone skeleton uses the actual accepted
Vanguard Mixamo names, parent hierarchy and rest rotations; rest translations
fit the retained anatomy. Actual accepted idle, in-place walk and down pose
channels are baked on that skeleton. This is a new binding, not an assignment
of old poses under renamed bones.

Blender's bone-heat solve failed on the retained segmented armor. Fresh smooth
anatomical envelopes therefore replace every old vertex weight; complete belt
pouches and the shoulder sensor retain their owning body region. Inverse
linear-blend conversion moves the retained neutral geometry into the fitted
Mixamo rest, reconstructing the neutral source within 0.000001 m. The hidden
old toe scaffold projected outside the boots and was removed below the shin.
The joint underlayer remains. No body regeneration or shape redesign occurred.

## Accepted technical output

`operator-v1.blend` is the editable source;
`game/Assets/Published/character.crew.operator.v1.glb` is the runtime asset.
It contains 25,162 triangles, two skinned meshes/materials, 33 bones and at most
four normalized influences per vertex. The neutral body is approximately
1.74 m high plus 0.006 m presentation floor clearance. Texture identities are
unchanged and packed in the source.

Actual holstered idle, walk/locomotion aliases, one-handed draw, armed idle,
armed locomotion, primary attack, holster and down are present. The separate
pistol has no support grip. Hand and hip sockets, palm offsets and the
`one_handed` metadata support the adapter's explicit one-handed mode. Draw and
holster use fitted paths around the real pistol; normalized gameplay timing
and recoil remain observation-driven. Every base/down frame is grounded from
evaluated skin contact before publication.

## Reproduction and verification

Use Blender 5.2 from the repository root:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_crew_expansion.py -- --asset operator --publish-existing
blender --background --factory-startup --python-exit-code 1 --python tools/blender/verify_station_crew_expansion.py
blender --background --factory-startup --python-exit-code 1 --python tools/blender/render_operator_portrait.py
```

The first command exports the committed editable source without a provider or
local raw cache. Omitting `--publish-existing` repeats the full binding from
the retained ignored backup and the accepted Vanguard source. The builder
checks neutral joint reconstruction; the verifier fresh-imports exact GLBs,
checks action/socket coverage and skin limits, and measures full published
loops, foot lift, floor contact and down. Diagnostics stay under
`artifacts/art-expansion/validation.json`.

Blender assembled idle/armed/walk reviews exposed and resolved detached old
weights, pouch stretching, sensor deformation and the toe scaffold. The
portrait is rendered from the final source. Final Godot distance, movement,
attack, healing, pause and retry checks belong to the integrated station gate.
