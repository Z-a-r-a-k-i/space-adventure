# Tripo, Mixamo, Blender handoff

Status: current class-specific production procedure

## Preflight

Before opening Tripo, confirm the active asset has:

- an approved roster ID, visual reference, and production brief;
- recorded project-owner or delegated-art-approver acceptance of the brief,
  plus a dedicated art worktree;
- resolved licensing and privacy state; and
- no unresolved design choice that would change the model.

Use the signed-in Tripo Studio web application. Do not add a Tripo API key or
provider-to-Godot bridge.

After preflight acceptance, the assigned art operator may complete routine
Tripo operations. Mixamo marker placement and the Auto-Rigger preview retain
their human approval gate. A new owner decision is also needed when the brief,
roster, licensing/privacy state, or live gameplay activation would change.

## Generate the static source

1. Prepare a clean prompt for one isolated, unrigged T-pose humanoid. Keep
   weapons separate. Generate exactly one production seed image; set Number
   of Images to `1`. If exploratory candidates are needed, select and approve
   one before opening the Model workspace.
2. Treat multiple image-generation outputs as a candidate batch, never as a
   multiview set. Do not click Generate 3D on a four-image batch: Tripo will
   generate separate single-view models rather than one view-informed model.
3. Load only the approved front-view seed into Tripo's direct single-image HD
   workflow. Generate Multi-Views is an exception for a named coverage defect,
   not a default production step; the accepted Vanguard comparison produced a
   better face and eyes from the direct input.
4. Use HD Model with v3.1 Best Quality and Ultra Mesh Quality enabled. For the
   dense pre-retopology source, use Triangle topology with a 2,000,000-face
   target, 4K textures, and PBR. Keep AI Complete, Generate in Parts, and 8K
   Texture off. Use the asset's approved account privacy setting.
5. Before submitting the paid operation, confirm that the input is present,
   the generation represents one subject, and the displayed cost matches the
   reviewed settings. Generation must create one static unrigged character;
   reject any grouped, batch, or multi-character output before retopology.
6. Generate one candidate and inspect it live. Generate another only for a
   named defect that cannot be repaired.
7. Preserve the selected untouched export in the ignored run-local cache and
   record its task/version, settings, path, and byte size.

For humanoids, require separated limbs, complete hands and feet, readable
joints, one coherent face, and a continuous deforming surface beneath rigid
armor. Finish segmentation or part completion before retopology.

## Retopology and export

1. Keep the selected model static and unrigged.
2. Run Retopology with Smart Low-Poly v2, Quad, target 10,000, retaining
   original UVs when usable.
3. Record the actual vertex, face, quad, triangle, material, and UV results.
4. Inspect silhouette and joint regions live and in Blender wireframe.
5. Export a static GLB and the Tripo Mixamo FBX preset with current 4K
   textures. Preserve the ZIP as the material master and extract its FBX for a
   geometry-only Mixamo upload.

Tripo's polygon target is not an exact result guarantee. If any mesh operation
occurs after rigging, discard that rig and restart from the new static mesh.
Do not run Tripo Auto Rig for production humanoids.

## Mixamo Auto-Rigger

1. Upload the geometry-only FBX. Keep the 4K Tripo ZIP as the separate material
   master; textures do not improve Auto-Rigger placement or skinning.
2. Confirm the character is front-facing before opening marker placement.
3. Enable Use Symmetry, select Standard Skeleton (65), and position the chin,
   wrists, elbows, knees, and groin/hip markers on the visible joint centers.
4. Inspect the complete marker layout once and obtain human approval before
   Auto-Rigger submission.
5. Inspect the rig preview for head/neck, shoulder, wrist, hip, knee, ankle,
   hand, and armor deformation. Obtain human approval; adjust markers or return
   to Blender for a named defect.
6. Confirm the new Mixamo character and download the accepted neutral FBX
   Binary with skin.

Mixamo is a baseline, not final authority. Visible deformation defects return
to Blender for weight or joint repair.

## Mixamo animation library

Use Mixamo's existing library as the default humanoid motion source. Review a
candidate clip at real speed before cleanup. After the corrected rig is
accepted, download production animation donors as FBX Binary without skin.
Prefer in-place variants and use 30 fps with no keyframe reduction. `Standard
Walk` with In Place enabled, Overdrive 50, and Character Arm-Space 50 is the
default exploration walk unless a character's brief names a different style.
If the no-skin donor does not preserve the accepted rest pose or
armature-object transform, record a character-specific exception and use the
matching with-skin donor.

Before Blender processing, copy one with-skin locomotion FBX to the ignored
local Godot baseline area and play it directly. Issue a sustained gameplay move
and inspect multiple phases of the cycle. If this untouched reference does not
stay grounded and alternate both feet, stop before publication.

Blender owns retargeting where required, trimming, looping, root handling,
contact cleanup, hand and weapon constraints, action names, transfer markers,
and final export. Preserve the imported Mixamo armature-object rotation and
scale. Applying those transforms directly to an animated armature corrupts its
pose curves; normalize only by retargeting and baking evaluated world-space
poses onto a separate production skeleton. Validate every frame of the final
GLB against the untouched baseline. A technically valid clip is rejected if
its weight transfer, foot plants, cadence, counter-motion, or action intent
looks wrong.

## Static props and environment pieces

Static props do not use the humanoid retopology and Mixamo sequence.

1. Start from the approved brief and reference. Use Blender directly for
   precise structural or modular geometry; use Tripo only when its decorative
   form is useful.
2. Generate at most one purposeful static candidate at a time and preserve its
   untouched export in the ignored cache.
3. Reconstruct or retopologize only to meet the brief-specific topology,
   silhouette, UV, material, and runtime budgets. Do not apply the humanoid
   Quad-10k setting by default.
4. Normalize dimensions, axes, pivot or mounting plane, materials, UVs, and
   collision ownership in Blender.
5. Export and fresh-import the exact static GLB, then inspect it in Godot at
   the required camera distances and lighting conditions.

Skip T-pose preparation, Mixamo, marker placement, armatures, skin weights,
motion clips, and deformation review. Keep a validated static source and GLB
unless a human rejects it or an approved revision supersedes it.

## Simple machines

Use Tripo only for the static silhouette. Prefer floating or stationary rigid
chassis. Blender authors the minimum pivots for aim, recoil, hover, impact, and
shutdown. Do not accept legs, organic deformation, or complex machine rigs.

## Approval checkpoints

Humanoids:

1. Static Tripo source.
2. Quad 10k topology in Blender.
3. Human-approved Mixamo marker placement and Auto-Rigger preview.
4. Rig deformation after Blender correction.
5. Animation playback on the corrected rig.
6. Exact GLB import and assembly in Godot.

Static props and environment assemblies:

1. Static source silhouette and completeness.
2. Brief-specific geometry, topology, materials, UVs, dimensions, and pivot.
3. Exact static GLB import and contextual Godot review.
4. Human visual approval before live replacement.

Simple rigid machines:

1. Static chassis and separable moving parts.
2. Minimum Blender pivot hierarchy.
3. Transform playback plus exact GLB review in Godot.
4. Human visual and motion approval before live replacement.

One representative screenshot per checkpoint is enough when a frozen image is
useful. Keep it under ignored `artifacts/`; the durable record is the concise
text decision and structural metrics.

Stop and reject the active branch when identity, topology, joints, deformation,
weapon clearance, animation quality, licensing, or privacy cannot be repaired
without redesign. Remove superseded models and failed animation experiments
from the active baseline rather than preserving them as instructions.
