# Security Enforcer v1 production record

Status: approved by the project owner in the combined Godot gallery on 2026-08-07

Asset: `character.enemy.security_enforcer.v1`
Run: `prod-tripo-v31bq-20260807-01`
Integrated: 2026-08-07

The accepted direct T-pose source is Tripo task
`c48a7f4c-7a43-42c6-ac08-678b4c297821`, generated with v3.1 Best Quality,
Ultra, Triangle 2M, and 4K PBR. Smart Low-Poly v2 retopology used Quad with
a target of 10,000 and returned 13,438 faces and 12,322 vertices. The exact
retopologized GLB triangulates to 24,586 faces in Blender. Untouched provider
exports remain in the ignored run cache and are enumerated by
`raw-export.manifest.json`.

## Rig and donor inspection

Mixamo used front-facing orientation, symmetry, and Standard Skeleton (65).
The downloaded FBX establishes the actual contract: 41 imported bones before
cleanup and 40 publication bones after removal of `HeadTop_End`. The rig
authority is `security-enforcer-rig-tpose-with-skin.fbx`.

Both tested no-skin donors changed the accepted rest skeleton and were
rejected by the profile builder. This asset therefore uses the documented
matching-with-skin exception for `Unarmed Idle` and `Standard Walk`. The walk
uses In Place, Overdrive 50, Character Arm-Space 50, 30 fps, and no keyframe
reduction. Before Blender processing, its untouched FBX passed a direct Godot
baseline: 6.01 m of sustained movement, 0.0168 m planted-foot variation, and
alternating 0.0527/0.0395 m foot lift.

## Publication

`tools/blender/build_humanoid_character_v1.py` with
`tools/blender/profiles/security-enforcer-v1.json` builds, validates, stages,
fresh-reimports, and atomically publishes the asset. The evaluated idle-pose
height is 1.8705 m, within the accepted 1.90 m ±2% gate. The publication has
24,580 triangles, three meshes, exactly three semantic materials, one 2048²
PBR texture set, 40 bones, and four normalized influences maximum.
Godot intentionally imports the three embedded material images with
`gltf/embedded_image_handling=2`, which keeps them inside the imported scene as
VRAM-compressed Basis Universal textures. It does not extract duplicate PNGs,
so no separate image-import compression overrides are required.

The six actions are `anim.humanoid.idle_holstered`,
`anim.humanoid.locomotion_holstered`, and
`anim.humanoid.walk_holstered`, plus `anim.humanoid.melee_strike` from
`Right Hook`, `anim.humanoid.hit_reaction` from `Hit Reaction`, and
`anim.humanoid.down` from `Falling Back Death`. All three combat donors use
the matching-with-skin exception because their downloaded skeleton is the
accepted rig authority. Locomotion has 0.05393 m planar hip range,
0.05859 m vertical hip range, zero loop-endpoint delta, and
0.10307/0.09889 m left/right foot lift. The right-hand contact socket publishes
as `socket.attack.contact.primary`. Its zero profile offset is intentional:
Blender bone parenting places it at the `RightHand` bone tail, at the knuckle
base rather than the wrist, and fresh GLB reimport validates the bone, rotation,
axes, and placement reference. Exact contact-frame clearance remains coupled to
Phase 4 timing. The live solo-tutorial presentation plays `Right Hook` at
1.42× so its release aligns with the authoritative 24-tick wind-up; recovery
remains core-owned at 36 ticks. The measured combat motion envelopes are
0.14803 m planar/0.14797 m vertical for the strike, 0.21364/0.09666 m for the
hit reaction, and 0.96759/0.71884 m for the falling-down clip. The exact GLB
fresh-reimports with all six named actions and the contact socket.
