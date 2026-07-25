# Tripo run metadata

Status: candidate 01 provisionally selected; normalized Blender source and
published GLB passed fresh structural validation and isolated Godot gallery
review; final owner visual approval and visible hand-grip revision pending

## Run

- Asset ID: `weapon.crew.vanguard_carbine.v1`
- Run ID:
  `prod-tripo-v31bq-20260723-01`
- UTC date: 2026-07-23
- Candidate class: separate rigid handheld weapon
- Production brief: `art/briefs/vanguard-carbine-v1.md`
- Authorization: ADR 0016; provisional selection and integration only
- Compatible character: `character.crew.vanguard.v1`
- Candidate limit: one initial candidate and at most one named-defect retry
- Credits consumed by this run: `55`

## Source and input preparation

Approved source sheet:
`art/reference-sheets/frontier-station-v1/poc-models/vanguard-carbine-turnaround-v1.png`
(`FE6CB280507202CD63E1B72EBF6F1E6329AD165AB1EA96E0B0E517D195C9B099`).
The neighboring provenance identifies the built-in Codex image generator,
records that its model, seed, and job ID were not exposed, and records owner
approval on 2026-07-23.

The exact inputs are lossless pixel crops from that sheet. They were not
resampled, repainted, relit, or redesigned. The sheet divider, excess empty
margin, and neutral hand scale silhouette were excluded from every provider
crop.

| File | Crop geometry | Dimensions | Bytes | SHA-256 |
|---|---|---:|---:|---|
| `input/left-side.png` | `500x300+110+170` | 500 x 300 | 184268 | `823A9F16511D2E9DCFAE280B4A6648506A860717DFA807ED5AEA1CAFF4CA7723` |
| `input/top.png` | `600x300+650+180` | 600 x 300 | 187194 | `62BD237545FD16AD2BDA11AF3A4B5BD7836ED4E19D65B62E1C860EBBBF7A80C4` |
| `input/front-muzzle.png` | `260x450+180+690` | 260 x 450 | 126889 | `822B645EAAFE1B13311BCC44249629B15D1708305F102402828B278F245F4E56` |
| `input/front-right-3q.png` | `600x430+650+720` | 600 x 430 | 315200 | `1D5139126CB6F0F5B81B3B83DDD28A6B4203C56ADD60D6A4D78F1FDC9B20F9C0` |
| `input/prompt.txt` | exact brief prompt | n/a | 707 | `E8AD801F075BDA87B2800F0065DABF0C8CF6E5516DACC99FA17A6404F2967519` |

The exact input crops above are the durable generation sources. Direct review
passed for complete silhouette, readable primary/support grips, open muzzle,
consistent top and three-quarter construction, clean dividers, and complete
exclusion of the human scale silhouette.

## Requested settings and truthful slot mapping

The requested preflight is recorded in `settings/requested.json`
(`5822F9034ECAFFBA09D15972C2A6645F873745824676FEC94F02F6045D1CEA53`):
signed-in Tripo Studio Build & Refine, HD Model, multi-view image-to-3D,
`v3.1 - Best Quality`, Ultra, Generate in Parts disabled for the first coherent
rigid candidate, and 8K Texture disabled.

Before submission, the operating agent records the live plan, privacy setting,
model identifier, displayed cost, and final settings as text. No API,
purchase, or upgrade is authorized.

The currently observed Studio workflow labels orthographic slots Front, Left,
Right, and Back. Only these mappings are unambiguous:

| Studio slot | Exact input | Planned use |
|---|---|---|
| Front | `input/front-muzzle.png` | Submit only when the slot expects the muzzle/front elevation |
| Left | `input/left-side.png` | Submit |
| Right | none | Do not mislabel an auxiliary view |
| Back | none | Do not mislabel an auxiliary view |
| Auxiliary | `input/top.png` | Prepared per brief; use only if Studio exposes a truthful top/auxiliary mapping |
| Validation | `input/front-right-3q.png` | Retain for candidate comparison and cleanup |

If the live workflow has no truthful way to include the top and
three-quarter views, preserve them as validation evidence and use only the
unambiguous orthographic inputs. Do not force them into Right or Back slots.

Live Studio verification found no truthful top or auxiliary slot. Studio's
other multiple-image control is a batch of independent models, not one
multi-view reconstruction. Candidate 01 therefore uses the approved
`input/front-right-3q.png` as a single coherent input; the left, top, and
muzzle crops remain validation and Blender-cleanup authority.

