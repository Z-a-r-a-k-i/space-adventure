# SpaceAdventure art-production continuation report

Status: bounded Phase 2 continuation reconciled; provisional wall selection pending owner review; experiment incomplete

Date: 2026-07-24

Branch: `codex/art-production-continuation-20260724`

Worktree: `C:\Developpement\space-adventure-art-production`

Baseline: `origin/main` at
`71ea25ed4b773e5ca2f6ee6344fe2f39381d8fe3`

Original art-run baseline:
`1f3d941800a05398221026da53108668c22d12d6`

## Authorization and phase scope

The project owner authorized autonomous continuation, provisional selection,
integration, commits, push, and a pull request, while prohibiting Tripo API
use, API keys, purchases, upgrades, and silent design substitutions.

On 2026-07-24 the owner also selected ADR 0017's local-cache policy. Untouched
Tripo exports remain on this dedicated art workstation at their run-local
`raw/` paths, which are ignored by Git. The branch commits their task IDs,
expected paths, byte sizes, hashes, and provenance manifests. Off-machine
online storage is deferred; Tripo remains only a best-effort recovery source.

The latest-main rebase did not pass the Phase 2 gameplay gate:

- Phase 2 is implemented but its required human playthrough remains pending.
- The Phase 2 exit gate has not passed.
- Commit `71ea25e` adds ADR 0016's brief-scoped offline-production lane, but it
  does not activate Phase 3 or later gameplay integration.
- Production-lane refinement for the three bake-off IDs remains blocked until
  the entire comparison is complete and frozen.

The owner's continuation request remains recorded as bounded bake-off work,
not an ADR 0016 production run:

1. continue the already completed `prop.station.service_terminal.v1` run;
2. run `prop.station.wall_utility.v1`;
3. run `machine.security_drone.body.v1` only if its dedicated approved
   provider pack exists; and
4. keep Vanguard, Operator, Protector, production machines, the structural
   kit, airlock, and ability-specific art deferred.

No routine approval checkpoint was requested or awaited. The wall selection
remains provisional pending the owner's final visual review. The terminal is
retained only as a renderer/readability comparator because it fails the
geometry hard gate.

After rebasing onto commit `71ea25e`, the provisional game-project copies,
dedicated gallery scenes, and standardized review renders were returned to
ignored local staging under `artifacts/`. The tracked source work, provenance,
and derived review GLBs remain on this branch. The live route,
`game/project.godot`, shared registries, gameplay wrappers, and normal
publication paths remain unchanged.

On 2026-07-24 the owner explicitly returned the task to Phase 2 after briefly
considering a later-phase exception. Before that return, six later-phase
character/weapon attempts had already been uploaded and generated in the
signed-in Studio session, charging 330 credits. Their 321 untracked generated
paths remain only in the original dirty main worktree under the Vanguard,
carbine, Operator, pistol, Protector, and shotgun asset IDs. They are excluded
from this branch, are not accepted or integrated here, and were left untouched
during rebase. This report keeps only the three bounded experiment asset IDs in
the retained branch scope while disclosing the out-of-scope activity below.

## Latest-main audit

Commit `71ea25e` defines ADR 0016's quality-first, brief-scoped offline source
lane; requires `__prod__` and `__bakeoff__` run tokens; keeps the bounded
experiment caps intact; and requires the whole experiment to be frozen before
production work begins for any of its three IDs. Every changed text file was
read during rebase reconciliation.

The two retained tasks predate the lane-token rule. Their original paths remain
unchanged to preserve provenance, while both metadata records now declare
`lane: bakeoff` and require the current naming format for any new run.

The earlier reference-package baseline at `1f3d941` added or corrected 41
files, including 24 approved PNGs. Its images were visually inspected and
their bytes/SHA-256 values checked against provenance before provider use.

The key constraints carried forward are:

- the ram-drone sheet is a complete Phase 4 production reference and is not
  the disposable bake-off body;
- the station structure and airlock remain dimensionally authored in Blender,
  not Tripo;
