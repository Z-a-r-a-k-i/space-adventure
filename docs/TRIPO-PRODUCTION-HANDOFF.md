# Tripo production handoff

Status: approved operating procedure; work authorization remains phase-scoped

Revision: 2026-07-23

## Purpose

This is the operational handoff for an agent using the signed-in Tripo Studio
account on the dedicated art workstation. It covers approved-reference input
preparation, candidate generation, segmentation, retopology, textures, rigging,
animation donors, Blender finalization, Godot review, evidence, and reporting.

It does not replace:

- `ROADMAP.md` for phase authorization;
- `POC-ASSET-ROSTER.md` for the bounded asset inventory and animation coverage;
- `ART-PIPELINE.md` for publication and review;
- `ATTACK-PRESENTATION.md` for combatant, weapon, marker, and clip contracts;
- `ART-WORKSTATION.md` for machine setup; or
- the production-ready brief for the active asset ID.

If these sources disagree, stop and report the conflict. Do not resolve it by
spending credits or inventing content.

## Non-negotiable rules

1. Use the signed-in **Tripo Studio web application**, controlled through the
   existing Chrome session. The subscription-credit workflow is the baseline;
   do not use the Tripo API, store an API key, or add an API integration.
2. An approved image is a visual anchor, not a production-ready brief. Begin
   generation only when the owning roadmap phase is active and the asset has an
   accepted brief, unless the asset is explicitly inside the current bounded
   generator bake-off.
3. Treat every Tripo mesh, material, skeleton, weight, animation, pivot, scale,
   name, and export as untrusted input until Blender and Godot review pass.
4. Preserve the raw candidate before editing, segmenting, remeshing, rigging,
   or converting it.
5. Complete every mesh-changing operation before final rigging. Segmentation,
   part completion, Smart Low-Poly, quad conversion, and other remeshing
   reconstruct geometry and invalidate skeletal data.
6. The four humans use one normalized Blender-owned humanoid skeleton and one
   reusable animation library. Never accept independent generated skeletons as
   four production rigs.
7. Each POC human has one fixed complete runtime outfit. Named undersuit and
   armor source objects are production seams, not gameplay equipment slots.
8. Party firearms remain separate assets. Do not generate a weapon fused into
   a character, bake it into a character animation export, or infer weapon
   switching, ammunition, reload, or inventory mechanics.
9. Do not place collision, navigation, hit volumes, damage volumes,
   telegraphs, muzzle flashes, tracers, projectiles, selection effects, or
   gameplay state inside provider exports.
10. Never store account details, authentication cookies, billing data, API
    keys, private Studio URLs, or other secrets in the repository.

## Preflight gate

Before opening Tripo for an asset, record all of the following:

- stable asset ID and owning phase;
- accepted production brief or explicit bake-off authorization;
- approved reference-sheet filename and SHA-256;
- assigned agent and worktree or path ownership;
- intended candidate class: static prop, handheld weapon, humanoid, body-source
  machine, or integrated-weapon machine;
- required scale, forward direction, material regions, moving parts, sockets,
  rig profile, animation coverage, and tactical-camera review distance;
- current Tripo plan tier, privacy setting, and commercial-use status; and
- one run ID:
  `<asset-id>__tripo__<live-model-id>__<yyyy-mm-dd>__<nn>`.

If the brief, phase authorization, licensing, privacy, or ownership is missing,
stop and report the missing prerequisite. Do not use a convenient default.

The only Tripo work authorized before the Phase 2 human-playthrough gate closes
is the bounded bake-off for:

- `prop.station.service_terminal.v1`;
- `prop.station.wall_utility.v1`; and
- `machine.security_drone.body.v1`.

The approved crew and weapon sheets are ready for production-brief preparation,
but their Phase 3 generation is not authorized by visual approval alone.

## Prepare provider inputs

Keep the approved full sheet unchanged. Create lossless provider-input crops in
the run directory:

- humanoid: front, strict side, back, and three-quarter views;
- handheld weapon: side, top, and three-quarter views; retain the muzzle view
  primarily for validation and include it as a fourth input only when the live
  multi-view workflow maps it correctly;
- prop or machine: the brief-selected front, side, back, and three-quarter
  views.

Submit two to four views of the same object as one **multi-view** generation
when Studio exposes that mode. Do not use Batch 3D Gen for the panels of one
turnaround: batch mode produces independent models, while multi-view gives one
model evidence from several angles.

