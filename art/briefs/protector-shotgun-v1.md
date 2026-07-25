# Asset brief - Protector shotgun v1

Status: accepted for bounded Tripo generation and offline source production;
final visual approval, final assembly dimensions, and gameplay attack binding
pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `weapon.crew.protector_shotgun.v1` |
| Category | Separate broad two-handed firearm presentation |
| Owning phase | Phase 3 art |
| Compatible character | `character.crew.protector.v1` |
| Attack source | `handheld` |
| Gameplay attack reference | Pending Phase 3/4 gameplay definition; do not invent |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/protector-shotgun-turnaround-v1.png` |
| Reference SHA-256 | `89C5555855DCCB07B46D5EB2A7A642FFF2C02B78F963F89A777A1C0DA5B101CE` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner through the Phase 3 production authorization.
- Authorized operations: generate at most two Tripo candidates, provisionally
  select the strongest, preserve the untouched static export and provenance,
  and defer Blender reconstruction, marker authoring, assembly, staging, and
  Godot review until the owner restarts those pipeline stages.
- Production owner: Codex on the dedicated art machine.
- Branch/worktree:
  `codex/phase3-vanguard-production-20260724` at
  `C:\Developpement\space-adventure-art-production`.
- Provider/privacy: signed-in Tripo Studio Max plan, Sharing Only, no API, API
  key, purchase, or upgrade.
- Phase-blocked fields: gameplay attack ID, damage, range, timing, ammunition,
  reload behavior, abilities, VFX, audio, and live integration.

## Shape and coordinate contract

- Preserve the approved heavy retro-industrial shotgun silhouette: shorter and
  broader than the Vanguard carbine, unmistakably wide open muzzle, thick
  upper barrel shroud, squared receiver, compact reinforced stock, primary
  pistol grip, reachable forward pump/support grip with visible travel
  clearance, dark navy/charcoal mechanism, broad warm-gray plates, one tiny
  cyan status accent, and restrained wear.
- The exact envelope is fitted later from the approved Protector hands,
  shoulder, and upper-back mount rather than invented independently.
- Blender source later uses meters and Z-up; published GLB uses `+Y` up and
  barrel forward along `-Z`.
- Weapon root coincides with `socket.grip.primary`; unit scale, no shear,
  applied rotation after offline reconstruction.
- Keep the firearm separate from the character. No shield, sling, optic,
  bayonet, loose shells, ammunition, hands, or character geometry.

## Parts, materials, and budgets

- Logical rigid parts: barrel and muzzle housing, upper shroud, receiver,
  compact stock, primary grip and trigger guard, pump/support grip, and armor
  plates.
- Stable material IDs:
  `mat.protector_shotgun.mechanism.dark`,
  `mat.protector_shotgun.armor.warm_gray`, and
  `mat.protector_shotgun.accent.cyan`.
- One de-lit 2048x2048 PBR texture set maximum.
- Target 3,500-8,000 triangles, hard limit 12,000.
- Maximum twelve mesh objects and four material slots.
- No armature, embedded animation, generated collision, projectile logic, or
  gameplay hit volume.

## Required markers and assembly

- `socket.grip.primary` at the right-hand pistol grip; root coincides with it.
- `socket.grip.support` on the reachable forward pump/support grip.
- `socket.attack.muzzle.primary` at the muzzle, local `-Z` outward and local
  `+Y` up.
- Validate the exact Protector hand spacing, trigger clearance,
  support-hand reach, shoulder placement, recoil clearance, back-mount fit,
  draw path, and wide-muzzle readability.
- Markers are presentation frames, never hitboxes. Final attack timing remains
  pending.

## Provider plan

Use compatible side, top, muzzle, and three-quarter views from the approved
sheet. Exclude the forearm-and-hand scale silhouette from every generator
input crop. Generate no character, hands, shield, projectile, muzzle flash,
environment, text, pedestal, or duplicate weapon.

Prefer one compatible multi-view attempt. Generate a second candidate only
when the first has a named identity, topology, or construction failure that a
bounded retry can plausibly correct. The maximum is two total Tripo
candidates.

Record the exact prompt, model and settings, visible task ID and URL, credits,
screenshots, filename, byte size, and local raw-export presence. The raw GLB is
kept locally under the ignored run `raw/` directory and is not manually
hashed.

## Review and stop conditions

Reject carbine or rifle identity, a long thin barrel, closed or unreadable
muzzle, missing or impossible pump grip, impossible hand spacing, missing
surfaces, paper-thin parts, fused human geometry, any shield, dominant cyan,
or a silhouette that cannot fit the approved back mount. Selection and final
dimensions remain provisional pending owner visual review.
