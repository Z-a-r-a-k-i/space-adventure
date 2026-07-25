# Tripo run metadata

Status: candidate 01 provisionally selected; untouched static export present in
the ignored local cache; Blender and Godot work deferred by owner request

## Run

- Asset ID: `character.crew.operator.v1`
- Run ID:
  `prod-tripo-v31bq-20260723-01`
- Production brief: `art/briefs/operator-character-v1.md`
- Approved reference:
  `art/reference-sheets/frontier-station-v1/poc-models/operator-character-turnaround-v1.png`
- Tripo task ID: `dd18ffbe-b4bb-4035-9a82-d87da93d9d8a`
- Created: 2026-07-23
- Candidate count: 1 of 2 maximum
- Credits: 55
- Recorded balance: 24,980 before; 24,925 after

## Submitted inputs

The inputs are truthful lossless crops of the approved reference sheet. The
front-right three-quarter crop was submitted as one coherent image after live
testing showed that the fixed multi-view character workflow could duplicate
identity features. The orthographic crops remain selection and later offline
reconstruction references.

| File | Crop geometry | Dimensions | Bytes | Use |
|---|---|---:|---:|---|
| `input/front.png` | `500x590+65+10` | 500 x 590 | 249572 | validation |
| `input/left.png` | `500x590+689+10` | 500 x 590 | 213615 | validation |
| `input/back.png` | `500x590+65+644` | 500 x 590 | 244560 | validation |
| `input/front-right-3q.png` | `500x590+689+644` | 500 x 590 | 247302 | submitted |
| `input/prompt.txt` | exact run prompt | n/a | 804 | submitted |

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

- Studio geometry: 1,003,393 vertices; 1,941,189 triangles.
- Untouched static export:
  `raw/character.crew.operator.v1__raw__tripo-v3.1__candidate-01.glb`
  (57,824,996 bytes; local presence confirmed 2026-07-24).
- The candidate was reviewed directly in the live Studio turntable. Settings,
  task identity, and the decision are recorded here and in the neighboring
  JSON records; disposable Studio screenshots are not versioned.

Candidate 01 preserves the approved adult female identity, high dark bun,
asymmetric sensor, agile build, unarmed A-pose, empty right-side holster,
complete limbs and back, and restrained navy/warm-gray/cyan palette. No
second candidate is justified.

The 2026-07-24 signed-in Studio audit reopened the exact task and confirmed
that it remains available. Current account balance was 24,580; the audit spent
zero credits. Raw binaries follow the current no-content-hash cache policy.
No Blender or Godot work is part of this checkpoint.
