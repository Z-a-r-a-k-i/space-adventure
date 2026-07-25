# Art pipeline and visual review

## Goal

Produce readable, consistent, and replaceable low-poly game art through a versioned pipeline. AI-generated material is a candidate, never automatically a production asset. Consistency comes from shared proportions, modular kits, rigs, palettes, materials, faction rules, camera constraints, and review—not merely from similar prompts.

The POC begins with Godot primitives and simple authored materials. Blender is opened only for an active asset task.

## Current baseline

- Blender 5.2 LTS is the editable-source and automation baseline.
- The official Blender MCP is installed for interactive agent control and inspection.
- GLB is the published runtime model format.
- Godot 4.7.1 is the final import and tactical-readability check.

The dedicated Windows machine checklist and known-good MCP revisions are
recorded in `ART-WORKSTATION.md`. The approved full-POC target inventory and
phase ownership are recorded in `POC-ASSET-ROSTER.md`. The operational
Studio-to-Blender-to-Godot instructions for the dedicated generation agent are
recorded in `TRIPO-PRODUCTION-HANDOFF.md`.

MCP actions are useful for exploration and review, but repeatable pipeline operations eventually live in versioned Blender scripts or explicit profiles. A successful interactive session is not sufficient provenance.

## Art direction before generation

Before producing a volume of assets, establish a small art bible containing:

- Tactical camera distance, field of view, lighting assumptions, and minimum readable feature size.
- Character height bands, head-to-body proportions, hand and weapon exaggeration, and silhouette rules.
- Faction shape language and forbidden overlaps.
- A controlled palette and reusable material library.
- Surface detail density, bevel language, emissive rules, and damage conventions.
- Modular environment dimensions, grid, door, cover, stair, and corridor standards.
- One approved reference character before producing the humanoid batch.
- One approved environment-module baseline before producing the environment
  batch.

Generation prompts derive from asset briefs and the art bible. They are not the source of truth.

## Asset lifecycle

1. Write an asset brief with a stable asset ID.
2. Generate or model raw candidates and record provenance.
3. Review candidates and select an editable source.
4. Finish segmentation, part completion, retopology or remeshing, fitting, and
   other mesh-changing work.
5. Normalize scale, orientation, origin, naming, materials, sockets, and rig.
6. Run mechanical validation.
7. Publish a GLB into a staging area.
8. Inspect the normalized asset directly in Blender.
9. Import the exact published GLB into the Godot asset gallery.
10. Inspect the exact asset and its animation directly in Godot.
11. Perform structured agent review and, where required, human review.
12. Approve the asset or return it for a named revision.

Direct `.blend` imports are not the controlled runtime publication path.

## Humanoid outfit source workflow

Each POC human publishes one reviewed fixed-outfit assembly. Godot does not
equip separate boots, gloves, leg armor, chest armor, or other clothing items,
and the art hierarchy does not create gameplay equipment slots.

The editable Blender source preserves future options without increasing POC
scope:

- a close-fitting dark navy technical undersuit is the base presentation; an
  anatomically complete hidden body is not required;
- torso, left and right shoulder, forearm, leg or knee, footwear, glove, and
  major accessory pieces remain clearly named objects when practical;
- all outfit pieces use the normalized shared humanoid skeleton;
- body or undersuit polygons fully hidden by rigid armor may be masked or
  omitted to prevent clipping; and
- the published GLB may retain multiple named skinned meshes, but Godot treats
  their reviewed combination as one inseparable outfit.

This supports a future whole-outfit replacement first. Individual equipment
slots, compatibility rules, and inventory semantics require a later gameplay
decision and are not implied by the source-object names. Distinct body
proportions may still require per-character armor fitting.

When a provider exposes part-aware generation, request separated parts or
segment the accepted static candidate. Run segmentation, part completion,
retopology, low-poly conversion, remeshing, and armor fitting before binding to
the shared skeleton: those mesh operations invalidate provider skeletons and
weights. For Tripo runs, preserve the raw candidate unchanged in the ignored
run-local cache. Other providers require their own recorded storage policy.
Preserve each accepted processed version with versioned provenance. Blender
owns the final topology, object boundaries, fit, skeleton, weights,
attachments, and export; provider rigs and animations are disposable
diagnostic inputs.