Remove the sheet divider and empty margins from each provider crop. A weapon
crop must exclude the neutral hand or forearm scale silhouette so it is not
reconstructed as geometry. Do not repaint or redesign the approved subject;
any semantic change needs a new approved reference.

Copy the accepted brief prompt into a plain-text input record. It may clarify
that the subject is a single isolated model in a neutral pose, but it must not
invent accessories, weapons, abilities, logos, or lore. Hash the prompt and
every input crop before upload.

## Tripo Studio generation procedure

1. Open the live model-generation workspace and confirm the signed-in plan,
   privacy state, and displayed credit cost.
2. Select **Build & Refine**, not One-Click, when the asset needs controlled
   parts, topology, or a later rig.
3. Choose image-to-3D multi-view and upload the prepared crops as one model.
4. Enable **Generate in Parts** only when the part policy below benefits from
   it. Record the exact live generation model, quality, topology, texture, PBR,
   part, seed or retry, and other visible settings; marketing model names in
   this repository are not substitutes for live identifiers.
5. Capture the settings and task or model identifier before generation.
6. Generate one candidate and inspect every view before retrying. Use a retry
   only for a named defect, such as fused legs, missing back geometry, wrong
   weapon silhouette, or unusable asymmetry. Record the reason and credits.
7. Preserve the untouched result and its render before any later Studio
   operation. Use Studio history's **Save as New Version** before a destructive
   branch when available.
8. Select at most one candidate for cleanup at a time. Available credits are
   not a reason to continue after a viable candidate or to retain unreviewed
   variants.

## Part policy by asset class

### Humanoids

Target one continuous deforming undersuit or body surface plus separable rigid
armor and major accessories:

- head and hair may remain separate;
- torso, shoulder, forearm, knee or shin armor, boots, gloves, pouches, sensor
  modules, and carry hardware may remain named objects where useful;
- left and right rigid pieces remain distinguishable;
- anatomical arms, forearms, thighs, calves, elbows, and knees must not remain
  independent watertight limb chunks at deforming joints; merge or rebuild
  them into the continuous deforming base before rigging;
- mask or omit undersuit polygons completely hidden beneath rigid armor when
  needed to prevent clipping; and
- never generate the party firearm as part of the humanoid.

Start with Balanced segmentation when generation did not already produce useful
parts. Use Detailed only for a specific boundary problem. Adjust and merge
segments deliberately. Use part completion on a rigid armor or accessory piece
when its hidden side is genuinely needed; do not complete artificial elbow,
knee, shoulder, or hip cuts that should deform continuously.

### Handheld weapons

Weapons are rigid assets and receive no humanoid rig:

- retain receiver, stock, primary grip, support grip, barrel or muzzle, and
  genuinely moving parts as logical objects when useful;
- keep the Protector shotgun pump separable if the final presentation animates
  it;
- do not create loose ammunition, detachable magazines, scopes, slings, or
  extra attachments absent from the approved sheet and brief; and
- preserve one coherent watertight exterior with a readable muzzle and no
  paper-thin plates.

### Machines

Generate the complete production silhouette, not an isolated decorative shell,
when the production brief is active:

- the ram drone needs a reinforced forward contact mass and deforming or rigid
  articulation for brace, strike, rebound, and recovery;
- the gun sentry needs an integrated gun with an unambiguous muzzle, aim
  clearance, and recoil articulation; and
- a provider-generated machine rig is only a diagnostic proposal. Blender owns
  the final machine rig and marker hierarchy.

The current `machine.security_drone.body.v1` remains a disposable body-only
bake-off candidate and must not be promoted into the production ram drone.

### Props and structure

Static props may use segmentation to separate real moving or material parts.
Station structure, airlock clearance, collision interfaces, pivots, and modular
dimensions are authored in Blender and Godot, not inferred from Tripo.

## Mesh, texture, and static export order

For every selected candidate:

1. Review and correct segmentation.
2. Run part completion only where the final object genuinely needs completed
   hidden surfaces.
3. Apply Smart Low-Poly, quad or triangle retopology, target polygon count, and
   any other remesh while the asset is still static.
4. Inspect silhouette, holes, disconnected fragments, normals, joints, armor
   clearance, and moving-part clearance.
5. Generate or repair textures only after topology and parts are stable.
   Preserve palette regions and remove baked lighting; tiny surface detail must
   not carry gameplay readability.
