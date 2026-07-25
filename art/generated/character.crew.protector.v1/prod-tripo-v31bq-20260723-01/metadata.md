# Tripo run metadata

Status: candidate 01 provisionally selected; untouched static export present
in the ignored local cache; Blender and Godot work deferred by owner request

## Run

- Asset ID: `character.crew.protector.v1`
- Run ID:
  `prod-tripo-v31bq-20260723-01`
- Production brief: `art/briefs/protector-character-v1.md`
- Approved reference:
  `art/reference-sheets/frontier-station-v1/poc-models/protector-character-turnaround-v1.png`
- Tripo task ID: `1fb3f3bb-1f0c-49cd-a9bd-87cf2a4abe75`
- Created: 2026-07-23
- Candidate count: 1 of 2 maximum
- Credits: 55
- Recorded balance: 24,870 before; 24,815 after

## Submitted inputs

The inputs are truthful lossless crops of the approved reference sheet. The
front-right three-quarter crop was submitted as one coherent image; the
orthographic crops remain selection and later offline reconstruction
references.

| File | Crop geometry | Dimensions | Bytes | Use |
|---|---|---:|---:|---|
| `input/front.png` | `500x590+65+10` | 500 x 590 | 287361 | validation |
| `input/left.png` | `500x590+689+10` | 500 x 590 | 223479 | validation |
| `input/back.png` | `500x590+65+644` | 500 x 590 | 274572 | validation |
| `input/front-right-3q.png` | `500x590+689+644` | 500 x 590 | 275856 | submitted |
| `input/prompt.txt` | exact run prompt | n/a | 796 | submitted |

## Submitted settings

- Signed-in Tripo Studio Max plan; Sharing Only privacy.
- Build & Refine / HD Model / single-image image-to-3D.
- `v3.1 - Best Quality`; Ultra geometry and texture.
- Generate in Parts disabled; 8K texture disabled.
- Displayed and charged cost: 55 credits.
- No API, API key, purchase, or upgrade.
- Machine-readable records:
  `settings/requested.json` and `settings/submitted-candidate-01.json`.

## Result and review

- Studio geometry: 1,000,357 vertices; 1,947,450 triangles.
- Untouched static export:
  `raw/character.crew.protector.v1__raw__tripo-v3.1__candidate-01.glb`
  (57,751,640 bytes; local presence confirmed 2026-07-24).
- The candidate was reviewed directly in the live Studio turntable. Settings,
  task identity, and the decision are recorded here and in the neighboring
  JSON records; disposable Studio screenshots are not versioned.

Candidate 01 preserves the approved adult Black male identity, close-cropped
hair, beard, broad powerful build, heavy armor, unarmed and shield-free
A-pose, complete limbs and rear surfaces, and restrained palette. The
approved explicit vertical upper-back shotgun rail is absent, but usable rear
armor and strap boundaries remain. This is a bounded rigid-hardware defect for
later offline reconstruction, not a reason to risk identity and anatomy with
another paid generation.

The 2026-07-24 signed-in Studio audit reopened the exact task and confirmed
that it remains available. Current account balance was 24,580; the audit spent
zero credits. Raw binaries follow the current no-content-hash cache policy.
No Blender or Godot work is part of this checkpoint.