## Asset brief

Every brief states:

- Asset ID, category, intended gameplay role, dimensions, pivot, forward direction, and sockets.
- Silhouette, proportions, faction shape language, palette, and material IDs.
- Triangle, mesh, material, texture, bone, and animation budgets.
- Rig profile and required clips or key poses.
- Required readability from the tactical gameplay camera.
- Modular connections and collision expectations where relevant.
- References, allowed variations, and forbidden traits.
- Generator, model, version, seed, source files, editing history, and licensing notes.

Offline execution additionally requires a durable approval block in the brief:

- status exactly `accepted for offline source production`;
- approval date and approving project owner or explicitly delegated art owner;
- the authorized asset ID and offline operations;
- unresolved gameplay fields marked `phase-blocked`; and
- the assigned production owner and dedicated branch/worktree.

The brief author or assigned production agent cannot infer or self-grant this
approval. Explicit delegation must be recorded in a versioned task or decision.

A row in `POC-ASSET-ROSTER.md` reserves scope and an asset ID; it is not a
production-ready brief. Offline source production may begin before the owning
gameplay phase only when the asset has an approved visual reference, an
accepted production-ready art brief with every applicable field above,
assigned ownership, and resolved licensing and privacy prerequisites. This
lane may produce review candidates, editable Blender sources, staged GLBs, and
isolated asset-gallery validation. It does not replace live greybox content or
authorize gameplay-coupled finalization and integration before the owning
roadmap phase.

Every production combatant brief additionally follows
`ATTACK-PRESENTATION.md`. Before model generation or rigging it selects a
handheld, integrated, or body-based source for each basic attack and records:

- the stable gameplay attack reference once defined and any separate weapon
  asset ID;
- carried, holstered, ready, aiming, attacking, and recovery states as
  applicable;
- hand, holster, grip, muzzle, `telegraph-origin`, or contact markers as
  applicable to the selected source;
- source-applicable aim, recoil, support-hand, or striking articulation;
- wind-up, release or contact, and recovery landmarks;
- movement and in-place root-motion constraints; and
- the representative complete assembly and tactical-camera views used for
  validation.

An explicitly scoped body-only or topology experiment may omit this section,
but it cannot pass production combatant approval.

Each separate handheld weapon has its own normal asset brief, cross-linked to
compatible rig and combatant profiles. It owns the weapon's bounds, pivot,
budgets, grip, support-grip, muzzle and moving-part markers, and provenance.
Complete-assembly animation and Godot review remain requirements of the
combatant brief.

## Repository shape when real assets begin

All new paths follow `PATH-CONVENTIONS.md`. The stable asset ID appears once in
the hierarchy, while `<run-id>`, revision directories, and evidence filenames
use the compact asset-scoped forms defined there.

```text
art/bible/
art/briefs/
art/kits/
art/materials/
art/rigs/
art/source/<asset-id>/
art/generated/<asset-id>/<run-id>/
art/generated/<asset-id>/<run-id>/raw/  # ignored for Tripo runs
game/Assets/Published/
tools/blender/
artifacts/reviews/<asset-id>/<asset-revision>/
artifacts/godot-asset-gallery/<worktree-id>/
```

The pair of `asset_id` and `asset_revision` is the canonical review join. For a
provider-backed candidate, `asset_revision` is exactly the asset-scoped
repository `run_id`, never the provider `task_id`. Use that `run_id` as the
`<asset-revision>` path component, record it at the top level of
`raw-export.manifest.json`, and include it with `asset_id` in every review
finding. The provider `task_id` remains a separate generation and recovery
reference in the same manifest. Tracked normalized sources and published
outputs retain the provider run's `asset_revision` and additionally record
their repository path and introducing Git commit.

