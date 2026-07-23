# Tripo free-tier smoke test — July 2026

## Purpose

Run one deliberately disposable, non-production image-to-3D generation before
considering a paid Tripo plan. The test object is a generic hard-surface
quadruped maintenance drone called the **Utility Walker Test Unit**.

This test is separate from the scored three-asset SpaceAdventure generator
bake-off. It must not be treated as a production design or imported into the
game without a separate licensing and art review.

The later head-bump idea is useful attack-source exploration, not a change to
this test's status. A production walker inspired by this silhouette would need
a new complete-machine brief under `docs/ATTACK-PRESENTATION.md`, including a
reinforced front contact surface, marker, rig, wind-up, strike, and
recovery. The sensor lens itself must not be treated as the impact surface.

## Why this input

The reference stresses several common image-to-3D failure modes in one model:

- a rectangular hard-surface hull tests straight edges and planar surfaces;
- three visible articulated legs plus one intentionally occluded far-side leg
  test symmetry and hidden-side completion;
- open joints, pistons, top rails, and the antenna test thin-part survival;
- the raised body and underslung equipment test underside completion;
- red front, cyan rear, and orange side markers test texture orientation;
- the separated feet make fused geometry and extra-limb errors easy to spot.

## Free-plan boundary

The live account showed **200 credits** before submission and quoted **55
credits** for the current single-image model generation.

The authenticated API wallet was checked separately and showed **0 available
credits** and **0 frozen credits**. Tripo documents the Studio/webapp and API as
independent billing systems, so this test cannot use the API without a separate
API purchase. No API upload or generation was submitted.

Tripo's live pricing page describes free-plan models as public under CC BY 4.0,
and Tripo's Terms grant broad rights over free-user inputs and outputs. For that
reason:

- upload only the disposable reference in this folder;
- do not upload SpaceAdventure key art, approved reference sheets, or lore;
- do not pay, upgrade, or start a second generation during this smoke test;
- export or capture the result promptly because free edit history is short;
- if the pipeline passes, regenerate any future accepted production design
  after moving to a private paid plan.

Official references:

- <https://www.tripo3d.ai/pricing>
- <https://www.tripo3d.ai/terms>
- <https://platform.tripo3d.ai/docs/faq>
- <https://platform.tripo3d.ai/docs/wallet>

## Planned run

1. Upload `utility-walker-reference-v1.png` as a single image.
2. Confirm the account still shows the free tier and the quoted credit cost.
3. Generate one raw model with no retries.
4. If available under the one-day trial, run Smart Mesh once using triangles and
   a target near 12,000 triangles; otherwise preserve the raw generated mesh.
5. Download the available GLB immediately.
6. Inspect front, rear, underside, wireframe, topology, UVs, and PBR maps in
   Blender before considering Godot import.

## Acceptance rubric

Score each category 0–2.

| Category | 2 — pass | 1 — repairable | 0 — fail |
| --- | --- | --- | --- |
| 360° coherence | Four coherent legs/feet, one front lens, two rear vents | One small artifact | Missing/extra limb or melted hidden side |
| Symmetry and joints | Mirrored paired legs and open major joints | Minor mismatch | Fused or twisted major segment |
| Thin parts | Antenna, rails, and pistons survive as closed volumes | One easy replacement | Most vanish, fuse, or become sheets |
| Underside | Closed hull with distinct underslung equipment | Plain but closed | Hole, inversion, or major collapse |
| Topology | Sensible density, normals, and manifold state | Local cleanup only | Retopology or major reconstruction required |
| Texture/export | GLB, UVs, and maps work; orientation colors stay correct | Minor seam or reconnect | Missing maps/UVs or major smearing |

Decision:

- **10–12**, no zero, and at most 30 minutes cleanup: useful for generated
  background props.
- **7–9** or 30–90 minutes cleanup: concept/base mesh only.
- **0–6**, any major zero, or more than 20% silhouette reconstruction: reject
  the pipeline for production assets.
