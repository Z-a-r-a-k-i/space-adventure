# Vanguard T-pose Mixamo production record

Asset: `character.crew.vanguard.v1`

Production run: `prod-tripo-v31bq-20260803-02`

Integrated: 2026-08-03

The August sections retain the original publication and review evidence.
The [September handling repair](#solo-handling-repair--2026-09-06) below
supersedes the original runtime draw/fire/holster recipe. Current contextual
verification is in [prototype history](../../../docs/archive/prototype-history.md).

## Authority and provenance

The project owner approved the front-view T-pose seed, Tripo Quad-10k result,
Mixamo marker placement, Auto-Rigger preview, and final Godot locomotion on
2026-08-03. The selected Tripo HD source is task
`d3851b9b-abed-4d47-a7e9-972d560fcd0c`, generated with v3.1 Best Quality,
Ultra Mesh Quality, Triangle 2M, 4K PBR, AI Complete off, Generate in Parts
off, and 8K Texture off. Smart Low-Poly v2 Quad retopology targeted 10,000 and
produced 10,592 vertices and 11,588 provider faces.

Raw Tripo and Mixamo files remain unchanged in the ignored workstation cache
and are enumerated in the production run's `raw-export.manifest.json`.

Mixamo exports used:

- `Unarmed Idle`, FBX Binary with skin, 30 fps, 58 frames, 39,613,984 bytes;
- `Standard Walk`, In Place, FBX Binary with skin, 30 fps, 36 frames,
  Overdrive 50, Character Arm-Space 50, 39,568,832 bytes.
- `Grab Rifle From Back`, `Rifle Aiming Idle`, in-place `Rifle Walk`,
  `Firing Rifle`, `Put Back Rifle`, and `Rifle Death`,
  FBX Binary without skin at 30 fps, no keyframe reduction, Overdrive 50, and
  Character Arm-Space 50.

The no-skin `Unarmed Idle` diagnostic was rejected as a rig authority because
its generic rest pose does not preserve this character's accepted bind pose.
For the combat clips, Blender requires the exact names, hierarchy, bone
lengths, and armature-object transform. It bakes each donor's local pose delta
onto the accepted rest pose, transfers root displacement in armature space,
and removes the donor. Direct curve assignment and the earlier global-matrix
correction both failed the standing/ground gates and are not permitted.

## Neutral-rig FBX inspection

The accepted rig authority is the with-skin idle FBX at
`art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260803-02/raw/mixamo/vanguard-unarmed-idle-with-skin.fbx`.
Blender 5.2 directly inspected 33 bones with one root. Every name below has the
`mixamorig:` prefix; `→` denotes a direct parent-to-child link:

- `Hips`
  - `Spine → Spine1 → Spine2`
    - `Neck → Head → HeadTop_End`
    - `LeftShoulder → LeftArm → LeftForeArm → LeftHand`
      - `LeftHandIndex1 → LeftHandIndex2 → LeftHandIndex3 → LeftHandIndex4`
    - `RightShoulder → RightArm → RightForeArm → RightHand`
      - `RightHandIndex1 → RightHandIndex2 → RightHandIndex3 → RightHandIndex4`
  - `LeftUpLeg → LeftLeg → LeftFoot → LeftToeBase → LeftToe_End`
  - `RightUpLeg → RightLeg → RightFoot → RightToeBase → RightToe_End`

## Blender assembly

`tools/blender/build_vanguard_character_v1.py` reproducibly builds
`vanguard-v1.blend` and the published GLB. It imports the accepted with-skin
idle as the rig authority, limits skinning to four normalized influences,
repairs material bindings, grounds and centers the presentation, adds the two
weapon sockets, and publishes:

- `anim.humanoid.idle_holstered` from `Unarmed Idle`;
- `anim.humanoid.locomotion_holstered` from the in-place `Standard Walk`; and
- `anim.humanoid.walk_holstered` from the same in-place walk donor;
- `anim.humanoid.draw_primary` from `Grab Rifle From Back`;
- `anim.humanoid.idle_armed` from `Rifle Aiming Idle`;
- `anim.humanoid.locomotion_armed` from in-place `Rifle Walk`;
- `anim.humanoid.attack_primary` from `Firing Rifle`;
- `anim.humanoid.holster_primary` from `Put Back Rifle`;
- `anim.humanoid.down` from `Rifle Death`.

Blender 5.2's native FBX importer is used. Because Mixamo leaves the metallic
texture embedded but unconnected, the build restores that channel from the
approved 4K Tripo ZIP material master and packs it into the Blender source.

The build preserves Mixamo's imported armature-object rotation and scale.
Applying those transforms directly corrupted animation-space translation in
the rejected build, so any future normalization must retarget and bake
evaluated world-space poses onto a separate rig.

The published body has 10,592 vertices, 11,586 Blender polygons, 21,158
runtime triangles, 33 bones, one material slot, and a 1.82 m evaluated height.
Every vertex is weighted and no vertex has more than four influences. Fresh
GLB reimport now requires all standing combat actions to keep the evaluated
hips at or above 0.75 m. The accepted ranges are 1.08575-1.12574 m for draw,
1.12368-1.12684 m for armed idle, 1.05630-1.13378 m for armed locomotion,
1.12347-1.12389 m for fire, and 1.11231-1.15027 m for holster. The down
clip ends at 0.19554 m from a 1.12388 m standing start.

Full-cycle evaluated world-space validation records horizontal hip ranges of
0.05113 m and 0.06218 m, loop endpoint delta 0.0 m, vertical hip range
0.06088 m, left-foot lift 0.15032 m, and right-foot lift 0.11324 m. The exact
exported GLB passes the same validation after fresh Blender reimport.

## Original Godot integration and review — August 2026

Godot imports the GLB's embedded images as Basis Universal data and sanitizes
the canonical dotted action names to underscore-separated AnimationPlayer
names. The GLB publishes nine actions. `VanguardPresentation` strictly requires
the eight runtime-selected actions; `anim.humanoid.locomotion_holstered` is a
non-required compatibility alias of `anim.humanoid.walk_holstered`. The
presentation attaches the published carbine to the hand or upper-back socket
from observed encounter state, scales draw/fire/holster playback to the
authoritative fixed-tick phases, faces the observed target or movement
direction, and freezes playback during tactical pause. Animation callbacks
never change gameplay.

Before Blender publication, the untouched with-skin Standard Walk FBX passed a
direct ignored Godot baseline: the character remained grounded and visibly
alternated both feet during a sustained gameplay move. The final GLB matched
that baseline without the previous flight or orbit defect. The station-route
Vanguard moves at 2.0 m/s to suit the walk cadence; a later run state may use a
separate faster clip and movement speed.

Direct graphical review in Godot confirmed scale, floor contact, silhouette,
direction changes, idle/walk blending, tactical-pause freezing, and stable
arrival. The project owner accepted the result on 2026-08-03.

The original Phase 4 solo-tutorial integration used the separate published
carbine and the draw, armed locomotion, fire, down, and holster actions. The
September repair below replaces that runtime handling recipe; the August
locomotion approval is not approval of the complete combat presentation.

## Solo handling repair — 2026-09-06

ADR 0027 authorizes this experiment in the owner's single checkout. Model,
accepted rest rig, 33 bones, 21,158 triangles, normalized skin weights and the
1.82 m standing envelope are preserved. Original provider exports remain in
the ignored raw cache; no new provider generation or skeleton conversion is used.

`repair_solo_presentation.py` (handling revision 2) fits the arm layer of armed
idle and locomotion to the exact carbine. It authors 55 samples over 1.8 seconds
for a reach/lift draw and reverse holster. Transfer landmarks are 25% and 75%;
the support hand joins over 60–90% of draw. The latter portion transitions to
the actual armed shoulder stance so the support wrist stays reachable on the
first armed frame after draw or retry. Both socket transforms are authored
against this complete assembly. The character builder now invokes this repair
after donor assembly and before its exact-GLB fresh-reimport gate.

Fresh reimport passed after revision 2: standing height 1.82 m, unchanged
locomotion endpoint/foot-lift metrics, draw/holster hip height 1.12388 m, armed
idle 1.12368–1.12684 m and armed walk 1.05630–1.13378 m. This supersedes the
earlier donor draw/holster hip ranges above.

Godot samples from simulation tick/fraction, restores the authored pose before
procedural changes, uses a normalized world weapon frame, and adds upper-body
aim, release-driven recoil and built-in two-bone left-arm IK. Palm proxies
derive from the accepted hand/index anatomy (right 0.062695 m, left 0.060356 m
along the hand's local Y). Fully armed review checkpoints enforce 3 cm maximum
proxy-to-grip error; these are rig-fit metrics, not finger-surface measurements.
The original `Firing Rifle` donor remains published for provenance but no
longer drives runtime shooting. See `docs/testing.md` for contextual checks.
