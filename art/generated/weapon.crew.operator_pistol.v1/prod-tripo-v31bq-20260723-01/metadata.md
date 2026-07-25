# Tripo run metadata

Status: candidate 02 provisionally selected as the strongest offline cleanup
source; two-candidate cap exhausted; untouched exports present in the ignored
local cache; Blender and Godot work deferred by owner request

## Run

- Asset ID: `weapon.crew.operator_pistol.v1`
- Run ID:
  `prod-tripo-v31bq-20260723-01`
- Production brief: `art/briefs/operator-pistol-v1.md`
- Approved reference:
  `art/reference-sheets/frontier-station-v1/poc-models/operator-pistol-turnaround-v1.png`
- Tripo task ID: `615ff81e-441e-4cea-b123-109cc93d65a3`
- Created: 2026-07-23
- Candidate count: 2 of 2 maximum
- Credits: 55 total; candidate 02 was a zero-credit Free Retry
- Recorded balance: 24,925 before candidate 01; 24,870 afterward and
  unchanged by candidate 02

## Submitted inputs

The inputs are truthful lossless crops of the approved reference sheet. The
hand-scale silhouette is excluded. Tripo had no truthful top or auxiliary
slot, so the front-right three-quarter crop was submitted as one coherent
image and the remaining views were used for validation.

| File | Crop geometry | Dimensions | Bytes | Use |
|---|---|---:|---:|---|
| `input/left-side.png` | `520x430+160+40` | 520 x 430 | 229504 | validation |
| `input/top.png` | `710x320+810+100` | 710 x 320 | 193253 | validation |
| `input/front-muzzle.png` | `340x460+220+530` | 340 x 460 | 129500 | validation |
| `input/front-right-3q.png` | `560x460+850+535` | 560 x 460 | 267169 | submitted |
| `input/prompt.txt` | exact run prompt | n/a | 608 | submitted |

## Submitted settings

- Signed-in Tripo Studio Max plan; Sharing Only privacy.
- Build & Refine / HD Model / single-image image-to-3D.
- `v3.1 - Best Quality`; Ultra geometry and texture.
- Generate in Parts disabled; 8K texture disabled.
- Candidate 01 displayed and charged 55 credits.
- Candidate 02 used the same task's Free Retry and charged zero credits.
- No API, API key, purchase, or upgrade.
- Machine-readable records are preserved under `settings/`.

## Results and evidence

Candidate 01:

- 1,027,287 vertices; 1,972,692 triangles.
- Raw:
  `raw/weapon.crew.operator_pistol.v1__raw__tripo-v3.1__candidate-01.glb`
  (59,054,116 bytes; present locally).
- Rejected because the front barrel terminates in a solid cap rather than the
  required open muzzle.

Candidate 02:

- 972,403 vertices; 1,878,022 triangles.
- Raw:
  `raw/weapon.crew.operator_pistol.v1__raw__tripo-v3.1__candidate-02.glb`
  (56,269,344 bytes; present locally).
- Preserves the compact pistol class, squared upper, primary grip, protected
  trigger, complete opposite side, and restrained material family.
- Still has a shallow capped bore. It is selected only as the strongest later
  offline reconstruction source, not accepted as generated.

Both candidates were reviewed directly in the live Studio turntable. Settings,
task identity, the muzzle defects, and the keep/reject decisions are recorded
in text and neighboring JSON; disposable Studio screenshots are not versioned.

The 2026-07-24 signed-in Studio audit reopened the task and confirmed candidate
02 remains active. Current account balance was 24,580; the audit spent zero
credits. Raw binaries follow the current no-content-hash cache policy. No
third candidate is permitted, and no Blender or Godot work is part of this
checkpoint.
