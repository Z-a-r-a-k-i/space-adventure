# Asset brief — Operator character v1

Status: accepted for bounded existing-run audit and offline source production;
final visual approval, exact normalized height, and gameplay attack binding
pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `character.crew.operator.v1` |
| Category | Humanoid / selectable protagonist presentation |
| Owning phase | Phase 3 art |
| Fixed weapon | `weapon.crew.operator_pistol.v1` as a separate asset |
| Attack source | `handheld` |
| Gameplay attack reference | Pending Phase 3/4 gameplay definition; do not invent |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/operator-character-turnaround-v1.png` |
| Reference SHA-256 | `9CC780E475D427792562566F9A080646AC7FB27005A7605D1A89259C3E245B31` |
| Rig profile | `rig.crew.humanoid.v1` in `art/rigs/crew-humanoid-v1.md` |
| Key-pose reference | `art/reference-sheets/frontier-station-v1/poc-animation/operator-weapon-handling-key-poses-v1.png` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner through the Phase 3 production authorization.
- Authorized operations: audit and preserve the existing Tripo run before
  creating any replacement; provisionally select at most one of two
  candidates; complete Blender reconstruction, shared-rig binding, sockets,
  static pistol assembly, GLB staging, and isolated Godot gallery review.
- Production owner: Codex on the dedicated art machine.
- Branch/worktree:
  `codex/phase3-vanguard-production-20260724` at
  `C:\Developpement\space-adventure-art-production`.
- Provider/privacy: signed-in Tripo Studio Max plan, Sharing Only, no API or
  API key. Do not purchase or upgrade.
- Phase-blocked fields: gameplay attack ID, damage, range, timing, abilities,
  ability-specific clips, VFX, audio, and live-route adoption.

The Operator is one complete fixed outfit. Outfit seams remain Blender source
organization and are not gameplay inventory slots.

## Shape, pose, and coordinates

- Preserve the approved adult female identity, athletic agile build, high dark
  bun with restrained side fringe, fitted navy technical undersuit, light
  warm-gray armor, asymmetric shoulder sensor/comms module, utility belt,
  compact pouches, fingerless technical gloves, sparse cyan accents, and empty
  right hip/thigh holster.
- She must remain visibly slimmer and lighter than Vanguard and Protector.
- Use the shared neutral A-pose, meters, Z-up Blender authoring, ground-center
  origin, unit scale, and no shear.
- The exact normalized standing height remains provisional until the three
  crew bodies are compared together. Do not change skeleton names, hierarchy,
  axes, or semantic rest pose to force a number.
- Publish through Blender glTF conversion as `+Y` up and `-Z` forward.
- No pistol, other weapon, shield, effect, or loose prop may be fused to the
  character source.

## Source, materials, and budgets

- Keep a continuous deforming body/undersuit through all major joints.
- Keep rigid armor, the sensor module, holster, and major accessories named
  where practical; omit hidden underlayer faces only where that prevents
  clipping.
- Stable material IDs:
  `mat.operator.surface.pbr` and `mat.operator.undersuit.navy`.
- Maximum two de-lit 2048×2048 texture sets; no baked scene lighting or bloom.
- Target 18,000–28,000 triangles, hard limit 35,000.
- Maximum 18 runtime skinned mesh objects, eight material slots, 64 published
  bones, and four normalized influences per vertex.
- Presentation GLB contains no generated collision or gameplay hit volumes.

## Attachments and animation

- `socket.weapon.hand_primary`, parented to `hand_r`.
- `socket.weapon.holster_primary`, parented to `pelvis` and fitted to the
  approved right hip/thigh holster.
- The pistol remains a separate rigid asset whose root coincides with
  `socket.grip.primary`.
- Validate hand fit, muzzle direction, holster clearance, draw path, and
  one-handed aim silhouette. The left hand must remain clear of the pistol;
  do not invent a support grip.
- Reuse the exact shared action-name contract from
  `art/rigs/crew-humanoid-v1.md`. Shared donor animation is retargeted through
  Blender; provider rigs and clips are never production authority.
- Weapon-specific draw, aim, recoil, recovery, and holster correction waits
  until the complete static Operator/pistol assembly passes review.
- Combat clips are in-place. Ability-specific clips are prohibited.

## Provider plan

First inspect the existing Tripo task
`dd18ffbe-b4bb-4035-9a82-d87da93d9d8a` and any matching untouched local
export. Record its exact live settings, visible task URL, credits, screenshots,
filename, and byte size. Do not generate a replacement unless the existing
candidate fails a named identity, topology, articulation, or construction
check. The two-candidate maximum includes every existing attempt.

If one bounded replacement is justified, use the approved four-view sheet or
truthfully mapped lossless crops. Preserve the complete unarmed identity and
fixed outfit. Generate no pistol, shield, environment, text, pedestal,
duplicate body, action pose, or baked effect.

## Review and stop conditions

Review at 7.5 m, 14.5 m, and 20 m and in the complete separate-pistol assembly.
Reject identity drift, missing bun or sensor module, sexualized redesign,
heavy Protector-like proportions, fused joints, embedded weapon geometry,
two-hand pistol construction, dominant cyan, unrepairable topology, or a body
that cannot reuse `rig.crew.humanoid.v1` without hierarchy redesign.
Selection remains provisional pending owner review.
