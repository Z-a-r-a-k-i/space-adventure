# 3D generator bake-off — July 2026

Status: planned, bounded, and non-gating  
Revision: 2026-07-23  
Scope: visual preproduction only

## Decision question

Can Tripo or Meshy produce an editable starting point for a small stylized
science-fiction prop faster than a Blender-only workflow while still meeting
the project's scale, topology, material, provenance, and tactical-readability
requirements?

This experiment does not select a permanent provider. It may select a useful
workflow per asset category, or conclude that manual Blender authoring is
faster.

## Governing documents

- [Art pipeline and visual review](../../docs/ART-PIPELINE.md)
- [Frontier station visual bible](../bible/frontier-station-v1.md)
- [Service terminal brief](../briefs/station-service-terminal-v1.md)
- [Wall utility brief](../briefs/station-wall-utility-v1.md)
- [Security drone body brief](../briefs/security-drone-body-v1.md)

## Scope

The experiment contains exactly three asset IDs:

1. `prop.station.service_terminal.v1`
2. `prop.station.wall_utility.v1`
3. `machine.security_drone.body.v1`

For each asset, compare:

- Tripo image-to-3D using the current detailed and/or smart-low-poly model
  available to the account at run time;
- Meshy image-to-3D using Meshy 6 and/or the current T2 Smart Topology model
  available at run time; and
- a Blender-only baseline at the same visual target.

Record exact provider model identifiers at run time instead of relying on the
marketing names above. Rodin is deferred to a future hero-asset trial. Local
TRELLIS.2 is excluded because its official hardware requirement exceeds the
current 8 GB GPU. Hunyuan3D 2.1 is excluded pending a separate UK territory and
license review. OpenDesign may help prepare reference sheets, but it is not a
mesh-generation participant.

The experiment does not include characters, rigging, animation, a complete
station module set, direct scene replacement, API integration, or provider
plugins.

The security-drone entry is scored only as a body and topology candidate.
Attack-source readiness, weapon integration, rigging, and animation are
explicitly outside its score; winning the bake-off does not satisfy the
production combatant gate in `docs/ATTACK-PRESENTATION.md`.

Evaluation accounts may be used only when their terms permit the intended
test. Any candidate considered for later acceptance must have documented
commercial-use, attribution, privacy, retention, and training terms acceptable
to the project. A paid plan is not assumed to guarantee those conditions.

## Input preparation

Prepare one provider-neutral input pack per asset:

- front, side, back, and three-quarter views where the subject supports them;
- a clean or transparent background;
- near-orthographic perspective and consistent proportions;
- neutral, even lighting without baked glow or cast shadows;
- no labels, dimensions, watermarks, logos, or UI text inside the subject;
- the same image files and semantic prompt for both providers; and
- SHA-256 hashes for every image and prompt file.

Existing concept images guide the reference sheets but are not sent as
whole-scene image-to-3D inputs. Crop or redraw the individual subject first.

## Bounded run protocol

1. Create a run ID in the form
   `<asset-id>__bakeoff__<provider>__<model>__<yyyy-mm-dd>__<nn>`.
2. Record the input hashes and provider settings before generation.
3. Allow at most two raw generations per provider per asset.
4. Select at most one raw candidate per provider per asset for cleanup.
5. Retain the raw selected candidate unchanged.
6. Normalize a copy in Blender 5.2 LTS.
7. Stop generated-candidate cleanup after 30 minutes of active Blender work.
8. Produce one Blender-only baseline with a 30-minute active-work cap.
9. Export the normalized candidate as GLB through the project publication
   conventions; do not use a direct provider-to-Godot bridge.
10. Validate the GLB, import it into an isolated Godot review scene, and capture
    the default tactical camera plus the 7.5 m and 20 m limits.
11. Complete the scorecard before trying another provider or changing the
    brief.

The maximum is twelve raw cloud generations, six selected generated candidates,
and three Blender baselines. Do not expand the run because one provider fails.
These caps protect the comparison and remain unchanged by ADR 0016. Any
quality-oriented Tripo work beyond them uses an
`<asset-id>__prod__tripo__<model>__<yyyy-mm-dd>__<nn>` run ID, follows an
accepted production-ready art brief, and is excluded from this experiment's
cleanup clock, scorecard, and result. Production-lane work for all three IDs is
blocked until every provider result or documented provider failure, Blender
baseline, scorecard, and final experiment decision is complete and frozen.

