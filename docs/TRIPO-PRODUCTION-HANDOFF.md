# Tripo, Mixamo, Blender handoff

Status: current class-specific production procedure

## Preflight

Before opening Tripo, confirm the active asset has:

- an approved roster ID, visual reference, and production brief;
- a named human approver and dedicated art worktree;
- resolved licensing and privacy state; and
- no unresolved design choice that would change the model.

Use the signed-in Tripo Studio web application. Do not add a Tripo API key or
provider-to-Godot bridge.

## Generate the static source

1. Prepare clean reference views and a prompt for one isolated, unrigged
   T-pose humanoid. Keep weapons separate.
2. Use Build & Refine when topology control is needed.
3. Generate one candidate and inspect it live. Generate another only for a
   named defect that cannot be repaired.
4. Preserve the selected untouched export in the ignored run-local cache and
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
5. Export a static GLB and an FBX-with-textures ZIP.

Tripo's polygon target is not an exact result guarantee. If any mesh operation
occurs after rigging, discard that rig and restart from the new static mesh.
Do not run Tripo Auto Rig for production humanoids.

## Mixamo Auto-Rigger

1. Upload the FBX-with-textures ZIP.
2. Position the chin, wrists, elbows, knees, and groin/hip marker.
3. Stop and ask the assigned human to confirm every marker before continuing.
4. Inspect the rig preview for head/neck, shoulder, wrist, hip, knee, ankle,
   hand, and armor deformation.
5. Download the accepted neutral FBX Binary with skin.

Mixamo is a baseline, not final authority. Visible deformation defects return
to Blender for weight or joint repair.

## Mixamo animation library

Use Mixamo's existing library as the default humanoid motion source. Review a
candidate clip at real speed before cleanup. After the corrected rig is
accepted, download clips without skin for the same skeleton and prefer in-place
variants.

Blender owns retargeting where required, trimming, looping, root handling,
contact cleanup, hand and weapon constraints, action names, transfer markers,
and final export. A technically valid clip is rejected if its weight transfer,
foot plants, cadence, counter-motion, or action intent looks wrong.

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
3. Human Mixamo marker placement.
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
