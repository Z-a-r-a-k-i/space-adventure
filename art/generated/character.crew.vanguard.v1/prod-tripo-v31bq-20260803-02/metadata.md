# Vanguard 4K T-pose generation

Status: integrated in the station-route prototype with Mixamo idle and walk

- Asset ID: `character.crew.vanguard.v1`
- Provider: Tripo Studio signed-in web application
- Generated: 2026-08-03
- Input: `input/front-tpose.png`
- Mode: HD Model, single-image
- Model: v3.1 Best Quality
- Ultra Mesh Quality: on
- AI Complete: off
- Texture: 4K, PBR on
- Topology: Triangle, requested 2,000,000 faces
- Generate in Parts: off
- 8K Texture: off
- Privacy: Sharing Only
- Displayed cost: 55 credits per generation
- Retopology, rigging, export, and publication: completed

## Provider results

The first submission appeared not to start because Tripo did not navigate or
show progress immediately. A retry was clicked, after which Studio completed
two separate tasks. This was an operator error; no further generation was
submitted.

1. Task `d3851b9b-abed-4d47-a7e9-972d560fcd0c`: 1,988,521 faces and
   1,022,314 vertices.
2. Task `2d2c2573-c6fb-4e9d-8e00-2f943e89778a`: 1,938,376 faces and
   997,132 vertices.

The project owner selected `d3851b9b-abed-4d47-a7e9-972d560fcd0c` because its
face and shoulder transition appear slightly more coherent.

## Retopology

- Provider operation: Retopology
- Source task: `d3851b9b-abed-4d47-a7e9-972d560fcd0c`
- Method: Smart Low-Poly v2
- Topology: Quad
- Requested polygon target: 10,000
- Displayed operation cost: 40 credits
- Result: 11,588 faces and 10,592 vertices
- Initial live review: T-pose, silhouette, face, shoulders, hands, and major
  armor shapes remain visually coherent
- Export, rigging, and publication: completed

The 10,000 value is a target rather than an exact output guarantee. The project
owner approved the live Quad result before export.

## Mixamo handoff

- Tripo export preset: Mixamo FBX, current 4K textures
- Cached ZIP: `raw/vanguard-tpose-quad10k-4k.zip` (77,221,987 bytes)
- ZIP contents: one FBX plus external base-color, normal, metallic, and
  roughness textures
- Geometry-only upload FBX:
  `raw/mixamo/vanguard-tpose-quad10k.fbx` (39,143,520 bytes)
- The 4K ZIP remains the material master; only the FBX is intended for Mixamo
  auto-rigging because textures do not affect marker placement or skinning
- Mixamo upload: completed from the geometry-only FBX
- Mixamo processing: completed without an ingestion error
- Orientation: front-facing T-pose accepted
- Marker placement: completed; Chin, Wrists, Elbows, Knees, and Groin markers
  were inspected before Auto-Rigger submission
- Marker review: chin centered below the beard; wrists and elbows centered on
  the visible arm joints; knees centered on the knee armor; groin centered on
  the pelvis between the upper legs
- Use Symmetry: on
- Skeleton LOD: Standard Skeleton (65)
- Marker approval: project owner approved the placement on 2026-08-03
- Auto-rigging: completed successfully in Mixamo
- Rig review: project owner accepted the Mixamo preview on 2026-08-03
- Active-character confirmation: completed; Mixamo displays
  `VANGUARD-TPOSE-QUAD10K` as the active character
- Neutral baseline: `Unarmed Idle`, FBX Binary with skin, 30 fps, no keyframe
  reduction, 58 frames
- Default walk donor: `Standard Walk`, In Place, FBX Binary with skin, 30 fps,
  no keyframe reduction, Overdrive 50, Character Arm-Space 50, 36 frames,
  Mirror off
- Blender validation: 10,592 vertices, 11,586 polygons, 21,158 triangles,
  33 bones, maximum four skin influences, 1.82 m evaluated height
- Untouched baseline: the Standard Walk with-skin FBX was imported directly in
  Godot and remained grounded with visible alternating steps during a sustained
  gameplay move
- Transform correction: the Mixamo armature object's imported 90-degree X
  rotation and `0.019069` scale are preserved; direct transform application is
  prohibited because it corrupts animation-space translation
- Walk world-space validation: horizontal hip ranges `0.05113` m and `0.06218`
  m, loop endpoint delta `0.0` m, vertical hip range `0.06088` m, left-foot lift
  `0.15032` m, and right-foot lift `0.11324` m
- Publication validation: the exported GLB is reimported automatically; its
  body is centered on the ground-plane origin, its boots are at ground, and the
  same world-space walk metrics pass after reimport
- Published outputs: `art/source/character.crew.vanguard.v1/vanguard-v1.blend`
  and `game/Assets/Published/character.crew.vanguard.v1.glb`
- Godot integration: direct Vanguard startup, holstered idle, and grounded
  in-place Standard Walk playback accepted in the station-route prototype after
  comparison with the untouched FBX baseline
