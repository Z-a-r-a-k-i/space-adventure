# Asset brief - Protector character v1

Status: published, integrated, and visually approved in Godot on 2026-08-07;
gameplay attack binding remains pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `character.crew.protector.v1` |
| Category | Humanoid / fixed recruitable companion presentation |
| Owning phase | Phase 3 art |
| Fixed weapon | `weapon.crew.protector_shotgun.v1` as a separate asset |
| Attack source | `handheld` |
| Gameplay attack reference | Pending Phase 3/4 gameplay definition; do not invent |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/protector-character-turnaround-v1.png` |
| Reference SHA-256 | `56C5BD24C0CF40FC59094F429899297435E55D5F056A1903C2A20E5271FCFB51` |
| Rig profile | `rig.crew.humanoid.v1` in `art/rigs/crew-humanoid-v1.md` |
| Key-pose reference | `art/reference-sheets/frontier-station-v1/poc-animation/protector-weapon-handling-key-poses-v1.png` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner through the Phase 3 production authorization.
- Completed operations: selected direct T-pose task
  `c9deee40-b461-45e1-840d-c0ca66cac4c3`, Quad-10k retopology, Mixamo rig,
  Blender reconstruction and weight repair, sockets, idle/walk publication,
  and locked-route Godot integration.
- Production owner: Codex on the dedicated art machine.
- Branch/worktree:
  `codex/phase3-vanguard-production-20260724` at
  `C:\Developpement\space-adventure-art-production`.
- Provider/privacy: signed-in Tripo Studio Max plan, Sharing Only, no API or
  API key. Do not purchase or upgrade.
- Phase-blocked fields: gameplay attack ID, damage, range, timing, abilities,
  ability-specific clips, VFX, and audio. The locked-route waiting presentation
  is integrated.

The Protector is one complete fixed outfit. Outfit seams remain offline source
organization and are not gameplay inventory slots.

## Shape, pose, and coordinates

- Preserve the approved adult Black male identity, very tall broad powerful
  build, dark skin, close-cropped black hair, trimmed short beard, calm stern
  expression, navy padded technical undersuit, broad warm-gray chest and back
  armor, layered shoulders, thick forearm bracers, reinforced gloves, heavy
  leg armor and boots, utility belt, sparse cyan accents, and empty upper-back
  shotgun mount.
- He must remain visibly taller, broader, and heavier than Vanguard and
  Operator without becoming a mech, exoskeleton, or Space Marine analogue.
- Use the shared neutral T-pose, meters, Z-up Blender authoring,
  ground-center origin, unit scale, and no shear after offline reconstruction.
- The accepted rest-pose height target is 1.98 m; the integrated holstered-idle
  silhouette evaluates to 1.93136 m. Do not redesign the shared skeleton to
  force a different number.
- The Blender publication uses glTF `+Y` up and `-Z` forward.
- No shotgun, other firearm, shield, energy barrier, effect, or loose prop may
  be fused to the character source.

## Source, materials, and budgets

- Keep a continuous deforming body/undersuit through all major joints.
- Keep rigid armor, the back mount, and major accessories named where
  practical; omit hidden underlayer faces only where that prevents clipping.
- Stable material IDs:
  `mat.protector.surface.pbr` and `mat.protector.undersuit.navy`.
- Maximum two de-lit 2048x2048 texture sets; no baked scene lighting or bloom.
- Target 20,000-30,000 triangles, hard limit 40,000.
- Maximum 18 runtime skinned mesh objects, eight material slots, 64 published
  bones, and four normalized influences per vertex.
- Presentation GLB contains no generated collision or gameplay hit volumes.

## Attachments and animation

- `socket.weapon.hand_primary`, parented to `hand_r`.
- `socket.weapon.holster_primary`, parented to `spine_03` and fitted to the
  approved empty upper-back mount.
- The shotgun remains a separate rigid asset whose root coincides with
  `socket.grip.primary`.
- Validate primary-hand fit, support-hand reach, shoulder placement, muzzle
  direction, recoil clearance, back-mount clearance, and draw/holster path.
- Reuse the exact shared action-name contract from
  `art/rigs/crew-humanoid-v1.md`. Use the validated Mixamo rig baseline
  and Mixamo library clips, with Blender correction and cleanup.
- Weapon-specific draw, aim, recoil, recovery, and holster correction waits
  until the complete static Protector/shotgun assembly passes review.
- Combat clips are in-place. Ability-specific clips are prohibited.

## Provider plan

Create or select exactly one strict front-view T-pose seed from the approved
sheet and use the shared direct single-image Tripo settings in
`docs/TRIPO-PRODUCTION-HANDOFF.md`. Preserve the complete unarmed identity and
fixed outfit. Generate no shotgun, shield, energy barrier, environment, text,
pedestal, duplicate body, action pose, or baked effect. Generate a second
candidate only when the first has a named identity, articulation, topology, or
construction failure that a bounded retry can plausibly correct.

Record the exact prompt, model and settings, visible task ID and URL, credits,
screenshots, filename, byte size, and local raw-export presence. The raw GLB is
kept locally under the ignored run `raw/` directory and is not manually
hashed.

## Review and stop conditions

Reject identity drift, light skin, missing beard, rear armor that leaves no
viable mounting area, Vanguard-like proportions, mech or Space-Marine
exaggeration, fused joints, embedded weapon geometry, any shield, dominant
cyan, unrepairable topology, or a body that cannot reuse
`rig.crew.humanoid.v1` without hierarchy redesign. An absent rigid mounting
rail is repairable offline when the approved rear-armor surface remains
usable. The selected candidate is accepted, published, integrated, and
visually approved; gameplay attack binding remains a separate later gate.
