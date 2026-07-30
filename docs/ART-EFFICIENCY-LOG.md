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
| 2026-07-29 | Station escape, cutter, humanoid enemy, and separated ship-combat exploration | 10 | 0 | Four purposeful combat refinements: remove a third crew figure, adopt the requested side-by-side arrangement, add a central separation, then remove movement lines | 6 shared project references | 0 | Clean separated v4 composition approved; remaining concepts retained as exploratory | Not exposed by the built-in image tool |
| 2026-07-30 | Jungle, desert, and city planet vibe exploration | 3 | 0 | None; one useful decision draft per distinct location | 3 shared project references | 0 | First drafts retained; owner review pending | Not exposed by the built-in image tool |
| 2026-07-30 | Frontier carrier and hostile dreadnought vibe exploration | 2 | 0 | None; one useful decision draft per distinct warship direction | 3 shared project references | 0 | First drafts retained; owner review pending | Not exposed by the built-in image tool |
| 2026-07-30 | Sapient alien and non-sapient fauna exploration | 2 | 0 | None; one composite decision draft per species class | 0 new; reused 5 current project references | 0 | First drafts retained; owner review pending | Not exposed by the built-in image tool |
| 2026-07-30 | Gameplay identity sequence from companion recruitment through planet arrival hubs | 9 | 0 | None; one useful decision draft per distinct concept, with existing machine attack sheets reused for item ten | 0 new; reused 18 current project references | 0 | First drafts retained; owner review pending | Not exposed by the built-in image tool |

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