Final live settings immediately before submission:

- plan displayed: Max;
- privacy: Sharing Only;
- visible balance: 25,035 credits;
- model: `v3.1 - Best Quality`;
- workflow: HD Model / single-image image-to-3D / Ultra;
- Generate in Parts: disabled;
- 8K Texture: disabled;
- displayed cost: 55 credits;
- no API, payment, purchase, or upgrade prompt.

## Candidate 01 result

- Candidate 01 completed in Tripo Studio and is provisionally selected pending
  the owner's final visual review.
- Studio task/model identifier:
  `01bb9aea-6b10-419d-bbeb-9648c9867a97`.
- Displayed and charged cost: 55 credits.
- Visible balance before submission: 25,035 credits.
- Visible balance after submission: 24,980 credits.
- Studio raw statistics: 1,006,324 vertices and 1,946,373 triangles.
- Candidate 01 was reviewed directly in Studio's live turntable; the findings
  and selection decision are recorded below.
- Immutable raw export:
  `raw/weapon.crew.vanguard_carbine.v1__raw__tripo-v3.1__candidate-01.glb`
  (58,235,208 bytes; provider task and local presence recorded in
  `raw-export.manifest.json`).
- Submitted settings: `settings/submitted-candidate-01.json`.

The candidate passed the silhouette, distinct primary/support grip, open
muzzle, complete-surface, separate-weapon, and palette checks. It uses a
coherent broad carbine form rather than a shotgun silhouette. No second
candidate was spent because the first candidate met the brief; that unused
retry remains intentionally unspent.

## Blender publication

Candidate 01 was processed and freshly revalidated in Blender 5.2.0 LTS.
Superseded cleanup intermediates remain in the ignored workstation archive
rather than Git history.

The accepted Blender pass welded the provider-derived surface, reconstructed
it at a 3.5 mm voxel size to remove 66 boundary and 95 non-manifold edges,
collapse-decimated it, normalized the envelope, and authored broad de-lit
palette regions. The provider base-color contained baked lighting, so the
final asset uses three authored materials and zero texture sets rather than
shipping that baked-lighting payload.

Final measured static contract:

- 7,398 triangles and 3,691 welded vertices;
- one connected component;
- zero boundary, non-manifold, loose, or zero-area elements after welding
  exported material seams;
- exact 0.82 m length × 0.13 m width × 0.27 m height;
- published `+Y` up and `-Z` forward;
- root coincident with `socket.grip.primary`;
- exact `socket.grip.primary`, `socket.grip.support`, and
  `socket.attack.muzzle.primary` names and identity orientations;
- three materials and zero texture sets; and
- no armature, skin, action, character, ammunition, or gameplay payload.

Accepted outputs:

- editable source:
  `art/source/weapon.crew.vanguard_carbine.v1/vanguard-carbine-v1.blend`
  (272,005 bytes; introducing Git commit pending);
- provisional published GLB:
  `game/Assets/Published/weapon.crew.vanguard_carbine.v1.glb`
  (143,676 bytes; introducing Git commit pending); and
- detailed provenance:
  `derived/blender-publication.md`.

The static review and exact fresh Blender re-import pass. The review decision
is keyed by the canonical repository `run_id`; large 3D binaries are recorded
by path and byte size without a second content hash.

## Exact Vanguard assembly result

The exact published weapon and Vanguard GLBs were reviewed read-only in the
provisional `anim.humanoid.idle_armed` interface pose:

- primary root/socket offset: 0 m;
- support-palm gap: 0.00454767 m;
- muzzle line: clear;
- rear-right holster overlap: 0 triangle pairs; and
- held contact: 1,308 triangle-pair overlaps at the hands/stock, retained as a
  visual-review finding rather than a mechanical blocker.

The mechanical assembly decision is `pass`, but the visual decision remains
`revise`: the generated glove topology reads open beneath the primary and
support grips in close review. The one-frame action is an interface landmark,
not a finished handling animation. Neither published GLB was modified by the
disposable review.

## Remaining gates

Final owner visual review, visible hand-grip revision, finished shared
animation retarget proof, and live gameplay-scene adoption remain pending. The
exact provisional GLB passed isolated Godot import and direct live gallery
review at 7.5 m, 14.5 m, and 20 m with a sibling greybox fallback. No Khronos
glTF Validator was installed; structural validation used exact fresh Blender
and Godot imports. Final gameplay attack identity, timing, damage, range, and
ability definitions remain undefined and were not invented.
