# Station survivor v1 production record

Asset: `character.npc.station_survivor.v1`
Run: `prod-tripo-v31bq-20260804-01`
Integrated: 2026-08-07

The accepted direct T-pose source is Tripo task
`fa370b18-b604-417e-8fa2-c6349712708f`, retopologized with Smart Low-Poly v2,
Quad, target 10,000. Untouched provider exports remain in the ignored run
cache and are enumerated by `raw-export.manifest.json`.

## Neutral-rig FBX inspection

Blender 5.2 directly inspected
`art/generated/character.npc.station_survivor.v1/prod-tripo-v31bq-20260804-01/raw/character.npc.station_survivor.v1__rig__mixamo-standard65-with-skin-neutral.fbx`
before publication. It contains 65 bones with one root. Every name below has
the `mixamorig:` prefix; `→` denotes a direct parent-to-child link:

- `Hips`
  - `Spine → Spine1 → Spine2`
    - `Neck → Head → HeadTop_End`
    - `LeftShoulder → LeftArm → LeftForeArm → LeftHand`
      - `LeftHandThumb1 → LeftHandThumb2 → LeftHandThumb3 → LeftHandThumb4`
      - `LeftHandIndex1 → LeftHandIndex2 → LeftHandIndex3 → LeftHandIndex4`
      - `LeftHandMiddle1 → LeftHandMiddle2 → LeftHandMiddle3 → LeftHandMiddle4`
      - `LeftHandRing1 → LeftHandRing2 → LeftHandRing3 → LeftHandRing4`
      - `LeftHandPinky1 → LeftHandPinky2 → LeftHandPinky3 → LeftHandPinky4`
    - `RightShoulder → RightArm → RightForeArm → RightHand`
      - `RightHandThumb1 → RightHandThumb2 → RightHandThumb3 → RightHandThumb4`
      - `RightHandIndex1 → RightHandIndex2 → RightHandIndex3 → RightHandIndex4`
      - `RightHandMiddle1 → RightHandMiddle2 → RightHandMiddle3 → RightHandMiddle4`
      - `RightHandRing1 → RightHandRing2 → RightHandRing3 → RightHandRing4`
      - `RightHandPinky1 → RightHandPinky2 → RightHandPinky3 → RightHandPinky4`
  - `LeftUpLeg → LeftLeg → LeftFoot → LeftToeBase → LeftToe_End`
  - `RightUpLeg → RightLeg → RightFoot → RightToeBase → RightToe_End`

`tools/blender/build_humanoid_character_v1.py` with
`tools/blender/profiles/station-survivor-v1.json` builds the Blender source and
stages the GLB before atomic replacement. The rig is authored at a 1.70 m
rest-pose target; the dialogue-idle evaluated silhouette is 1.66439 m and
passes the brief's explicit ±3% evaluated-action gate. The publication contains
25,756 triangles, five meshes, five materials, one 2048 PBR texture set, 64
bones, and four normalized influences maximum. The exact GLB fresh-reimports as
25,836 triangles with all three required dialogue actions.

Godot uses `HumanoidPresentation` to validate the imported actions strictly,
blend dialogue states, and freeze playback during tactical pause. The live
Survivor changes from dialogue idle to speaking solely from the observed
authoritative dialogue state. Collision, interaction identity, label, and
route behavior remain unchanged.
