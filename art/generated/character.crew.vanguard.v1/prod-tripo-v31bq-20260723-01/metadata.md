# Tripo run metadata

Status: candidate 02 provisionally selected; normalized Blender source and
published GLB passed fresh structural validation and isolated Godot gallery
review; final owner visual approval and animation-library completion pending

## Run

- Asset ID: `character.crew.vanguard.v1`
- Run ID:
  `prod-tripo-v31bq-20260723-01`
- UTC date: 2026-07-23
- Candidate class: humanoid
- Production brief: `art/briefs/vanguard-character-v1.md`
- Authorization: ADR 0016; provisional selection and integration only
- Rig profile: `rig.crew.humanoid.v1`
- Separate weapon: `weapon.crew.vanguard_carbine.v1`
- Candidate limit: one initial candidate and at most one named-defect retry
- Credits consumed by this run: `75` (`55` static generation + `20` donor rig)

## Source and input preparation

Approved source sheet:
`art/reference-sheets/frontier-station-v1/poc-models/vanguard-character-turnaround-v1.png`
(`66858FFDA50CB37A113D6A3EEB66165FB57DE02D60372A7B1551008BD349D0DB`).
The neighboring provenance identifies the built-in Codex image generator,
records that its model, seed, and job ID were not exposed, and records owner
approval on 2026-07-23.

The exact inputs are lossless pixel crops from that sheet. They were not
resampled, repainted, relit, or semantically changed. The divider and excess
empty margin were excluded, all bodies remain complete, and no weapon is
present.

| File | Crop geometry | Dimensions | Bytes | SHA-256 |
|---|---|---:|---:|---|
| `input/front.png` | `500x590+65+10` | 500 x 590 | 356101 | `C014911146FEF04237FDADCD7109EC3FBAFDA7D4D29237D8F2B2F66E44D0D7AA` |
| `input/left.png` | `500x590+689+10` | 500 x 590 | 310623 | `477778DBD17812DA1ABCF9FC78A5752FE20B2A93413CB64AE50C8B6BD161FE8B` |
| `input/back.png` | `500x590+65+644` | 500 x 590 | 341876 | `035B94372729C8E0CA1C95E188DD67E73F52C1F916D4928A1E0CAD4A0A880C2F` |
| `input/front-right-3q.png` | `500x590+689+644` | 500 x 590 | 348927 | `C7369C79334E094779CFB07700F6975050D2802350CC1D7D73BAEC8CF556AACC` |
| `input/prompt.txt` | exact brief prompt | n/a | 744 | `6D6212AA1FF8EF58DEA574CA7E1992F68369F79D4FFBA224280C7DF970E54639` |

The exact input crops above are the durable generation sources. Direct review
passed for full-body framing, consistent scale, clean dividers, unarmed
presentation, and front/left/back identity consistency. The three-quarter
view remains useful validation input.

## Requested settings and truthful slot mapping

The requested preflight is recorded in `settings/requested.json`
(`4E4C0612D4F8B9EB1389D383A3907C61A5B24C4BDA7B5ACEC430B7D09DA2E164`):
signed-in Tripo Studio Build & Refine, HD Model, multi-view image-to-3D,
`v3.1 - Best Quality`, Ultra, Generate in Parts enabled for the fixed armor
outfit, and 8K Texture disabled.

Before submission, the operating agent records the live plan, privacy setting,
model identifier, displayed cost, and final settings as text. No API,
purchase, or upgrade is authorized.

Live Studio verification immediately before submission:

- plan displayed: Max;
- privacy: Sharing Only;
- visible balance: 25,090 credits;
- model: `v3.1 - Best Quality`;
- workflow: HD Model / multi-view image-to-3D / Ultra;
- Generate in Parts: enabled, `Balanced`;
- 8K Texture: disabled;
- displayed button pricing: `60` credits struck through and `0` current under
  the included `New Function Trial x1`;
- no account, payment, purchase, or upgrade prompt appeared.

| Studio slot | Exact input | Planned use |
|---|---|---|
| Front | `input/front.png` | Submit |
| Left | `input/left.png` | Submit |
| Back | `input/back.png` | Submit |
| Right | none | Leave blank unless Studio explicitly accepts an auxiliary perspective |
| Validation | `input/front-right-3q.png` | Retain for candidate comparison and cleanup |

Do not mislabel the approved front-right three-quarter view as a strict Right
elevation. If the live workflow explicitly accepts auxiliary perspective views
independently of the labeled orthographic slots, record that mapping before
using the three-quarter input.

## Pending operations

- Candidate 01 generation submitted in Tripo Studio.
- Studio task/model identifier:
  `b18e331b-0699-453d-ad8e-a71ffa0e373c`.
