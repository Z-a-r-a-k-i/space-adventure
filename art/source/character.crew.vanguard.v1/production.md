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

The no-skin `Unarmed Idle` diagnostic was rejected because its generic rest
skeleton did not preserve this character's accepted rest pose. The Vanguard
therefore uses documented with-skin donors so Blender can compare the exact
rest skeleton and armature-object transform before removing donor meshes.

## Blender assembly

`tools/blender/build_vanguard_character_v1.py` reproducibly builds
`vanguard-v1.blend` and the published GLB. It imports the accepted with-skin
idle as the rig authority, limits skinning to four normalized influences,
repairs material bindings, grounds and centers the presentation, adds the two
weapon sockets, and publishes:

- `anim.humanoid.idle_holstered` from `Unarmed Idle`;
- `anim.humanoid.locomotion_holstered` from the in-place `Standard Walk`; and
- `anim.humanoid.walk_holstered` from the same in-place walk donor.

Blender 5.2's native FBX importer is used. Because Mixamo leaves the metallic
texture embedded but unconnected, the build restores that channel from the
approved 4K Tripo ZIP material master and packs it into the Blender source.

The build preserves Mixamo's imported armature-object rotation and scale.
Applying those transforms directly corrupted animation-space translation in
the rejected build, so any future normalization must retarget and bake
evaluated world-space poses onto a separate rig.

The published body has 10,592 vertices, 11,586 Blender polygons, 21,158
runtime triangles, 33 bones, one material slot, and a 1.82 m evaluated height.
Every vertex is weighted and no vertex has more than four influences.

Full-cycle evaluated world-space validation records horizontal hip ranges of
0.05113 m and 0.06218 m, loop endpoint delta 0.0 m, vertical hip range
0.06088 m, left-foot lift 0.15032 m, and right-foot lift 0.11324 m. The exact
exported GLB passes the same validation after fresh Blender reimport.

## Godot integration and review

Godot imports the GLB's embedded images as Basis Universal data and sanitizes
the canonical dotted action names to underscore-separated AnimationPlayer
names. `VanguardPresentation` strictly requires the canonical idle and walk
animations, maps them to authoritative movement and tactical-pause state,
rotates the presentation toward the commanded planar direction, and freezes
animation while paused.

Before Blender publication, the untouched with-skin Standard Walk FBX passed a
direct ignored Godot baseline: the character remained grounded and visibly
alternated both feet during a sustained gameplay move. The final GLB matched
that baseline without the previous flight or orbit defect. The station-route
Vanguard moves at 2.0 m/s to suit the walk cadence; a later run state may use a
separate faster clip and movement speed.

Direct graphical review in Godot confirmed scale, floor contact, silhouette,
direction changes, idle/walk blending, tactical-pause freezing, and stable
arrival. The project owner accepted the result on 2026-08-03.

The separate carbine, two-hand constraints, draw, aim, fire, recovery, and
holster sequence remain at their own later review gate.
