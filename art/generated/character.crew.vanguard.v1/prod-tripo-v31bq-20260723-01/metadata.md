# Vanguard static source

Status: retained neutral-pose technical result; not authorized for production
Mixamo rigging under the shared T-pose contract.

## Provenance

- Asset ID: `character.crew.vanguard.v1`
- Run ID: `prod-tripo-v31bq-20260723-01`
- Approved reference:
  `art/reference-sheets/frontier-station-v1/poc-models/vanguard-character-turnaround-v1.png`
- Selected Tripo source task: `c889d05a-90fe-4186-85eb-12d4eceafb35`
- Retopology asset/version: `6fe92193-7baf-44ac-9fa2-1d66ead75dff`
- Provider: signed-in Tripo Studio web application

The ignored workstation cache retains the untouched selected high-density GLB,
the Smart Low-Poly GLB, and the FBX-with-textures ZIP used by Mixamo. Their
paths and byte sizes are recorded in `raw-export.manifest.json`.

## Retained static result

- Tripo operation: Retopology, Smart Low-Poly v2, Quad, target 10,000
- Tripo result: 13,280 faces and 12,362 vertices
- Blender FBX inspection: one mesh, no armature, 13,280 polygons,
  11,343 quads, 1,937 triangles, no ngons, and one UV layer
- Decision: topology and appearance retained for comparison, but the source
  does not pass the current T-pose gate

The 10,000 value is the requested target, not an exact output guarantee. The
current source predates the shared T-pose rule. Successful provider ingestion
does not authorize an exception.

## Next gate

Generate and approve a new unrigged T-pose Vanguard, then apply Smart Low-Poly
v2, Quad, and a 10,000 target before uploading that result to Mixamo. A human
must confirm the chin, wrist, elbow, knee, and groin/hip markers before
Auto-Rigger submission. This retained ZIP is not the production input unless a
named project-owner-approved exception is recorded in ADR 0016 and the roster.
