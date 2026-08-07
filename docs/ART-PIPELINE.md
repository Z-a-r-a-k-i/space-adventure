# Art pipeline

## Goal

Produce readable, replaceable 3D assets for Godot 4.7.1. AI output is source
material, never automatic production authority. Blender 5.2 owns final sources
and GLB export; Godot owns runtime presentation.

## Supported animated subjects

The POC uses skeletal animation only for humanoids. Non-humanoids must be
simple rigid assemblies such as floating robots, stationary turrets, doors, or
single-pivot mechanisms. Do not introduce quadrupeds, walkers, creatures,
organic wings or tails, or transforming machines that require custom deforming
rigs and unique locomotion libraries.

Gameplay moves rigid machines. Their authored animation is limited to a few
pivots or transforms for hover, aim, recoil, impact, and shutdown.

## Asset lifecycle

1. Approve an asset brief, reference, owner, licensing, and privacy state.
2. Generate or model one useful unrigged source and record provider provenance.
3. Finish every mesh-changing operation before rigging when the asset deforms;
   static assets skip rigging entirely.
4. Review topology and appearance in the owning tool.
5. Apply only the class-specific work below: humanoid deformation, rigid
   pivots, or no animation at all.
6. Normalize scale, orientation, origin, names, materials, and sockets in
   Blender.
7. Validate and re-import the exact exported GLB.
8. Review that GLB in Godot before live integration.

Live gameplay replacement remains controlled by the roadmap. Offline art work
does not authorize scene wiring, abilities, attacks, damage, or timing.

## Static props and environment assemblies

Static props, modular architecture, furniture, terminals, wall dressing, and
non-moving equipment do not enter the humanoid pipeline. They need no T-pose,
fixed Quad-10k target, Mixamo upload, marker review, skeleton, skin weights, or
animation library.

1. Prefer dimensionally authored Blender geometry for structural kits,
   doorframes, floors, walls, collision-critical pieces, and precise modular
   assemblies. Tripo may supply decorative forms when it saves real work.
2. Use the topology and material budget from the asset brief. Retopologize or
   reconstruct only when the source fails silhouette, topology, UV, material,
   or runtime-performance requirements; there is no universal 10,000-polygon
   target for props.
3. In Blender, normalize bounds, axes, origin, mounting plane, material slots,
   UVs, and any separately authored collision contract.
4. Export one static GLB, fresh-import that exact file, and inspect it in Godot
   under representative lighting and camera distances.
5. Retain a technically validated source and reviewed GLB until a human rejects
   it or an approved replacement supersedes it. A character-pipeline change
   does not invalidate an inanimate asset.

Doors, terminals, and other stateful assemblies may expose a few rigid parts
or material states. Gameplay controls those node transforms and states; this
does not turn the asset into a skinned character.

## Humanoid pipeline

1. Start from exactly one approved front-view seed of one isolated, unarmed
   humanoid in a strict T-pose. Keep the image provider's production image
   count at one. An A-pose requires a named exception.
2. Use Tripo's direct single-image HD workflow by default. Select v3.1 Best
   Quality, Ultra Mesh Quality, Triangle topology with a 2,000,000-face target,
   4K PBR textures, AI Complete off, Generate in Parts off, and 8K Texture off.
   Generate Multi-Views is not a routine prerequisite: use it only for a named
   coverage defect after the direct workflow has failed.
3. Generate one static, unrigged source, inspect overall source coherence before
   spending on later operations, and preserve the untouched source in the
   ignored run-local cache.
4. In Tripo Retopology choose Smart Low-Poly v2, Quad, target 10,000, and retain
   original UVs when usable. Record requested and actual counts.
5. Inspect face, shoulders, elbows, wrists, hips, knees, ankles, hands, feet,
   armor boundaries, normals, holes, and disconnected parts. The deforming
   surface must remain continuous from torso through each shoulder and
   underarm; a detached limb is a source-mesh rejection, not a marker-placement
   or provider-retry problem.
6. Export the Tripo Mixamo FBX preset with the current 4K textures. Preserve
   that ZIP as the material master and upload its geometry-only FBX to Mixamo;
   textures do not affect marker placement or skinning.
7. In Mixamo, orient the character front-facing, enable symmetry, select
   Standard Skeleton (65), and place chin, wrist, elbow, knee, and groin/hip
   markers on the visible anatomical joint centers. Inspect the complete
   placement once and submit when it is anatomically coherent; routine marker
   placement does not require human confirmation.