## Cleanup clock

Count time spent on:

- import and scene inspection;
- deleting or separating unusable parts;
- symmetry repair, hole repair, and proportion correction;
- retopology or decimation;
- UV repair and material consolidation;
- de-lighting or texture repair;
- pivot, orientation, bounds, and naming normalization; and
- GLB export fixes attributable to the candidate.

Do not count provider queue time, downloads, automated validation runtime, or
standardized capture runtime. Record both elapsed generation time and active
cleanup time separately.

## Hard gates

A candidate is rejected before scoring when any of these is true:

- the source or output license is missing, incompatible, or ambiguous;
- the provider/model/version cannot be identified;
- it cannot reach the brief's bounds and orientation without a redesign;
- critical parts remain fused, missing, non-manifold, or paper-thin after the
  30-minute cleanup cap;
- topology or UVs cannot support the brief's required material regions;
- it exceeds the brief's hard triangle or material limit after cleanup;
- the GLB is malformed or produces unresolved Godot import warnings;
- baked lighting cannot be removed sufficiently for the project's lighting; or
- the asset's role is not readable at the 20 m tactical view.

Raw generated collision, rigs, animations, names, pivots, and scale are never
trusted automatically.

## Scorecard

Score each surviving candidate from 0 to 5 in every category.

| Category | Weight | Evaluation |
|---|---:|---|
| Tactical silhouette and role | 25% | Reads at default and 20 m views |
| Match to the visual bible | 15% | Shape language, palette, detail density |
| Cleanup efficiency | 20% | Useful quality reached within the active-work cap |
| Geometry and editability | 15% | Parts, topology, normals, symmetry, thin surfaces |
| UV and material control | 10% | Clean regions, de-lit color, reusable materials |
| GLB and Godot behavior | 10% | Bounds, pivot, import, shading, file size |
| Provenance and licensing | 5% | Complete, reviewable, commercially usable record |

Record triangle count, vertices, object/mesh/material count, texture count and
resolution, GLB bytes, generation time, cleanup time, validation result, Godot
warnings, provider credits or cost, and reviewer notes alongside the score.

No global winner is required. Tripo may win a machine form while Blender wins a
wall utility. A provider is adopted for a category only when it beats the
Blender baseline in useful active time without failing a hard gate.

## Provenance record

Create a metadata file beside each retained raw candidate containing:

- asset ID and run ID;
- UTC generation date;
- provider and product;
- exact model/version and settings;
- job ID, task ID, and seed when exposed;
- prompt text and prompt SHA-256;
- input filenames and SHA-256 hashes;
- output filenames and SHA-256 hashes;
- account privacy setting and plan tier;
- credits consumed and attributable cost;
- source-image ownership or license;
- provider terms and licensing URLs with retrieval date;
- whether the provider may use uploads or outputs for training;
- download format and any provider-side remesh/retexture stages; and
- all subsequent Blender editing steps.

Never store API keys, authentication cookies, account IDs, personal billing
data, or private provider URLs in the repository.

## Storage and publication

- Raw and normalized candidate sources follow
  `art/generated/<asset-id>/<run-id>/`.
- Editable accepted sources follow `art/source/<asset-id>/`.
- Review captures and manifests follow
  `artifacts/reviews/<asset-id>/<asset-hash>/`.
- Nothing enters `game/Assets/Published/` solely because it won this experiment.
  It must complete the normal asset lifecycle and receive the required review.
- Interaction nodes, collision, navigation, and gameplay stable IDs remain
  authored in Godot wrappers.

## Exit

The experiment is complete when all three asset rows have:

- two provider results or a documented provider failure;
- one Blender-only baseline;
- completed hard-gate and score records;
- tactical-camera captures;
- provenance records; and
- a decision of `tripo`, `meshy`, `blender`, or `none`.

Freeze the completed scorecards, measurements, evidence hashes, baselines, and
decisions before opening a `__prod__` run for any of the three asset IDs.

Completion does not pass the Phase 2 human-playthrough gate, begin Phase 3, or
complete the Phase 6 representative-asset pipeline.
