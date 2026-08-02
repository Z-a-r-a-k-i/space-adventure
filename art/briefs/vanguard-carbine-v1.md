# Asset brief — Vanguard carbine v1

Status: accepted for offline source production; final visual approval and
gameplay attack binding pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `weapon.crew.vanguard_carbine.v1` |
| Category | Separate rigid two-handed firearm presentation |
| Owning phase | Phase 3 art |
| Compatible character | `character.crew.vanguard.v1` |
| Rig profile | `rig.crew.humanoid.v1` in `art/rigs/crew-humanoid-v1.md` |
| Attack source | `handheld` |
| Gameplay attack reference | Pending Phase 3/4 gameplay definition; do not invent |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/vanguard-carbine-turnaround-v1.png` |
| Reference SHA-256 | `FE6CB280507202CD63E1B72EBF6F1E6329AD165AB1EA96E0B0E517D195C9B099` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner.
- Authorized operations: use the existing selected Tripo candidate; complete
  rigid reconstruction, normalization, material-region cleanup, socket
  authoring, exact Vanguard assembly fitting, GLB staging, and isolated Godot
  gallery validation.
- Production owner: Codex on the dedicated art machine.
- Branch/worktree:
  `codex/vanguard-walk-animation-20260728` at
  `C:\Developpement\space-adventure-art-production`.
- Production resumed by project-owner direction on 2026-07-28. Carbine
  assembly work remains active alongside character animation; live
  replacement approval remains pending.
- Writable paths: this brief; the matching `art/generated/`, `art/source/`,
  `tools/blender/`, ignored `artifacts/`, and isolated Godot gallery paths
  required by the separate Vanguard/carbine assembly.
- Provider/privacy resolution: signed-in Tripo Studio Max plan, Sharing Only,
  no API or API key. Candidate 01 is sufficient; do not spend the unused retry
  without a new named generation defect.
- Phase-blocked fields: gameplay attack ID, damage, range, timing, reload
  behavior, command/event mapping, abilities, VFX, and audio.

## Bounds and coordinates

| Property | Requirement |
|---|---|
| Overall envelope | 0.82 m long × 0.13 m wide × 0.27 m high |
| Scale tolerance | ±2% after normalized Vanguard fit |
| Published up / forward | `+Y` / barrel and muzzle along `-Z` |
| Pivot/root | coincident with `socket.grip.primary` |
| Transform | unit scale, no shear, applied rotation |

Preserve the approved medium-long broad silhouette, squared receiver, compact
stock, short thick barrel, primary pistol grip, reachable forward support grip,
inset vent cuts, warm-gray plates over dark navy/charcoal mechanism, sparse
cyan status accent, and readable muzzle opening.

## Parts, materials, and budgets

- Logical rigid parts may include receiver, stock, primary grip, support grip,
  barrel/muzzle housing, and armor plates.
- No armature is required. Any movable diagnostic part remains a named rigid
  object and cannot imply reload gameplay.
- Stable material IDs:
  `mat.vanguard_carbine.mechanism.dark`,
  `mat.vanguard_carbine.armor.warm_gray`, and
  `mat.vanguard_carbine.accent.cyan`.
- One de-lit 2048×2048 PBR texture set maximum.
- The weapon remains static for the POC: no reload animation, magazine
  simulation, moving diagnostic part, embedded armature, or embedded action.
- Collision belongs to the Godot gameplay wrapper. The presentation GLB does
  not publish generated collision, projectile logic, or gameplay hit volumes.

| Budget | Target | Hard limit |
|---|---:|---:|
| Triangles | 4,000–8,000 | 12,000 |
| Mesh objects | 2–8 | 12 |
| Material slots | 2–3 | 4 |
| Texture sets | 0–1 | 1 |
| Bones / embedded animations | 0 | 0 |

## Required markers and assembly

- `socket.grip.primary` at the right-hand grip; weapon root coincides with it.
- `socket.grip.support` at the intended left-hand support grip.
- `socket.attack.muzzle.primary` at the muzzle; local `-Z` points outward and
  local `+Y` points up.

Markers are presentation frames, never hitboxes. Validate both hands, stock
placement, muzzle line, carry hardware, draw path, aim, recoil, and recovery on
the exact Vanguard assembly. Final attack timing and gameplay mapping remain
pending.

Test both the character-sheet thigh/hip hardware and the animation-sheet
rear-right/back carry rail. The selected carry transform remains provisional
pending owner visual review and must not be treated as a gameplay equipment
definition.

## Provider plan and prompt

Use signed-in Tripo Studio Build & Refine, image-to-3D multi-view with side,
top, and three-quarter crops. Include the muzzle crop only when the live
multi-view mapping is explicit. Remove the human scale silhouette from every
crop. Start with one candidate; retry once only for a named silhouette, muzzle,
grip, or missing-surface defect.

```text
One isolated stylized low-poly retro-industrial science-fiction two-handed
carbine matching the supplied approved views. Medium-long broad weapon with a
sturdy squared receiver, compact stock, short thick barrel, primary pistol
grip, clearly reachable forward support grip, inset vents, warm-gray protective
plates over a dark navy/charcoal mechanism, a tiny cyan status accent, mild
practical wear, and a real open muzzle. Keep one coherent manufacturable rigid
weapon with clean major part boundaries. No character, hands, ammunition,
loose magazine, sling, scope, bayonet, shield, projectile, muzzle flash, text,
logo, scenery, floating pieces, thin fragile barrel, baked lighting, or extra
attachments.
```

## Review and stop conditions

Review at 7.5 m, 14.5 m, and 20 m and in the complete Vanguard assembly. Reject
carbine/shotgun identity confusion, impossible grip spacing, fused or missing
surfaces, paper-thin parts, unreadable muzzle, unstable carry fit, excessive
greebles, unclear licensing/privacy, or topology that exceeds the cleanup cap.
Selection remains provisional pending owner review.