8. Review the Auto-Rigger motion preview for deformation. When it passes,
   confirm the new character and download the neutral FBX Binary with skin.
   Adjust markers or return to Blender for a named defect instead of pausing
   for routine human confirmation.
   Treat the selected Mixamo skeleton profile as an input request only. Inspect
   and record the hierarchy, names, and bone count in the downloaded FBX; only
   the exported file establishes the publication contract.
9. Download one representative locomotion clip with skin and import that FBX
   untouched into a local ignored Godot baseline. The clip must remain grounded
   and visibly alternate both feet through a sustained gameplay move before
   Blender is permitted to process the rig or animation.
10. Correct joint placement and weights in Blender, especially chin, neck,
    shoulders, wrists, hips, knees, ankles, gloves, and armor transitions.
11. Use existing Mixamo library clips as the default motion source and
    download production donors without skin after Blender weight repair. Use
    30 fps, no keyframe reduction, and prefer in-place variants. `Standard
    Walk` with In Place on, Overdrive 50, and Character Arm-Space 50 is the
    default exploration walk. If a no-skin donor changes the accepted rest
    skeleton or armature-object transform, document a character-specific
    exception and use the matching with-skin donor instead.
12. In Blender, preserve the Mixamo armature object's imported rotation and
    scale. Never apply transforms directly to an animated armature: if a
    normalized production skeleton is required, retarget and bake evaluated
    world-space poses onto a separate rig. Trim, loop, repair contacts, add
    constraints and event markers, then export GLB.
13. Reimport the GLB and validate the complete cycle in evaluated world space.
    For the current exploration walk, horizontal hip range must stay at or below
    0.15 m, the loop endpoint delta at or below 0.01 m, vertical hip range at or
    below 0.15 m, and each foot must lift at least 0.04 m. Repeat the same
    sustained Godot movement used for the untouched baseline.

No Tripo Auto Rig or AI-generated motion is used for production humanoids when
the Mixamo workflow can provide the required baseline.

Base rig, idle, locomotion, and attachment-fit work may precede combat. Draw,
attack, contact, recoil, recovery, and holster clips must be selected, trimmed,
and validated alongside the authoritative Phase 4 action timings. Do not lock
a detached fight-animation set and then reshape gameplay rules around it.

## Weapons and rigid machines

Weapons remain separate rigid assets with primary grip, optional support grip,
and muzzle or contact markers. Validate the exact character-plus-weapon
assembly.

Machines use a small Blender-authored rigid hierarchy. Floating or stationary
forms are preferred. Reject a design that needs legs, organic deformation,
complex appendages, or a large bespoke animation set.

## Mechanical validation

Check the fields that apply to the asset class: bounds, units, axes, ground
contact or mounting plane, transforms, topology, UVs, textures, materials,
collision contract, pivots, sockets, and fresh GLB import diagnostics. For
humanoids also check skeleton hierarchy, weights, unweighted vertices,
influence limits, animation names and durations, loop behavior, contacts, and
root motion.

Technical validity does not establish visual quality. Direct visual review is
required for prominent characters, rigs, animation, weapons, and environments.
The assigned art operator validates humanoid marker placement and the
Auto-Rigger preview without an intermediate human approval gate. Project-owner
or delegated-art approval remains required only where the roster, roadmap,
brief, licensing/privacy state, scope, or live-replacement gate explicitly
names it.

## Review evidence

Review candidates live in Tripo, meshes and animation live in Blender, and
runtime imports live in Godot. Prefer direct playback over frame dumps. When a
frozen handoff image helps, use at most one representative screenshot per
checkpoint by default; create more only for a named defect.

Screenshots, turntables, contact sheets, sampled frames, and temporary review
files stay ignored under `artifacts/`. Commit only concise decisions, provider
IDs/settings, and structural metrics.

## Storage and publication

Untouched provider exports live under ignored
`art/generated/<asset-id>/<run-id>/raw/`. A tracked manifest records provider
and task/version IDs, cache-relative paths, settings, byte sizes, and presence.
Do not content-hash large 3D binaries. Tracked `.blend` and GLB files use normal
Git/LFS identity.

Do not publish a character GLB until its topology, rig, deformation, animation,
and complete assembly pass review. Static assets may be retained after their
brief-specific geometry, material, orientation, and Godot checks pass, even
while live integration is deferred. Never retain a rejected model or animation
as an active production asset merely for history.