- abilities and shields remain undefined/blocked;
- the Operator uses the corrected one-handed pistol aim;
- the production gun sentry has exactly three legs; and
- the corrected HUD reference pairs Protector with the shotgun.

Those later-phase corrections and the new offline-production policy were
understood but do not reclassify this bounded Phase 2 evidence as production
work or promote later-phase gameplay integration.

## Credit ledger

| Scope | Asset | Run/task | Candidates | Credits | Account balance |
|---|---|---|---:|---:|---|
| Retained Phase 2 | Service terminal | `d3014f04-3de4-45ba-9502-90d6f80ea67b` | 1 | 55 | historical run: 25,145 to 25,090 |
| Out of scope, untracked | Vanguard | free c01 `b18e331b-0699-453d-ad8e-a71ffa0e373c`; charged c02 `c889d05a-90fe-4186-85eb-12d4eceafb35` | 2 | 55 | 25,090 to 25,035 |
| Out of scope, untracked | Vanguard carbine | `01bb9aea-6b10-419d-bbeb-9648c9867a97` | 1 | 55 | 25,035 to 24,980 |
| Out of scope, untracked | Operator | `dd18ffbe-b4bb-4035-9a82-d87da93d9d8a` | 1 | 55 | 24,980 to 24,925 |
| Out of scope, untracked | Operator pistol | `615ff81e-441e-4cea-b123-109cc93d65a3`; charged c01 plus free c02 retry | 2 | 55 | 24,925 to 24,870 |
| Out of scope, untracked | Protector | `1fb3f3bb-1f0c-49cd-a9bd-87cf2a4abe75` | 1 | 55 | 24,870 to 24,815 |
| Out of scope, untracked | Protector shotgun | `83d827a9-9149-4aea-8ef1-d52fb5c83a21` | 1 | 55 | 24,815 to 24,760 |
| Retained Phase 2 | Wall utility | `162d5614-e586-4a2b-a9b6-bbfe71d8caf9` | 1 | 55 | 24,760 to 24,705 |
| Retained Phase 2 | Disposable drone body | no run created | 0 | 0 | unchanged |

Credits charged during the dedicated-machine continuation after the historical
terminal run: **385**.

Credits represented by the two retained bounded Phase 2 runs: **110**.

Credits represented by the excluded later-phase attempts: **330**.

Total observed charge across the historical terminal and all continuation
activity: **440**.

For the retained Phase 2 runs, no purchase, upgrade, API, segmentation,
provider retopology, provider retexture, rigging, or animation operation was
used. The excluded later-phase files are disclosed but not validated or
promoted by this report.

## Asset results

### `prop.station.service_terminal.v1`

Status: previously completed candidate migrated and freshly revalidated for
renderer/readability comparison; not a surviving bake-off candidate.

- One Tripo candidate; no retry.
- Raw GLB in the ignored local cache: 57,150,780 bytes,
  `09249AE0F5D5201B684839C3ED81680F645161C99917117027F803BC28DB4CA0`.
- Tracked recovery/integrity record: `raw-export.manifest.json`.
- Retained V3 comparator Blender source: 3,402,472 bytes,
  `94B2A770AC1E393FEFE77DA2262CA83651933519F738C7F0987383183C60F79D`.
- Retained V3 derived review GLB: 536,644 bytes,
  `1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121`.
- Geometry: 3,979 triangles, exact 0.80 x 1.30 x 0.42 m envelope,
  corrected `-Z` front, two materials.
- The ignored local terminal gallery contains a hidden, correctly sized
  greybox sibling; the live route is intentionally unchanged on this branch.
- Fresh Godot 4.7.1 import, exact scene load, runtime tree, and 14.5 m capture:
  pass.
- Exact-hash local evidence retains the 7.5, 14.5, and 20 m comparator review
  set.
- The editable provider shell still has 42 boundary edges and 64 non-manifold
  edges. They are not visibly open in the isolated gallery, but they violate
  the experiment's geometry hard gate. The terminal is therefore not scored
  and must not be called a production winner without a future topology repair
  and revalidation.