Untouched Tripo exports are not repository deliverables. Download each export
before processing and retain it at the documented run-local `raw/` path on the
dedicated art workstation. Commit a neighboring `raw-export.manifest.json`
with top-level `run_id` and provider `task_id` fields plus one entry per cached
payload. Each entry includes its expected relative path, original filename,
byte size, export settings, `local_presence_checked_utc`, and
`local_presence_status` (`present` or `missing`). Reference the ignored payload
by provider, run ID, task ID, path, filename, and size; these fields prove
availability, not content identity, because same-size corruption or
substitution can pass silently. Do not stream the file merely to compute or
verify a content hash. If integrity is in doubt, restore the exact
provider-task export or another trusted copy and run the normal structural
import and validation checks. New or updated manifests use schema version 2
and omit raw-binary hash fields. Existing schema-version-1 hashes are
historical metadata and are not recomputed. Record privacy/licensing state at
the run level. Tripo recovery is best-effort; it does not replace the local
cache. When an off-machine archive is introduced, add its non-secret locator
and restore-check date to the manifest. Normal clones, CI, game builds, and
runtime publication must not depend on either the cache or Tripo. This policy
does not silently apply to another provider.

Large accepted binary sources use Git LFS if repository size demonstrates the
need. One worker owns one asset ID at a time because binary sources merge
poorly.

Before live integration is authorized, staged GLBs and their isolated review
project remain under the ignored `artifacts/` paths above. Do not create a
gallery scene in `game/`, modify `game/project.godot`, or touch a shared scene
or imported-asset registry for offline review. Moving an approved GLB into
`game/Assets/Published/` and creating reusable gallery infrastructure require
an integration-owner task. Each art worker uses a dedicated branch/worktree,
local Godot user data, logs, and MCP port as defined in
`AGENT-AUTOMATION.md`.

## Mechanical validation

Validation should check:

- Units, scale, bounding box, transforms, forward axis, origin, and ground contact.
- Object, mesh, material, texture, socket, and animation naming.
- Mesh, triangle, material, texture, bone, and influence budgets.
- Missing textures, invalid paths, UV availability, and unexpected embedded data.
- Skeleton hierarchy, bone count, weights, deformation coverage, and root behavior.
- Required clips, clip duration, looping, root motion, and key-pose availability.
- Combatant attack-profile completeness; source-applicable attachment, grip,
  muzzle, and contact markers from `ATTACK-PRESENTATION.md`, including their
  parenting and orientation.
- Hand and holster alignment, two-handed support placement, weapon and body
  clearance, integrated aim/recoil pivots, and body-strike contact placement
  in the exact reviewed assembly.
- GLB structure and Godot import diagnostics.

Record exact tool versions and stable revisions for the source, brief,
normalization profile, published GLB, and render profile. Small tracked text,
configuration, prompt, and reference-image files may retain SHA-256 provenance.
Do not separately hash `.blend`, GLB, FBX, OBJ, provider archives, or other
large 3D binaries: tracked files use their normal Git/LFS revision identity,
while ignored provider payloads use the manifest's provider/task reference,
path, filename, and byte size. Record the repository-relative path and
introducing Git commit for each tracked normalized source and published output.

## AI image generation and visual-inspection efficiency

This policy controls avoidable AI visual work and repository noise. It does
not select or reduce the Codex model, reasoning effort, fast mode, or agent
concurrency; those remain under the user's control.

For concept and reference generation:

- Default to one composite draft or reference sheet per asset decision, not
  four independent high-resolution images. Put compatible views or closely
  related props on the same readable sheet when that does not compromise scale,
  identity, or silhouette judgment.
- When the generation surface exposes quality and size controls, use medium
  quality at approximately 1536 by 1024 for the decision draft. On a surface
  without those controls, request one landscape composite and do not generate
  alternatives by default.
- After the direction is approved, create one high-quality final reference
  sheet when Tripo, Blender, texture work, or close-up review needs more detail.
  If the medium draft is already sufficient, use it as the approved reference
  without regenerating it.
