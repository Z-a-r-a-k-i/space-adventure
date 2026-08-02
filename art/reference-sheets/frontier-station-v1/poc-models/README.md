# POC model visual approvals

Status: active visual references; rigid gun-sentry redesign pending

Revision: 2026-08-02

## Approval rule

Review one asset delivery at a time. A sheet may be marked `approved`,
`revise`, or `rejected`. Approval freezes the depicted visual direction for the
matching production brief; visual approval alone does not authorize Tripo,
modeling, rigging, animation, publication, or Godot integration. An approved
roster asset may enter offline source production only after its production-
ready art brief is separately accepted under ADR 0016. Gameplay-coupled
finalization and live integration remain governed by `docs/ROADMAP.md` and
`docs/POC-ASSET-ROSTER.md`.

Character bodies and handheld weapons are separate assets. After both sheets
are approved, the pair also receives a complete-assembly check for grip,
support hand, holster, muzzle, articulation, and tactical-camera readability.

## Approved roster

| Order | Asset group | Approval delivery | Status |
|---:|---|---|---|
| 1 | `prop.station.service_terminal.v1` | Existing four-view terminal turnaround | Approved provider input |
| 2 | `character.crew.vanguard.v1` | Unarmed four-view character turnaround; carry hardware remains provisional until complete 3D assembly | Approved visual anchor |
| 3 | `weapon.crew.vanguard_carbine.v1` | Side, top, muzzle, three-quarter, and human-scale weapon views | Approved visual anchor — batch 01 |
| 4 | `weapon.crew.operator_pistol.v1` | Side, top, muzzle, three-quarter, and hand-scale weapon views | Approved visual anchor — batch 01 |
| 5 | `character.crew.operator.v1` | Unarmed four-view character turnaround | Approved visual anchor — batch 01 |
| 6 | `weapon.crew.protector_shotgun.v1` | Side, top, muzzle, three-quarter, and two-hand-scale weapon views | Approved visual anchor — batch 01 |
| 7 | `character.crew.protector.v1` | Unarmed four-view character turnaround; no shield | Approved visual anchor — batch 01 |
| 8 | `character.npc.station_survivor.v1` | Four-view character turnaround and dialogue silhouette | Approved visual anchor — batch 02 |
| 9 | `character.enemy.security_enforcer.v1` | Four-view humanoid T-pose turnaround | Approved visual anchor — batch 04 |
| 11 | `prop.station.wall_utility.v1` | Front, mounting-depth side, top, and three-quarter views | 2D visual anchor approved — batch 02; retained 3D candidate owner review pending |
| 12 | `item.healing.field_aid.v1` | Hand-scale form alternatives followed by one selected turnaround, only if visible 3D handling is retained | Alternative 2 and turnaround approved — batch 02 |
| 13 | `kit.station.structure.v1` | Multi-piece orthographic module sheet plus assembled cutaway example | Approved visual anchor — batch 03; Blender-authored, not Tripo |
| 14 | `assembly.station.evacuation_airlock.v1` | Part sheet plus closed/open-state sheet | Approved visual anchor — batch 03; structural parts Blender-authored |

The previous articulated-machine directions were removed. The remaining gun
sentry requires a new reference as a stationary rigid assembly with only aim,
recoil, hit, and shutdown pivots.

## Shared sheet format

- Clean pale neutral background and even studio lighting.
- Full object visible with generous padding and no scenery.
- Consistent identity, scale, construction, palette, and asymmetry across
  views.
- Near-orthographic views suitable for Blender modeling and provider input.
- No labels, logos, watermark, baked effects, collision, telegraphs, or
  gameplay UI.
- Broad low-poly forms readable at the tactical camera; restrained detail.
- Each accepted image retains its generation prompt, input-reference roles,
  dimensions, hash, and approval status in a neighboring provenance file.