Ignored local validation staging:
`artifacts/reviews/prop.station.service_terminal.v1/1EA04834A19627B94AC004574C5EF1F9A62961FF80B60BFD1C579BFF061FB121/godot/validation.md`

Tactical capture SHA-256 values:

- 7.5 m:
  `C21AB76F20C795796F937AC65A2E3F35CD68A6A938E94FC01A93E4D81E2E1B3A`
- 14.5 m:
  `D503B44306A9C2E0E0EE462926620E331ACC5BFC22DE877F7F9FB2E3537DB0F0`
- 20 m:
  `85680A055597BA3FBC92EE848A4E730A305826A3F2315FB8E09E303E80FFDF3D`

### `prop.station.wall_utility.v1`

Status: Tripo candidate passed Blender and isolated Godot validation;
provisionally selected pending owner review and missing comparison rows.

- Approved Front and Right elevations were submitted through true multi-view
  mode; the incompatible Top and three-quarter crops were retained only as
  validation evidence.
- One Tripo candidate; no retry.
- Task: `162d5614-e586-4a2b-a9b6-bbfe71d8caf9`.
- Raw topology displayed by Studio: 1,943,381 triangles and 1,017,000
  vertices.
- Untouched raw GLB in the ignored local cache: 58,219,864 bytes,
  `7D1B87029212C9DA8757DABBA7643B7811808F124FFEC7E80C4A7E546F969059`.
- Tracked recovery/integrity record: `raw-export.manifest.json`.
- Strong front identity match: dark enclosure, dominant grille, two
  copper-dark runs, heavy clamps, and one subordinate cyan strip.
- Named defect: the generated rear repeats utility detail and contains a broad
  blue baked-color artifact instead of a flat mounting plane.
- A retry was not used because no approved rear elevation exists; resubmitting
  the same two views would not target the defect.
- Blender removed the duplicate rear and blue artifact, reconstructed a
  closed flat mounting face at `Z = 0`, welded and reduced the retained front,
  and preserved the approved handedness.
- Editable Blender source: 3,550,000 bytes,
  `F5E1C2AE41D65F8A0E193BB24A4D13F90778C694D68E8EA7B64C38AFC52B9326`.
- Derived review GLB: 485,876 bytes,
  `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2`.
- Geometry: 2,764 triangles, one mesh, two materials, three embedded
  1024-pixel images, no skin, animation, or collision.
- Exported bounds are exactly 1.20 x 0.80 x 0.22 m with signed AABB
  `X[-0.60,+0.60]`, `Y[0,+0.80]`, `Z[-0.22,0]`; the front faces `-Z`.
- The spatial-weld diagnostic reports zero boundary, non-manifold, wire,
  loose, and zero-area geometry.
- Final deterministic Blender cleanup took 27.853 seconds. The recorded
  cumulative automated repair trials took 272.018 seconds; active human
  cleanup remained below the 30-minute cap.
- The exact derived GLB passed local Godot import, scene validation, runtime
  inspection, hidden-fallback verification, and visual review at 7.5, 14.5,
  and 20 m with zero editor/debugger/dialog errors.

Ignored local standardized Godot evidence:
`artifacts/reviews/prop.station.wall_utility.v1/104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2/godot/`

Tactical capture SHA-256 values:

- 7.5 m:
  `360AA5D83F00C09ED3159396E54CDCFCB1D9B6A238416E4181354A36091AFCA9`
- 14.5 m:
  `905AFEDA47E41EA44185870C121CB7D5E3AA92C14B0DECC9EE064C8AE269A539`
- 20 m:
  `8B876B4A7EAADE1A116523398C355A50B7072C244EF942A110C07DC73414D462`

### `machine.security_drone.body.v1`

Status: blocked before run creation; zero credits.

There is no approved provider-neutral body-only pack and no legitimate prior
run for this asset ID. The latest ram-drone reference is a different,
later-phase complete machine. The utility-walker smoke test is also a different
asset, is unsubmitted in repository provenance, and violates the body-only
brief.

Blocker record:
`art/generated/machine.security_drone.body.v1/PREFLIGHT-BLOCKED-2026-07-24.md`

