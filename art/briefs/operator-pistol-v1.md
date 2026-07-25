# Asset brief — Operator pistol v1

Status: accepted for bounded existing-run audit and offline source production;
final visual approval and gameplay attack binding pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `weapon.crew.operator_pistol.v1` |
| Category | Separate compact one-handed firearm presentation |
| Owning phase | Phase 3 art |
| Compatible character | `character.crew.operator.v1` |
| Attack source | `handheld` |
| Gameplay attack reference | Pending Phase 3/4 gameplay definition; do not invent |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/operator-pistol-turnaround-v1.png` |
| Reference SHA-256 | `E03B8688BDF1472262849CD7C6BCC1B4350F3FB652C15380B1B06175D6272238` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner through the Phase 3 production authorization.
- Authorized operations: audit and preserve every existing Tripo attempt;
  provisionally select the strongest candidate within the two-candidate cap;
  reconstruct and normalize it in Blender; author markers; fit the exact
  Operator hand and holster; stage GLB; and review it in the isolated gallery.
- Provider/privacy: signed-in Tripo Studio Max plan, Sharing Only, no API, API
  key, purchase, or upgrade.
- Phase-blocked fields: gameplay attack ID, damage, range, timing, ammunition,
  reload behavior, abilities, VFX, audio, and live integration.

## Shape and coordinate contract

- Preserve the compact agile sidearm silhouette, short chunky barrel, squared
  upper housing, comfortable primary grip, protected trigger, dark
  navy/charcoal mechanism, restrained warm-gray plates, tiny cyan status
  accents, mild wear, and unmistakable open muzzle.
- The exact envelope is fitted from the approved Operator hand and right-thigh
  holster rather than invented independently.
- Blender source uses meters and Z-up; published GLB uses `+Y` up and barrel
  forward along `-Z`.
- Weapon root coincides with `socket.grip.primary`; unit scale, no shear,
  applied rotation.
- Keep the firearm separate from the character. No stock, support grip,
  suppressor, scope, loose magazine, ammunition, hands, or character geometry.

## Parts, materials, and budgets

- Logical rigid parts: upper housing/slide, receiver, grip, trigger guard,
  barrel/muzzle housing, and protective plates.
- Stable material IDs:
  `mat.operator_pistol.mechanism.dark`,
  `mat.operator_pistol.armor.warm_gray`, and
  `mat.operator_pistol.accent.cyan`.
- One de-lit 2048×2048 PBR texture set maximum.
- Target 2,500–6,000 triangles, hard limit 9,000.
- Maximum ten mesh objects and four material slots.
- No armature, embedded animation, generated collision, projectile logic, or
  gameplay hit volume.

## Required markers and assembly

- `socket.grip.primary` at the right-hand grip; root coincides with it.
- `socket.attack.muzzle.primary` at the muzzle, local `-Z` outward and local
  `+Y` up.
- No `socket.grip.support`.
- Validate the exact Operator right-hand fit, trigger clearance, one-handed
  aim line, muzzle clearance, right-thigh holster fit, and draw path.
- Markers are presentation frames, never hitboxes. Final attack timing remains
  pending.

## Provider plan

Inspect the existing Tripo pistol attempts first, including visible task
`615ff81e-441e-4cea-b123-109cc93d65a3` and the two matching local candidate
exports. Resolve whether they are two distinct generations or two exports of
one provider task before recording credit totals. Do not create a third
candidate.

Use the approved side, top, muzzle, and three-quarter reference roles.
Exclude the hand-scale silhouette from every generator crop. Reject rather
than endlessly repair pistol/carbine confusion, a closed or unreadable muzzle,
impossible grip geometry, missing surfaces, paper-thin parts, or a silhouette
that cannot fit the approved holster.

## Review boundary

Review at 7.5 m, 14.5 m, and 20 m and in the exact Operator assembly. Selection
and all final dimensions remain provisional pending owner visual review.
