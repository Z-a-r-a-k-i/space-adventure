# Protector v1 production record

Asset: `character.crew.protector.v1`
Run: `prod-tripo-v31bq-20260805-01`
Integrated: 2026-08-07

The accepted direct T-pose source is Tripo task
`c9deee40-b461-45e1-840d-c0ca66cac4c3`, retopologized with Smart Low-Poly v2,
Quad, target 10,000. Untouched provider exports remain in the ignored run
cache and are enumerated by `raw-export.manifest.json`.

## Neutral-rig FBX inspection

Blender 5.2 directly inspected
`art/generated/character.crew.protector.v1/prod-tripo-v31bq-20260805-01/raw/candidate-04/mixamo/protector-rig-tpose-with-skin.fbx`
before publication. It contains 33 bones with one root. Every name below has
the `mixamorig:` prefix; `→` denotes a direct parent-to-child link:

- `Hips`
  - `Spine → Spine1 → Spine2`
    - `Neck → Head → HeadTop_End`
    - `LeftShoulder → LeftArm → LeftForeArm → LeftHand`
      - `LeftHandIndex1 → LeftHandIndex2 → LeftHandIndex3 → LeftHandIndex4`
    - `RightShoulder → RightArm → RightForeArm → RightHand`
      - `RightHandIndex1 → RightHandIndex2 → RightHandIndex3 → RightHandIndex4`
  - `LeftUpLeg → LeftLeg → LeftFoot → LeftToeBase → LeftToe_End`
  - `RightUpLeg → RightLeg → RightFoot → RightToeBase → RightToe_End`

`tools/blender/build_humanoid_character_v1.py` with
`tools/blender/profiles/protector-v1.json` builds and validates the asset. The
rig is authored at a 1.98 m rest-pose target; the holstered-idle evaluated
silhouette is 1.93136 m. The publication contains 21,962 triangles, two meshes,
two materials, one 2048 PBR texture set, 32 bones, and four normalized
influences maximum. The exact GLB fresh-reimports with the idle and two walk
action aliases plus both weapon sockets.

The hand socket is parented to `mixamorig:RightHand`; the upper-back holster is
parented to `mixamorig:Spine2`. Both use local `-Z` forward and `+Y` up by the
socket contract, but exact shotgun clearance and fit remain explicitly pending.

Godot uses the same strict `HumanoidPresentation` component for the waiting and
future party instances. Only one is visible at a time, tactical pause freezes
both, locomotion faces authoritative movement direction, and the content speed
is 2.0 m/s to match the accepted Standard Walk cadence.
