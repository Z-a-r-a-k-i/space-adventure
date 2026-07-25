# AI art efficiency comparison

## Purpose

Use this lightweight log to compare art quality and Codex usage before and
after the visual-efficiency policy in `ART-PIPELINE.md`. The goal is to find a
practical quality-to-usage sweet spot, not to create a precise token-accounting
system.

The unrestricted baseline covers work completed through 2026-07-24. Exact
per-asset usage was not recorded, so do not estimate or reconstruct it. A
period-level usage figure may be added later if the product UI exposes one.

## Work-period log

Record one row per asset batch or representative work period when the values
are readily available.

| Period | Assets or batch | AI drafts | High-quality finals | Extra generations and reason | Source-art vision reviews | Temporary defect captures opened | Approval and rework outcome | Approximate usage and source |
|---|---|---:|---:|---|---:|---:|---|---|
| Through 2026-07-24 | Unrestricted baseline | not recorded | not recorded | not recorded | not recorded | not recorded | Existing approved and revise-required records | Add only if a trustworthy period-level figure is available |

Do not count routine Tripo, Blender, or Godot screenshots because they should
not be generated or sent through model vision. When readily available, note
whether a temporary defect capture was necessary. Do not spend model usage
reconstructing historical counts.

Do not add Tripo account balances or reconcile aggregate Tripo credit totals
here. Provider task or version IDs, operations, and keep/reject decisions stay
in each run's normal provenance. A displayed operation cost is optional.

## Comparison checkpoint

Review the policy after roughly ten representative assets or several normal
art-production days. Compare:

- approximate Codex usage per approved asset or batch;
- visual quality, consistency, and approval confidence;
- corrective work required in Tripo or Blender;
- approval time and the number of revise cycles; and
- whether a rejected or defective asset would have benefited from more
  generation or higher-detail inspection.

Keep the policy when quality remains stable and usage falls. If quality or
downstream rework worsens, loosen the specific generation or inspection rule
responsible rather than changing the user's Codex model or reasoning settings.
