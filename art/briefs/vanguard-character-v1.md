# Asset brief — Vanguard character v1

Status: accepted for offline source production; final visual approval and
gameplay attack binding pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `character.crew.vanguard.v1` |
| Category | Humanoid / selectable protagonist presentation |
| Owning phase | Phase 3 art, advanced provisionally by ADR 0016 |
| Fixed weapon | `weapon.crew.vanguard_carbine.v1` as a separate asset |
| Attack source | `handheld` |
| Gameplay attack reference | Pending Phase 3/4 gameplay definition; do not invent |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/vanguard-character-turnaround-v1.png` |
| Reference SHA-256 | `66858FFDA50CB37A113D6A3EEB66165FB57DE02D60372A7B1551008BD349D0DB` |
| Rig profile | `rig.crew.humanoid.v1` in `art/rigs/crew-humanoid-v1.md` |
| Key-pose reference | `art/reference-sheets/frontier-station-v1/poc-animation/vanguard-weapon-handling-key-poses-v1.png` |
| Key-pose reference SHA-256 | `F618192F0F52AEC1F1AEBA4AA2658DEB7EBD1F18D97B695D870E89DB459A2C22` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner.
- Authorized operations: use the existing bounded Tripo candidates; preserve
  them in the ignored workstation cache; complete Blender reconstruction,
  normalization, rigging, weighting, sockets, animation-interface proof, GLB
  staging, and isolated Godot gallery validation.
- Production owner: Codex on the dedicated art machine.
- Branch/worktree:
  `codex/phase3-vanguard-production-20260724` at
  `C:\Developpement\space-adventure-art-production`.
- Writable paths: this brief; the matching `art/generated/`, `art/source/`,
  `art/rigs/`, `tools/blender/`, ignored `artifacts/`, and isolated Godot
  gallery paths needed for this asset and its separate carbine.
- Provider/privacy resolution: signed-in Tripo Studio Max plan, Sharing Only,
  no API or API key. Existing candidates are sufficient; no new credit spend
  is authorized or required for this pass.
- Phase-blocked fields: gameplay attack ID, damage, range, timing, command and
  event mapping, abilities, ability-specific clips, VFX, and audio. Production
  must not invent them.

Vanguard is one fixed complete outfit. Named undersuit, torso armor, left and
right shoulders, forearms, gloves, leg armor, boots, belt, pouches, and carry
hardware are editable-source seams, not gameplay equipment slots.

## Bounds, pose, and coordinates

| Property | Requirement |
|---|---|
| Standing height | 1.82 m target, ±2% after normalization |
| Shoulder width | approximately 0.52 m before A-pose arm spread |
| Published up / front | `+Y` / `-Z` |
| Pivot | ground-plane center between feet |
| Neutral pose | symmetrical A-pose matching the shared rig profile |
| Ground contact | boot soles at `Y = 0` |
| Transform | unit scale, no shear, applied rotation |

The silhouette is sturdy and athletic, clearly lighter than Protector and
broader than Operator. Preserve the approved face, short dark textured
undercut, trimmed beard, navy technical undersuit, broad warm-gray chest and
limb armor, chunky boots and gloves, restrained cyan accents, and practical
wear. The firearm must not be generated with the body.

## Source-part and material policy

- Continuous deforming undersuit/body surface through shoulders, elbows, hips,
  and knees; no independent watertight limb chunks at joints.
- Rigid armor and major accessories remain named objects where practical.
- Hidden undersuit polygons may be omitted beneath rigid armor to prevent
  clipping.
- Stable material IDs:
  `mat.vanguard.surface.pbr` for the cleaned combined visual-shell texture
  regions (warm-gray armor, dark mechanism, cyan accent, skin, and hair), and
  `mat.vanguard.undersuit.navy` for the continuous deforming underlayer.
- Maximum two 2048×2048 texture sets; de-lit PBR color with no baked scene
  lighting, bloom, or cast shadow.
- Collision belongs to the Godot gameplay wrapper. The presentation GLB does
  not publish generated collision or gameplay hit volumes.

## Budgets

| Budget | Target | Hard limit |
|---|---:|---:|
| Triangles | 18,000–28,000 | 35,000 |
| Runtime skinned mesh objects | 4–12 | 18 |
| Material slots | 4–6 | 8 |
| Unique texture sets | 1–2 | 2 |
| Published bones | shared profile | 64 |
| Skin influences | 4 | 4 |

## Attachments and animation interfaces

- `socket.weapon.hand_primary`, parented to the right hand.
- `socket.weapon.holster_primary`, fitted after normalized assembly review.
  The character sheet suggests thigh/hip hardware while the key-pose sheet
  shows a rear-right/back carry rail. Test both without changing gameplay and
  record the selected transform as provisional pending owner visual review.
- Required complete-assembly validation: stock/shoulder clearance, primary
  hand, support hand, muzzle line, holster clearance, and draw path.
- Required presentation coverage follows `rig.crew.humanoid.v1`: holstered
  idle/locomotion, draw and transfer, armed idle/locomotion, raise/aim,
  fire/recoil, recovery, holster transfer, dialogue poses, terminal
  interaction, healing use, hit reaction, and down.
- Combat clips are in-place. Ability-specific clips are prohibited.
- Final durations and authoritative attack-phase mapping remain pending.

The required exported presentation names are:

```text
anim.humanoid.idle_holstered
anim.humanoid.locomotion_holstered
anim.humanoid.draw
anim.humanoid.idle_armed
anim.humanoid.locomotion_armed
anim.humanoid.raise_aim
anim.humanoid.fire_recoil
anim.humanoid.recovery
anim.humanoid.holster
anim.humanoid.dialogue_idle
anim.humanoid.dialogue_speak
anim.humanoid.dialogue_listen
anim.humanoid.interact_terminal
anim.humanoid.use_healing
anim.humanoid.hit_reaction
anim.humanoid.down
```

Existing one-frame actions are interface landmarks only, not a completed
animation library. Vanguard must prove at least cleaned idle and locomotion
retargeting plus reviewed draw, transfer, ready/aim, recoil, recovery, and
holster poses before the shared library is accepted for another human.

The key-pose sheet is concept direction for carried, draw, ready/aim, attack,
recovery, and holster readability. It is not timing, animation, attack, VFX, or
audio authority.

## Provider plan and prompt

Use signed-in Tripo Studio Build & Refine, image-to-3D multi-view with front,
strict side, back, and three-quarter lossless crops. Generate in Parts may be
enabled for the fixed armor outfit. Start with one candidate; a second is
allowed only for a named defect such as fused limbs, missing back armor,
unrepairable face identity, or unusable part boundaries.

Prompt:

```text
One isolated unarmed stylized low-poly adult male Vanguard in a clean neutral
A-pose. Preserve the approved turnaround identity: sturdy athletic build,
short dark textured undercut, trimmed beard, dark navy padded technical
undersuit, broad warm-gray retro-industrial chest, shoulder, forearm, knee and
shin armor, chunky reinforced boots and gloves, sparse cyan equipment accents,
practical belt and pouches, and empty thigh/hip carry hardware. Same character
and outfit from every supplied view. Keep major rigid armor parts separable
from one continuous deforming undersuit/body base. No firearm, weapon, shield,
helmet, cape, text, logo, scenery, pedestal, extra accessories, duplicate body,
action pose, baked lighting, or floating parts.
```

## Review and stop conditions

Review the static candidate at 7.5 m, 14.5 m, and 20 m and the exact carbine
assembly at 14.5 m and 20 m. Reject identity drift, fused joints, blocked
articulation, weapon geometry, Protector-like bulk, dominant cyan, unusable
topology, unclear licensing/privacy, or a candidate that cannot fit the shared
rig without redesign. Selection remains provisional pending owner review.
