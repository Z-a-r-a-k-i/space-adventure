# Tripo run metadata

Status: candidate 01 provisionally selected; untouched static export present
in the ignored local cache; Blender and Godot work deferred by owner request

## Run

- Asset ID: `weapon.crew.protector_shotgun.v1`
- Run ID:
  `prod-tripo-v31bq-20260723-01`
- Production brief: `art/briefs/protector-shotgun-v1.md`
- Approved reference:
  `art/reference-sheets/frontier-station-v1/poc-models/protector-shotgun-turnaround-v1.png`
- Tripo task ID: `83d827a9-9149-4aea-8ef1-d52fb5c83a21`
- Created: 2026-07-23
- Candidate count: 1 of 2 maximum
- Credits: 55
- Recorded balance: 24,815 before; 24,760 after

## Submitted inputs

The inputs are truthful lossless crops of the approved reference sheet. The
forearm-and-hand scale silhouette is excluded. Tripo had no truthful top or
auxiliary slot, so the front-right three-quarter crop was submitted as one
coherent image and the remaining views were used for validation.

| File | Crop geometry | Dimensions | Bytes | Use |
|---|---|---:|---:|---|
| `input/left-side.png` | `730x260+20+80` | 730 x 260 | 208326 | validation |
| `input/top.png` | `720x260+790+120` | 720 x 260 | 154012 | validation |
| `input/front-muzzle.png` | `370x460+200+530` | 370 x 460 | 150137 | validation |
| `input/front-right-3q.png` | `720x400+790+530` | 720 x 400 | 285313 | submitted |
| `input/prompt.txt` | exact run prompt | n/a | 739 | submitted |

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

- Studio geometry: 1,007,701 vertices; 1,962,119 triangles.
- Untouched static export:
  `raw/weapon.crew.protector_shotgun.v1__raw__tripo-v3.1__candidate-01.glb`
  (58,116,884 bytes; local presence confirmed 2026-07-24).
- The candidate was reviewed directly in the live Studio turntable. Settings,
  task identity, and the decision are recorded here and in the neighboring
  JSON records; disposable Studio screenshots are not versioned.

Candidate 01 is unmistakably a short, broad, front-heavy shotgun. It preserves
the coherent stock, receiver, primary grip, reachable pump/support grip,
complete underside and opposite side, wide real open muzzle, and restrained
navy/warm-gray/cyan palette. No character, hand, shield, shell, magazine, or
extra attachment is fused into it. No second candidate is justified.

The 2026-07-24 signed-in Studio audit reopened the exact task and confirmed
that it remains available. Current account balance was 24,580; the audit spent
zero credits. Raw binaries follow the current no-content-hash cache policy.
No Blender or Godot work is part of this checkpoint.