- Extra generations are allowed when they address a named problem, such as
  identity drift, a missing view, an unreadable silhouette, or unusable hand,
  grip, or weapon geometry. Prefer a targeted edit of the selected sheet over a
  fresh set of unrelated alternatives.
- Simple background props and icons normally stop at the accepted medium
  reference. Prominent characters, creatures, weapons, and hero props are the
  strongest candidates for the optional high-quality final.

For agent inspection and user review:

- Concept and reference artwork is durable production input. Inspect the
  selected draft or reference once for the decision it supports; do not reopen
  it merely because another agent takes over.
- Review provider candidates in Tripo Studio's live turntable, topology, and
  material views. Review meshes, attachments, weights, and animation through
  Blender's live viewport and playback. Review import, scale, materials,
  animation, and tactical readability through the live Godot asset gallery.
- Do not build a repository evidence set from Studio screenshots, Blender or
  Godot viewport screenshots, offline renders, contact sheets, turntables, or
  sampled animation frames. They are redundant with the editable asset and
  make reviews slower and more expensive.
- When a frozen image is genuinely needed to diagnose a named defect, create
  the minimum useful crop or capture under `artifacts/`, inspect it once, and
  leave it ignored. Do not promote it into `art/generated/` or Git LFS.
- Do not send a complete render set or every animation frame through model
  vision. Prefer live playback and direct tool inspection. Original-detail
  vision is reserved for a specific defect that cannot be judged reliably in
  the live tool.
- Assign one review owner for an asset revision. Other agents consume the
  textual decision and structural reports. Repeat direct review only after the
  asset changes, for a named unanswered question, or when the project owner
  explicitly requests an independent review.

Record whether an AI image is a decision draft or final reference, its
available quality and dimensions, and the named reason for any extra
generation. This is a default packet of one medium draft plus an optional
high-quality final, not a hard cap.

Each reviewed asset revision may maintain a small `visual-review.md` next to
its metadata. It records:

- `asset_id` and `asset_revision`;
- the tool and live state reviewed;
- the review question, decision, and named defects;
- conclusions later agents may reuse; and
- the asset change or unanswered question that requires another review.

It must not enumerate screenshot paths or hashes. Update the summary before
handoff without generating new images merely to document the review.

Use `ART-EFFICIENCY-LOG.md` for a coarse comparison against the unrestricted
pre-policy baseline. Record one row per asset batch or representative work
period when the information is readily available. Do not reconstruct missing
historical usage or turn the comparison into a per-call accounting task.