6. Download and hash a static GLB with materials and textures before using Auto
   Rig. Keep any additional OBJ or FBX only when it provides information the
   GLB does not.
7. Import a copy into Blender. Never publish directly from Tripo or a provider
   bridge into Godot.

If a mesh operation is performed after rigging, discard that rigged derivative
and restart rigging from the resulting static mesh.

## Rig and animation workflow

Rigging starts only after model approval and after topology, parts, fitting,
UVs, and textures are stable.

### Tripo's role

For a compatible neutral-pose humanoid or creature candidate:

1. Run the available rig check or inspect obvious prerequisites: separated
   limbs, no fused hands or legs, readable shoulders, elbows, hips, knees, and
   feet.
2. Run **Auto Rig** and record the displayed rig model or version, rig type,
   skeleton specification, task ID, and credits.
3. Apply a small diagnostic set first: neutral idle, walk, run, shoot or attack,
   hurt, fall or down, and turn when those presets exist in the live Studio
   library.
4. Request in-place motion where Studio exposes it. Root translation from a
   provider clip is not gameplay authority.
5. Export the rigged base plus useful preset clips as GLB or FBX and hash every
   file. Preserve the pre-rig static export beside them.

Tripo's skeleton, skin weights, and preset clips are donor or diagnostic inputs.
Do not repeat animation generation across all four humans until one donor
skeleton and clip survive Blender retargeting onto the shared project rig.
Generic `shoot` motion is a starting reference, not an accepted carbine,
pistol, or shotgun animation.

### Blender's role

Blender owns the production result:

1. Normalize scale, origin, neutral pose, object names, materials, and the
   shared humanoid skeleton in Blender's native Z-up authoring space. Configure
   the glTF export conversion so the published GLB frames are `+Y` up and `-Z`
   forward; do not rotate source rigs into glTF coordinates by hand. Re-import
   or inspect the exact exported GLB to verify the converted orientation.
2. Fit the continuous undersuit and every fixed-outfit mesh to that skeleton;
   repair weights at shoulders, elbows, wrists, hips, knees, ankles, neck, and
   armor boundaries.
3. Retarget useful Tripo donor clips to the shared skeleton. Extract,
   rename, trim, loop, clean foot sliding, remove unwanted root motion, and
   repair deformation in Blender.
4. Use a separate non-published weapon proxy and constraints to author or
   correct hand placement. The runtime weapon stays a separate scene.
5. Author or substantially correct the clips Tripo cannot satisfy:
   draw, deterministic attachment transfer, holster, weapon-specific
   raise/aim, two-hand support, fire/recoil/recovery, dialogue gestures,
   terminal interaction, healing use, ram contact, integrated-gun aim/recoil,
   and machine shutdown.
6. Do not create ability-specific clips until the corresponding ability,
   target shape, source, and timing are accepted.

The complete required coverage is the matrix in `POC-ASSET-ROSTER.md`.
Combat clips are in-place. Gameplay owns movement, attack phases, resolution,
damage, interruption, and cooldowns. Animation playback presents observed state
and freezes with tactical pause; animation callbacks never apply damage.

### Attachment and attack interfaces

Use the interfaces in `ATTACK-PRESENTATION.md`:

- character: `socket.weapon.hand_primary` and
  `socket.weapon.holster_primary`;
- handheld weapon: `socket.grip.primary`, optional
  `socket.grip.support`, and `socket.attack.muzzle.primary`;
- integrated weapon: `socket.attack.muzzle.primary` plus brief-specific aim
  and recoil joints; and
- body attacker: `socket.attack.contact.primary`.

Weapon roots coincide with `socket.grip.primary`. Muzzle and contact frames
point local `-Z` outward with local `+Y` up. Validate the exact character and
weapon assembly; a plausible character and plausible weapon can still fail
together.

## Checkpoints and approval

Do not run the entire pipeline invisibly. Deliver these checkpoints:

1. **Raw-candidate review:** Studio turntable or multi-angle captures, settings,
   credit use, task ID, input hashes, and a decision of keep, retry, or reject.
2. **Static Blender review:** normalized bounds, topology, named parts,
   materials, wireframe, and tactical-camera silhouette.
3. **Assembly review:** hands, holster or back mount, support grip, muzzle,
   weapon clearance, machine attack source, and scale.
4. **Rig review:** skeleton hierarchy, weights, deformation stress poses,
   moving parts, and required markers.
5. **Animation review:** full-speed and stepped wind-up, release or contact,
   recovery, draw and holster transfer, loops, root behavior, and clipping.
