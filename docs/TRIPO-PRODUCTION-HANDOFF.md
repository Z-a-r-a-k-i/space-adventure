# Tripo production handoff

Status: approved operating procedure; offline art authorization is
brief-scoped and gameplay integration remains phase-scoped

Revision: 2026-07-24

## Purpose

This is the operational handoff for an agent using the signed-in Tripo Studio
account on the dedicated art workstation. It covers approved-reference input
preparation, candidate generation, segmentation, retopology, textures, rigging,
animation donors, Blender finalization, Godot review, evidence, and reporting.

It does not replace:

- `ROADMAP.md` for gameplay-phase authorization and integration;
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
   offline source production only when the approved roster asset has an
   accepted production-ready art brief, assigned ownership, and resolved
   licensing and privacy prerequisites. The owning gameplay phase does not
   need to be active for this offline lane. Live greybox replacement,
   gameplay-coupled finalization, and integration remain phase-scoped. The
   bounded generator bake-off is a separate explicit authorization.
3. Treat every Tripo mesh, material, skeleton, weight, animation, pivot, scale,
   name, and export as untrusted input until Blender and Godot review pass.
4. Preserve the raw candidate unchanged in the ignored run-local workstation
   cache before editing, segmenting, remeshing, rigging, or converting it.
   Commit its provider/task reference, path, size, and provenance manifest
   rather than the raw payload. Do not content-hash the raw or another large 3D
   binary.
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
- assigned agent, dedicated art branch/worktree, owned asset ID, and exact
  writable repository paths;
- intended candidate class: static prop, handheld weapon, humanoid, body-source
  machine, or integrated-weapon machine;
- required scale, forward direction, material regions, moving parts, sockets,
  rig profile, animation coverage, and tactical-camera review distance;
- current Tripo plan tier, privacy setting, and commercial-use status; and
- one unambiguous production run ID:
  `<asset-id>__prod__tripo__<live-model-id>__<yyyy-mm-dd>__<nn>`; or
- for the bounded experiment only, one bake-off run ID:
  `<asset-id>__bakeoff__tripo__<live-model-id>__<yyyy-mm-dd>__<nn>`.

If the brief, applicable authorization, licensing, privacy, or ownership is
missing, stop and report the missing prerequisite. Do not use a convenient
default.

An accepted production brief records status
`accepted for offline source production`, approval date, approver, authorized
asset ID and operations, phase-blocked gameplay fields, production owner, and
dedicated worktree. Approval comes from the project owner or an art owner whose
delegation is recorded in a versioned task or decision. Brief authorship or
asset assignment is not approval, and the production agent cannot self-approve
without that explicit delegation.

The art worktree must be separate from every active gameplay worktree. Follow
`AGENT-AUTOMATION.md`: use worktree-local Godot user data, logs, editor state,
and a distinct MCP port. During offline production, do not edit
`game/project.godot`, live gameplay scenes, shared content schemas, or imported-
asset registries. Keep staged GLBs and the local review project under
`artifacts/reviews/<asset-id>/<asset-revision>/` and
`artifacts/godot-asset-gallery/<worktree-id>/`. Those ignored paths are review
staging, not publication. Moving a GLB to `game/Assets/Published/` or adding
shared gallery infrastructure requires a separately assigned integration owner.
For a Tripo-backed asset, `<asset-revision>` is exactly the repository `run_id`;
the Tripo `task_id` is a separate field used for provider recovery.

The provider-comparison work authorized before the Phase 2 human-playthrough
gate closes is the bounded bake-off for:

- `prop.station.service_terminal.v1`;
- `prop.station.wall_utility.v1`; and
- `machine.security_drone.body.v1`.

No production-lane work may begin for any of those three IDs until the whole
bake-off's provider results, Blender baselines, scorecards, and final decisions
are complete and frozen. This prevents later refinement from influencing the
controlled comparison.

The approved crew, weapon, machine, prop, and environment sheets are ready for
production-brief preparation. Once an approved roster asset receives an
accepted production-ready art brief, its separate offline source-production
lane may run under ADR 0016 even when its gameplay phase is not active. Use a
`__prod__` run ID, not a `__bakeoff__` run ID, and keep its results out of the
bake-off scorecard.

Before the owning gameplay phase, offline work may include reference crops,
generation, targeted candidate branches, part completion, topology, textures,
provider rig and animation donors, Blender finalization, staged GLBs, and the
isolated Godot asset gallery. It may not include:

- replacing content in the live route or encounter;
- gameplay scene wiring or stable gameplay definitions;
- ability-specific props, clips, icons, or effects;
- final attack timing, telegraph, hit, damage, or target-shape synchronization;
  or
- work whose unresolved gameplay dependency would force the agent to invent a
  mechanic.

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
6. Generate an initial candidate and inspect every view before choosing the
   next operation. Create a targeted alternative only for a named defect, such
   as fused legs, missing back geometry, wrong weapon silhouette, or unusable
   asymmetry. One to three raw candidates will often be enough, but production
   work has no general hard candidate cap.
7. Preserve the untouched result in the ignored run-local cache and preserve
   its review evidence before any later Studio operation. Use Studio history's
   **Save as New Version** before a destructive branch when available.
8. Select one promising source for active cleanup at a time. Invest
   subscription credits in completing that source properly: useful
   segmentation, part completion, topology or texture branches, rig
   diagnostics, and animation donors. Do not stop merely because the first
   viable candidate exists, and do not make candidate count a goal.