## Experiment status

The bounded work is not a completed provider bake-off:

- no Meshy result or documented provider failure exists for any row;
- the existing terminal Blender-only comparator has no recorded 30-minute
  authoring clock;
- the wall and drone Blender-only baselines are missing;
- provider-generation elapsed times were not exposed or recorded;
- the terminal Tripo candidate fails the surviving-geometry hard gate; and
- the drone row remains blocked before generation.

The wall Tripo candidate has a provisional weighted score of 4.15/5. It is not
a provider-adoption decision because the comparison rows are missing. The
formal status and score are recorded in
`art/experiments/3d-generator-bakeoff-2026-07.md`.

These visual-spike gaps are non-gating for Phase 2 gameplay. They do not
authorize Phase 3.

## Phase 2 verification matrix

The repository's canonical commands were invoked from the isolated worktree
with Windows PowerShell 5. The machine does not have `pwsh`, and the installed
.NET SDK is 8.0.421 while the repository pins 8.0.319 with `latestPatch`.
Restore/build/test therefore used the temporary worktree-only
`latestFeature` roll-forward; `global.json` was restored before publication.

| Check | Result | Notes |
|---|---|---|
| `scripts/dev.ps1 doctor` | Tooling failure | Required `pwsh` is not installed |
| `scripts/dev.ps1 restore` | Pass | Exit 0 |
| `scripts/dev.ps1 build` | Pass | Exit 0; 0 warnings, 0 errors |
| `scripts/dev.ps1 test` | Pass | Exit 0; 14/14 tests |
| `scripts/dev.ps1 scenario -Name station-route` | Pass | Exit 0; completed at tick 97 with no command rejections |
| `scripts/dev.ps1 import` | Wrapper tooling failure | Windows PowerShell 5 lacks `ProcessStartInfo.ArgumentList`; failure occurred before Godot launch |
| `scripts/dev.ps1 headless -Name station-route` | Wrapper tooling failure | Same wrapper/runtime incompatibility |
| `scripts/dev.ps1 capture -Name wall-cutaway` | Wrapper tooling failure | Same wrapper/runtime incompatibility |
| Direct Godot 4.7.1 import using the wrapper's exact arguments | Pass | Exit 0 in isolated user data |
| Direct station-route smoke using the wrapper's exact arguments | Pass | Exit 0; `passed:true`, tick 97, phase `Completed`, terminal inspected |
| Direct wall-cutaway capture using the wrapper's exact arguments | Pass | Exit 0; `passed:true` |

The wrapper failures are host-tooling compatibility defects, not observed
Godot or gameplay defects. The wall-cutaway capture is 1280 x 720, its
lifecycle completed initial cut, restore, and re-cut while paused, and its
manifest hash matches the PNG:

- PNG: 126,328 bytes,
  `63E237C6E548D21FA21FE1874D049475F1B959989F34DC7717DD503194273691`
- JSON: 13,300 bytes,
  `03AD520AA2EE74CBFAA7B4E1729AA53E83683BB85F04ACEA07BDF38D75E9CF81`

Visual inspection found the HUD, protagonist, survivor, terminal, and airlock
readable. The occluding wall's upper panel is cut while its opaque low base
remains, and unrelated walls remain intact. This automated evidence does not
replace the required physical-input human playthrough or its flicker/feel
assessment.

## Provenance and repository audit

- The terminal derived-artifact ledger replays 19/19 retained inputs and
  outputs.
- The terminal and wall Studio evidence manifests replay 6/6 and 7/7 entries,
  respectively.
- The wall derived-artifact ledger replays 19/19 retained inputs and outputs.
- Both ignored raw-cache files are present on the dedicated workstation and
  match the committed manifest sizes and SHA-256 values. Normal clones and CI
  do not require them.