6. **Godot review:** import the exact published GLB or GLBs into the asset
   gallery, bind the reviewed presentation profile, and play the complete
   assembly against a target dummy. For handheld profiles, exercise draw and
   holster attachment transfer through pause, resume, scenario reset, and
   animation resynchronization. Inspect wind-up, release or contact, recovery,
   weapon or body clearance, and attack direction at 14.5 m and 20 m.

Prominent characters, combatants, hero weapons, and environments require human
approval at their applicable checkpoints. A 2D concept approval does not
pre-approve the 3D candidate, rig, or animations.

## Repository deliverables

For each Tripo run, retain:

```text
art/generated/<asset-id>/<run-id>/
  input/
    prompt.txt
    <lossless-view-crops>
  raw/
    <untouched-static-export>
    <optional-rigged-or-animation-donor-exports>
  evidence/
    settings/
    studio-review/
  metadata.md
  selection.md
```

`metadata.md` records:

- asset and run IDs;
- UTC date;
- Tripo Studio plan and privacy state;
- exact live model, settings, task ID, and visible credit use;
- input filenames and SHA-256 hashes;
- output filenames and SHA-256 hashes;
- every Studio operation and version branch;
- export formats;
- source-image ownership and provider licensing URLs with retrieval date; and
- known defects and the next intended Blender operation.

Accepted editable sources later move to `art/source/<asset-id>/`. Published GLBs
move to `game/Assets/Published/` only after the normal validation and approval
gates. Review evidence belongs under
`artifacts/reviews/<asset-id>/<asset-hash>/`.

## Rejection and stop conditions

Reject or stop when:

- the candidate changes the approved identity, silhouette, weapon class,
  palette, or attack source;
- limbs, joints, armor, grips, muzzle, or moving parts cannot be repaired
  without redesign;
- a character cannot maintain a continuous deforming base under its armor;
- hidden surfaces, non-manifold geometry, paper-thin parts, texture lighting,
  or topology remain unsuitable after the brief's cleanup cap;
- the complete assembly cannot satisfy grip, support hand, carry, aim, recoil,
  or attack clearance;
- the rig cannot survive required stress poses;
- the asset does not read at the 20 m tactical view;
- licensing, privacy, provenance, or provider version is unclear; or
- the owning phase, brief, or asset ownership changes.

Report the named failure and preserve evidence. Do not spend credits indefinitely
trying to rescue a candidate that violates the approved design.

## Recommended execution order

When the relevant phases and briefs are active:

1. Prove the current bounded bake-off separately; it does not create production
   combatants.
2. Generate and approve the static Vanguard and carbine, then fit their complete
   assembly.
3. Establish the shared humanoid skeleton and prove one Tripo donor clip can be
   retargeted, cleaned, exported, and imported into Godot.
4. Generate the Operator and pistol, then the Protector and shotgun; fit each
   complete assembly before producing their final weapon-specific clips.
5. Add the survivor on the same skeleton after its reference and brief are
   approved.
6. Generate and rig the production machines only after their Phase 4 attacks
   and briefs are accepted.
7. Author the exact station structure and airlock in Blender during Phase 5;
   use Tripo only for the explicitly permitted decorative candidates.

Never generate all humanoid animations before the first complete
character-plus-weapon assembly and Godot retarget proof succeeds.

## Verified Tripo references

Retrieved 2026-07-23:

- [Tripo Studio tutorial](https://www.tripo3d.ai/blog/tripo-studio-tutorial-english)
  documents Build & Refine, Generate in Parts, segmentation, retopology,
  texture, Auto Rig, preset animation, and GLB/FBX export.
- [Using multiple reference images](https://www.tripo3d.ai/blog/multiple-reference-images-3d-generation)
  recommends two to four images for Tripo multi-view generation.
- [Segmentation v2](https://www.tripo3d.ai/blog/tripo-segmentation-v2)
  documents precision tiers, editable part boundaries, and completion of open
  cut surfaces.
- [Tripo animation documentation](https://platform.tripo3d.ai/docs/animation)
  states that segmentation, mesh completion, Smart Low-Poly, and remeshing
  strip skeletal rig data and recommends completing them before rigging and
  retargeting.
- [Tripo auto-rig workflow](https://www.tripo3d.ai/blog/how-to-rig-an-ai-generated-character)
  recommends a clean neutral mesh, joint preview, and export to Blender or an
  engine for further correction.