9. For every charged operation, record the purpose, settings, task or version
   ID, visible credit cost, result, and keep/reject decision. Do not repeat an
   unchanged failed operation or retain an unreviewed branch merely to consume
   credits.

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
6. Download and record a static GLB with materials and textures before using
   Auto Rig. Record its task/run reference, path, filename, export settings,
   and byte size without computing a content hash. Keep any additional OBJ or
   FBX only when it provides information the GLB does not.
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
   library. Once retargeting is proven, use additional provider credits
   freely for donor variants that address named coverage, deformation,
   silhouette, or timing needs.
4. Request in-place motion where Studio exposes it. Root translation from a
   provider clip is not gameplay authority.
5. Export the rigged base plus useful preset clips as GLB or FBX and record
   every filename, task/version reference, export setting, path, and byte size.
   Do not separately hash those large binary files. Preserve the pre-rig static
   export beside them.

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

For each Tripo run, commit:

```text
art/generated/<asset-id>/<run-id>/
  input/
    prompt.txt
    <lossless-view-crops>
  evidence/
    settings/
    studio-review/
  raw-export.manifest.json
  metadata.md
  selection.md
```

Retain the provider payload locally at the matching ignored cache path:

```text
art/generated/<asset-id>/<run-id>/raw/
  <untouched-static-export>
  <optional-rigged-or-animation-donor-exports>
```

`metadata.md` records:

- asset and run IDs;
- lane: `production` or `bakeoff`;
- UTC date;
- Tripo Studio plan and privacy state;
- exact live model, settings, task ID, and visible credit use;
- input filenames and SHA-256 hashes;
- output filenames, byte sizes, and provider task/version references;
- every Studio operation and version branch;
- export formats;
- source-image ownership and provider licensing URLs with retrieval date; and
- known defects and the next intended Blender operation.

New or updated `raw-export.manifest.json` files use schema version 2 and record
top-level `run_id` and provider `task_id` fields plus one entry for every cached
payload. Each entry includes its expected cache-relative path, original
filename, byte size, export format and settings,
`local_presence_checked_utc`, and `local_presence_status` (`present` or
`missing`). They omit binary content hashes. The manifest also records credit
use and plan/privacy/licensing state. It records a non-secret off-machine
archive locator and restore check when such storage is introduced. Existing
schema-version-1 hash fields are historical and are not recomputed. Do not
record a private Studio URL, session URL, cookie, token, account identifier,
or temporary download link.

The raw cache is hydrated manually from a locally retained export, Tripo
Studio, or a future private archive. Before Blender uses it, verify that the
manifested path exists and compare its byte size. Do not read the whole file
merely to calculate or verify a hash. A missing cache entry is a clear
source-hydration requirement, not permission to generate a substitute. An
unexpected size requires manual confirmation against the trusted local copy or
provider task. Tripo Studio history is a best-effort recovery source: its
advertised storage features are not a durability guarantee. Keep each local
raw export until an off-machine copy is checked or the project owner explicitly
approves eviction.

Presence and size establish cache availability only. Same-size corruption or
substitution can pass those checks. If corruption is suspected or an
integrity-sensitive restore is required, hydrate from the exact Tripo task or a
trusted local/private-archive copy and run the normal structural Blender and
glTF validation gates. Do not hash the large binary merely to compare it.

Normal clones, CI, game builds, runtime imports, and published assets must be
self-contained and must not require Tripo access or the raw cache.

Accepted editable sources later move to `art/source/<asset-id>/`. Published GLBs
move to `game/Assets/Published/` only after the normal validation and approval
gates. Review evidence belongs under
`artifacts/reviews/<asset-id>/<asset-revision>/`. Existing hash-keyed review
directories remain valid historical evidence and do not need renaming.
Provider-backed review manifests and findings set `asset_revision` to the exact
repository `run_id` and record the provider `task_id` separately.

## Rejection and stop conditions

Reject or stop when:

- the candidate changes the approved identity, silhouette, weapon class,
  palette, or attack source;
- limbs, joints, armor, grips, muzzle, or moving parts cannot be repaired
  without redesign;
- a character cannot maintain a continuous deforming base under its armor;
- hidden surfaces, non-manifold geometry, paper-thin parts, texture lighting,
  or topology remain unsuitable after reasonable targeted provider and Blender
  branches;
- the complete assembly cannot satisfy grip, support hand, carry, aim, recoil,
  or attack clearance;
- the rig cannot survive required stress poses;
- the asset does not read at the 20 m tactical view;
- licensing, privacy, provenance, or provider version is unclear; or
- the applicable authorization, brief, or asset ownership changes.

Report the named failure and preserve evidence. Reject the failing branch, not
the quality goal: switch to a targeted alternative or to Blender when that is
the better repair path. Stop when the asset passes its applicable reviews, the
provider cannot improve the named defect, or the next unresolved step belongs
to a later gameplay phase. This prevents unproductive repetition; it does not
require stopping after the first viable result or conserving prepaid monthly
credits.

## Recommended execution order

When the relevant production briefs are accepted, perform offline source work
in this order. Any gameplay-coupled step still waits for its owning roadmap
phase:

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
6. Generate the production-machine forms and prove the required articulation
   after their offline art briefs accept the roster's body-ram and integrated-
   gun source classes. Final attack clips, timing, telegraph synchronization,
   and live integration wait for their Phase 4 attacks.
7. Author the exact station structure and airlock in Blender when their
   production briefs are accepted; use Tripo only for explicitly permitted
   decorative candidates. Live route replacement and airlock gameplay
   integration wait for Phase 5.

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

Retrieved 2026-07-24:

- [Tripo Terms of User Agreement](https://www.tripo3d.ai/terms) states that
  Tripo has no obligation to store inputs or outputs, may impose storage
  limits, and may delete account content when service access terminates.
- [Tripo pricing](https://www.tripo3d.ai/pricing) advertises plan-dependent
  model storage and edit history. Those product features do not override the
  storage disclaimers in the terms.