- Studio routed the included Generate-in-Parts trial to its Segmentation
  workspace with `Balanced` detail selected.
- Visible balance before submission: 25,090 credits.
- Visible balance immediately after submission: 25,090 credits.
- Initial generation charge: `0` credits under the displayed included trial.
- Studio source topology: 1,938,461 triangles/faces and 976,637 vertices.
- The Balanced preview exposed 20 provisional parts (`tripo_part_0` through
  `tripo_part_19`) and was saved without an additional visible balance change.
- Raw base export:
  `raw/character.crew.vanguard.v1__raw-base__tripo-v3.1__candidate-01.glb`.
- Raw base export bytes: 46,718,744.
- Raw base export: 46,718,744 bytes, recorded by provider task, filename,
  cache-relative path, and size in `raw-export.manifest.json`.
- Candidate 01 was reviewed directly in Studio's live geometry, textured,
  quarter, rear, and rear-quarter views.

The firearm remains a separate run and was not included in this generation.
Candidate 01 is preserved but not selected because Studio rear and
rear-quarter inspection indicates that the face/head orientation is
inconsistent with the rear-facing torso. A separate raw-GLB audit is checking
whether this is a viewer artifact or a real mesh defect. That named defect
justifies the run's one allowed retry.

Studio's unlabeled multiple-image control was inspected before retry. It
identified itself as `Batch Images to 3D (4 / 30)` and displayed 220 credits,
meaning four independent generations rather than one auxiliary multi-view
reconstruction. It was abandoned before submission, produced no models, and
consumed no credits.

Candidate 02 instead uses the approved
`input/front-right-3q.png` as a truthful single-image input to remove the
front/back head ambiguity:

- workflow: HD Model / single-image image-to-3D / Ultra;
- Generate in Parts: disabled, isolating the view correction from part logic;
- 8K Texture: disabled;
- model: `v3.1 - Best Quality`;
- privacy: Sharing Only;
- visible balance: 25,090 credits;
- displayed cost: 55 credits.
- Candidate 02 task/model identifier:
  `c889d05a-90fe-4186-85eb-12d4eceafb35`.
- Candidate 02 displayed and charged cost: 55 credits.
- Visible balance after submission: 25,035 credits.

Blender retopology, shared-rig skinning, socket authoring, and provisional
publication are complete for the static asset. Provider rigs and animations
remain donor inputs only; Blender owns the shared skeleton and future
animation library. Final gameplay attack identity, timing, damage, range, and
ability-specific content remain undefined and must not be invented here.

The isolated candidate-01 raw audit confirmed a definitive provider defect:
the head contains two complete faces, one aligned with the chest and a second
above the back plate. Candidate 01 has 20 generic mesh parts, no armature,
976,637 vertices, 1,938,461 triangles, 14,853 non-manifold edges, no UVs or
images, one constant material, and raw bounds of
0.551147 x 0.230286 x 0.979034 m. Do not retopologize or rig candidate 01.
The durable audit result is recorded in the metrics above; temporary Blender
review artifacts were not versioned.

Candidate 02 completed without the duplicate-face defect. Studio front,
three-quarter, side, and rear review shows one correctly oriented face, a
clean back of head, complete hands and feet, intact carry hardware, and a
continuous unarmed Vanguard silhouette. It is provisionally selected:

- Studio topology: 1,973,024 triangles/faces and 1,021,309 vertices;
- raw export:
  `raw/character.crew.vanguard.v1__raw__tripo-v3.1__candidate-02.glb`;
- raw export bytes: 58,610,072;
- raw export identity and local presence:
  `raw-export.manifest.json`;
- direct Studio review covered the completed front, quarter, and back views;
- static-candidate generation credits: 55; the later donor rig adds 20 and is
  recorded below.

Candidate 02 advanced through Blender cleanup, normalization, retopology,
material reduction, shared Vanguard-first rigging, socket creation, and GLB
publication. The selection remains provisional pending owner visual review.

## Blender production result

Candidate 02 completed the current Vanguard-first static production pass. The
shared humanoid contract is not yet reusable because finished animation
retargeting and the visible hand-grip revision remain open:

- editable source:
  `art/source/character.crew.vanguard.v1/vanguard-character-v1.blend`
  (13,171,789 bytes; introducing Git commit pending);
- provisional published GLB:
  `game/Assets/Published/character.crew.vanguard.v1.glb`
  (12,188,056 bytes; introducing Git commit pending);
- exact fresh-import result: 1.817825 m grounded height, 28,000
  triangles, two materials, one 2048 base/normal/RM texture set, 59 bones,
  maximum four weights per vertex, and zero unweighted vertices;
- interfaces: `socket.weapon.hand_primary` and
  `socket.weapon.holster_primary`;