The defaults follow OpenAI's current guidance that low quality is intended for
quick drafts and thumbnails, medium or high quality for final assets, and small
targeted revisions are more controllable than broad rewrites. They also account
for current vision behavior in which original detail preserves large image
dimensions and can consume more input tokens. See the official
[image-generation guide](https://developers.openai.com/api/docs/guides/image-generation),
[image and vision guide](https://developers.openai.com/api/docs/guides/images-vision),
and [image creation guidance](https://openai.com/academy/image-generation/)
(checked 2026-07-24).

## Direct asset review

Inspect these views or states interactively in the owning tool:

- Front, back, left, and right.
- Front-left and front-right three-quarter views.
- Top and underside.
- Tactical gameplay camera.
- Wireframe or topology view.
- Scale reference.
- Required animation key poses.
- For combatants: carried or ready, wind-up, release or contact, recoil or
  rebound, and recovery; draw and holster when applicable.

Use a neutral studio, fixed framing, fixed lights, fixed color management, and
an explicit review profile. Inspect the published GLB in both Blender and
Godot: Blender reveals source and topology problems, while Godot reveals
import, material, skeleton, animation, scale, and tactical-readability
problems. Findings record the asset ID, canonical `asset_revision`, provider
`task_id` when applicable, tool/version, severity, and violated brief or
art-bible rule.

### Deterministic in-engine capture

For reproducible animation checks, automation may seek a requested timestamp,
freeze simulation and animation while render-settling frames pass, and verify
the actual position within an explicit tolerance. Record those values in a
small text or JSON report. A local diagnostic capture may be written under
`artifacts/`, but it is not a repository deliverable and does not replace live
playback.

Headless Godot remains valid for import and smoke checks. Visual judgment uses
a graphical editor or game window. Missing animation, timestamp drift, or
render failures must produce a clear structured error and exit promptly rather
than dumping frames or hanging.

## Tool roles

- Blender and Blender MCP: source editing, normalization, rigging, animation
  inspection, and temporary diagnostic renders under `artifacts/`.
- Godot and the existing `godot-ai-plugin`/Godot AI Control integration: import
  diagnostics, live asset-gallery inspection, tactical camera, animation
  playback, and temporary diagnostic viewport capture.
- Khronos glTF Validator: fail malformed or structurally suspicious GLB files before Godot import.
- glTF Transform: inspect, report, optimize, simplify, or transform published GLB files when a concrete publication step needs it.
- ImageMagick: crop or compare a named local diagnostic when a live-tool review
  is insufficient.
- FFmpeg: encode a temporary local animation diagnostic when playback alone is
  insufficient.

The last four tools are useful at the matching pipeline milestone but are not prerequisites for C# gameplay bootstrap or primitive greyboxing.

## Review decision

Structured review covers silhouette, proportions, material coherence, topology, rig deformation, animation readability, scale, tactical readability, blockers, notes, and a decision of `accept`, `revise`, or `reject`.

Combatant review also asks whether the attack source, direction, wind-up,
release or contact, and recovery remain legible at 14.5 m and 20 m. Review
uses the exact character-plus-weapon or complete-machine assembly in Godot.
Reject baked muzzle flashes, projectiles, telegraph shapes, hit volumes, and
generated collision as substitutes for authored presentation and gameplay
integration.

Automated captures prove visibility and comparability, not artistic quality. A human approves prominent production characters, creatures, vehicles, hero props, and environments. Proxy and test assets may use a lighter approval level.

Avoid strict PNG hashes across GPUs. Prefer mechanical checks, configuration hashes, generous perceptual thresholds for gross regressions, and structured visual judgment.

## Early AI use

Initially favor AI for concept exploration, palette and material candidates, modular variations, proxy meshes, texture drafts, and reference boards. Do not assume current text-to-3D output has usable topology, rigs, animation, consistency, scale, or licensing provenance.

Before the Phase 2 exit, the controlled provider-comparison evidence remains
limited to the three bounded bake-off IDs and its fixed attempt caps. Separate
offline source production may proceed for another approved roster asset under
ADR 0016 when its reference, production-ready art brief, ownership, licensing,
and privacy gates pass. That work must use a production run ID and must not be
reported as bake-off evidence. Production and bake-off runs use the explicit
`prod-` and `bake-` prefixes defined in
`TRIPO-PRODUCTION-HANDOFF.md`.

Tripo may provide humanoid-body and handheld-weapon candidates as separate
assets. A humanoid candidate may use provider part generation or segmentation,
but all mesh reconstruction and fitting precedes the shared Blender rig as
defined above. Structural modules remain dimensionally authored in Blender.
Integrated weapons need a readable line of fire and movable clearance; body
attackers need a reinforced contact region and sufficient articulation.
Generated rigs and animations remain untrusted inputs until normalized and
reviewed through the complete attack assembly.

Subscription-credit availability supports thorough completion rather than
large unreviewed candidate pools. Select a promising source early, then use
targeted provider operations and animation donors wherever they improve the
finished asset. Record the provider operation, task or version ID, result, and
keep/reject decision. Capture a displayed operation cost only when it is
already visible and useful; do not track remaining balance, reconcile
historical totals, or use cost as an acceptance criterion. Do not stop solely
because the first viable candidate exists, and do not repeat an unchanged
failed operation merely to consume credits.

Keep the gameplay-facing asset contract stable so improved tools can replace early art one asset ID at a time without changing gameplay code, level logic, or saved state.