- Standardized Blender and Godot review evidence remains on the dedicated
  machine in ignored local staging. Its terminal Blender manifest, contact
  sheet, and Godot ledger hashes are
  `53811B1C6AF29D0BC8852417FFAF3B1429F3AA7779C5A389C672477627FA5F4B`,
  `C5B3006B8C3FE0A6ACC387EBBA5660A74ACB34EECF158DA94670DA7DDEA493AA`,
  and
  `1AF05F30583419FD3D6F91BD139DF09D2E0CA497170330A375A84E7984AEE31C`.
  The corresponding wall hashes are
  `954B8385F8307F22DE11F8C758CA0110CDEABE6609B33A94A973A21350A96B15`,
  `C065B8BE086F30376263BDC7B1CD451EE1DBCDC68B5423D3D81E110A75E57743`,
  and
  `786E4392597D0B0982694AA6E5E479FBE5FA1B83A593AD929B7FBDD5541CBC65`.
- Superseded Blender outputs and recorder frame/audio duplicates were pruned
  from the review branch. Their exact hashes, named rejection reasons, and
  processing metrics remain in run metadata and processing-history records.
- The local Godot staging copies matched the derived GLB hashes during
  validation. Their game-project publications were removed from this branch
  during latest-main policy reconciliation.
- No private Tripo task URL, credential, cookie, bearer token, email address,
  or API key was found in the retained branch.
- No later-phase asset path appears in this branch's net diff. The original
  dirty main worktree contains 321 untracked generated paths under
  `character.crew.vanguard.v1`, `weapon.crew.vanguard_carbine.v1`,
  `character.crew.operator.v1`, `weapon.crew.operator_pistol.v1`,
  `character.crew.protector.v1`, and
  `weapon.crew.protector_shotgun.v1`; reconciliation did not move, stage,
  delete, or validate them.
- Both task UUIDs are recorded consistently in metadata, and the wall UUID is
  also retained in the generating-screenshot filename. Neither UUID is legible
  inside the captured Studio pixels, so strict screenshot-visible task-ID
  evidence remains an honest provenance gap.
- Accepted GLB, Blender, and generated-input binaries are assigned to Git LFS
  through `.gitattributes`. Untouched Tripo payloads and standardized review
  binaries remain ignored.
- Full-window Studio evidence retains the numeric credit balance,
  notification badge, and thumbnails of other SpaceAdventure generations
  needed to explain the account-balance chain. It contains no email,
  credential, token, private task URL, or API key. The repository is currently
  private, but this evidence should be cropped or separately archived before
  any future public release if those surrounding thumbnails are not intended
  for disclosure.
- These two legacy runs predate the mandatory lane token and the current
  preflight ownership record. Their paths are preserved for provenance,
  metadata explicitly labels them `bakeoff`, and the original preflight did
  not record an MCP port or the exact writable path set. New runs must use the
  current token and preflight requirements.
- The branch's Blender tools default to their containing repository or an
  explicit `SPACE_ADVENTURE_REPOSITORY`; no tool retains a hard-coded pointer
  to the owner's original worktree.
- The isolated Godot editor was closed cleanly after all scenes were saved.
  This rebase/reconciliation did not change the owner's original editor or
  dirty main worktree.

## Validation and remaining work

Completed:

- latest-main audit and phase determination;
- isolated branch/worktree;
- exact terminal migration and byte-for-byte audit;
- fresh terminal import, runtime scene inspection, fallback verification, and
  14.5 m capture;
- wall-utility approved input pack, hashes, live settings, task ID, credit
  evidence, selection screenshots, ignored local raw export, tracked
  raw-export manifest, and selection record;
- wall-utility Blender normalization, rear reconstruction, retopology,
  materials, exact-hash derived GLB validation, ignored local gallery,
  reversible greybox fallback, and 7.5/14.5/20 m captures;
- drone-body preflight blocker with zero credit use.

Pending:

- independent physical-input human playthrough recorded in
  `docs/PLAYTESTS.md`;
- Meshy results or documented provider failures, missing Blender-only
  baselines/timing, and final provider decisions if the non-gating experiment
  is resumed;
- owner disposition of the 321 untracked later-phase generated paths and the
  full-window Studio evidence before any future public release;
- optional migration of ignored raw exports to private off-machine storage,
  with verified restore locators added to their manifests; and
- owner visual review and independent physical-input playtest.