- animation library: all 16 exact shared action names from
  `rig.crew.humanoid.v1`; the holstered idle action is now a 12.3-second
  in-place Tripo-donor retarget proof and the holstered locomotion action is a
  2.4-second in-place walk proof. The unarmed dialogue idle is a 17.6-second
  `standing_relax` donor proof and dialogue listen is a 6.0-second `wait`
  donor proof while the remaining actions are still unbound one-frame pose
  landmarks without invented timing; and
- curated current review:
  `derived/v2-production/`.

Fresh Blender export/re-import and exact isolated Godot import passed. The
complete carbine assembly passes its mechanical socket, support-hand,
muzzle-line, and holster-clearance checks. Transparent provisional defects
remain for final visual review: the generated glove topology reads visibly
open below both vertical grips in close review, several small rigid
accessories may need manual weight refinement, and twelve actions remain
interface landmarks rather than finished animation. Current static reports
remain in `derived/v2-production/`; the idle donor-retarget proof is in
`derived/v3-retarget-proof/`, and the locomotion proof plus current tactical
review is in `derived/v4-locomotion-proof/`. The current dialogue-idle proof
is in `derived/v5-dialogue-idle-proof/`; the current exact-published-file
dialogue-listen structural proof is in `derived/v6-dialogue-listen-proof/`.
Superseded staging and visual review artifacts remain only in the ignored
workstation archive.

## Tripo donor rig and idle retarget proof

On 2026-07-24 the selected candidate advanced through Tripo Studio's humanoid
diagnostic lane:

- source task/model ID:
  `c889d05a-90fe-4186-85eb-12d4eceafb35`;
- live rig model: `v1.0 - Good for Humanoid`;
- Auto Rig displayed and charged: 20 credits;
- visible balance: 24,600 before rigging and 24,580 after completion;
- no purchase, upgrade, API, or API key was used;
- documented diagnostic presets queued in order: `idle`, `walk`, `run`,
  `shoot`, `hit_to_body_01`, `fall`, `turn`, `standing_relax`, and `wait`;
- preset retargeting did not display or charge additional credits;
- idle export:
  `raw/character.crew.vanguard.v1__donor__tripo-humanoid-v1__idle.glb`;
- combined diagnostic export:
  `raw/character.crew.vanguard.v1__donor__tripo-humanoid-v1__diagnostic-set.glb`;
- separately identified walk export:
  `raw/character.crew.vanguard.v1__donor__tripo-humanoid-v1__walk.glb`;
- separately identified dialogue-idle export:
  `raw/character.crew.vanguard.v1__donor__tripo-humanoid-v1__standing-relax.glb`;
- separately identified dialogue-listen export:
  `raw/character.crew.vanguard.v1__donor__tripo-humanoid-v1__wait.glb`;
- idle export settings: GLB, current 4K texture, Export Skeleton enabled,
  Animation stay in Place enabled;
- all diagnostic exports use those same settings;
- export bytes: idle 79,317,132, combined set 79,801,332, and walk
  79,122,136; dialogue idle is 79,350,188 and dialogue listen is
  79,139,372.

The untouched individual donor GLBs each contain one skin, 41 provider
joints, and one animation. The combined donor contains the same skin and seven
animations; Tripo labels those tracks only as `NlaTrack` through
`NlaTrack.006`, so the separate walk export is the authoritative mapping for
that clip. All donor binaries are preserved only in the ignored raw cache and
recorded by task, filename, settings, byte size, and local presence without a
large-binary hash.

Blender maps 26 semantic donor joints onto `rig.crew.humanoid.v1`, samples the
provider's armature-space rotation deltas, removes donor objects and root
translation, replaces only the requested interface action, and preserves all
other exact action names. Fresh export/re-import passed with one armature, 59
bones, 16 actions, a multi-frame idle, a multi-frame holstered locomotion
clip, and multi-frame dialogue idle/listen actions. Direct Blender playback
showed coherent in-place motion without an obvious deformation or identity
failure.

Godot 4.7.1 imports Blender's dotted action names with underscores. The gallery
maps `anim_humanoid_idle_holstered` and
`anim_humanoid_locomotion_holstered` back to their dotted presentation
contracts and also recognizes `anim_humanoid_dialogue_idle` and
`anim_humanoid_dialogue_listen`. It validates the 12.3-second idle,
2.4-second walk, 17.6-second dialogue-idle, and 6.0-second dialogue-listen
durations and was reviewed live at 7.5 m, 14.5 m, and 20 m. The shared
retarget workflow is therefore proven across idle, locomotion, and two unarmed
dialogue actions. Final Vanguard weapon-handling acceptance still waits for
the existing visible glove/grip revision and the remaining
draw/armed/recoil/holster presentation work.
