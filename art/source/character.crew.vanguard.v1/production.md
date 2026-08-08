# Vanguard T-pose Mixamo production record

Asset: `character.crew.vanguard.v1`

Production run: `prod-tripo-v31bq-20260803-02`

Integrated: 2026-08-03

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
  `Firing Rifle`, `Put Back Rifle`, `Rifle Hit To Back`, and `Rifle Death`,
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
- `anim.humanoid.hit_reaction` from standing reaction frames 1-10 of
  `Rifle Hit To Back`; and
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
1.12347-1.12389 m for fire, 1.11231-1.15027 m for holster, and
0.84942-1.12388 m for the trimmed hit reaction. The down clip ends at
0.19554 m from a 1.12388 m standing start.

Full-cycle evaluated world-space validation records horizontal hip ranges of
0.05113 m and 0.06218 m, loop endpoint delta 0.0 m, vertical hip range
0.06088 m, left-foot lift 0.15032 m, and right-foot lift 0.11324 m. The exact
exported GLB passes the same validation after fresh Blender reimport.

## Godot integration and review

Godot imports the GLB's embedded images as Basis Universal data and sanitizes
the canonical dotted action names to underscore-separated AnimationPlayer
names. `VanguardPresentation` strictly requires all ten actions, attaches the
published carbine to the hand or upper-back socket from observed encounter
state, scales draw/fire/holster playback to the authoritative fixed-tick
phases, faces the observed target or movement direction, and freezes playback
during tactical pause. Animation callbacks never change gameplay.

Before Blender publication, the untouched with-skin Standard Walk FBX passed a
direct ignored Godot baseline: the character remained grounded and visibly
alternated both feet during a sustained gameplay move. The final GLB matched
that baseline without the previous flight or orbit defect. The station-route
Vanguard moves at 2.0 m/s to suit the walk cadence; a later run state may use a
separate faster clip and movement speed.

Direct graphical review in Godot confirmed scale, floor contact, silhouette,
direction changes, idle/walk blending, tactical-pause freezing, and stable
arrival. The project owner accepted the result on 2026-08-03.

The Phase 4 solo-tutorial integration uses the separate published carbine and
the draw, armed locomotion, fire, hit, down, and holster actions. Final
project-owner graphical acceptance of the complete live combat presentation
remains the review gate for this update.
