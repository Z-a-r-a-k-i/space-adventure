# Asset brief — station survivor v1

Status: accepted for offline source production; shared-rig reuse and final
visual approval pending

## Identity and role

| Field | Value |
|---|---|
| Asset ID | `character.npc.station_survivor.v1` |
| Category | Humanoid / noncombatant dialogue NPC presentation |
| Owning phase | Phase 3 |
| Runtime outfit | One fixed civilian maintenance outfit |
| Attack source | None; this character is a noncombatant |
| Gameplay attack reference | Not applicable |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/station-survivor-turnaround-v1.png` |
| Reference SHA-256 | `3B54A69C21A563FD9685737D44AC5E55330E8CA1A0E06186655EE96F780AD169` |
| Rig profile | `rig.crew.humanoid.v1` in `art/rigs/crew-humanoid-v1.md` |

## Acceptance record

- Approval date: 2026-07-24.
- Approver: project owner, through the explicit instruction to continue the
  documented SpaceAdventure production order and the later instruction to
  start additional Tripo asset generation in parallel.
- Authorized asset: `character.npc.station_survivor.v1`.
- Authorized offline operations: prepare approved-reference crops, generate at
  most one initial Tripo candidate and one named-defect retry, preserve the raw
  static export in the ignored workstation cache, complete static
  segmentation/topology/material preparation, create an editable Blender
  source, and stage isolated Blender and Godot review evidence.
- Production owner: Codex on the dedicated art machine.
- Branch/worktree:
  `codex/phase3-vanguard-production-20260724` at
  `C:\Developpement\space-adventure-art-production`.
- Writable paths: this brief and the matching
  `art/generated/character.npc.station_survivor.v1/`,
  `art/source/character.npc.station_survivor.v1/`, asset-local Blender tools,
  ignored `artifacts/reviews/character.npc.station_survivor.v1/`, and isolated
  Godot gallery staging.
- Provider/privacy resolution: signed-in Tripo Studio Max plan, `Sharing Only`,
  provider-generated source images owned by the project, and no API or API key.
  Do not purchase, upgrade, change privacy, or expose account information.
- Phase-blocked fields: authored dialogue content, dialogue availability and
  outcomes, gameplay scene replacement, stable live-scene binding, and any
  gameplay-coupled animation timing.
- Dependency-blocked fields: final binding and retargeting to
  `rig.crew.humanoid.v1` remain provisional until Vanguard proves the shared
  skeleton and reusable animation-retarget workflow. No unrelated provider rig
  may be accepted as production authority.

The survivor is one fixed complete outfit. Named coverall, vest, work panels,
boots, cuffs, belt, pouches, shoulder lamp, hair, and head objects are editable
source seams, not equipment slots or interactive inventory.

## Bounds, pose, and coordinates

| Property | Requirement |
|---|---|
| Standing height | 1.70 m target, ±2% after normalization |
| Published up / front | `+Y` / `-Z` |
| Pivot | ground-plane center between feet |
| Neutral pose | symmetrical relaxed A-pose compatible with the shared rig |
| Ground contact | boot soles at `Y = 0` |
| Transform | unit scale, no shear, applied rotation |

Preserve the exact approved identity: an older warm-brown-skinned woman with a
lean practical build, short salt-and-pepper textured hair with one shaved side,
tired intelligent face, dark navy padded maintenance coverall, worn warm-gray
reinforced work vest, utility cuffs, compact tool belt, two closed pouches,
sturdy broad work boots, one small amber caution shoulder lamp, restrained
amber piping, and one tiny cyan station-interface tab. She must read as
civilian technical staff and a dialogue NPC, visibly less armored than the
party.

## Source-part and material policy

- Continuous deforming coverall/body surface through shoulders, elbows, hips,
  and knees; no independent watertight limb chunks at deforming joints.
- Work vest, broad protective panels, cuffs, boots, belt, pouches, lamp, hair,
  and other rigid pieces remain named objects where practical.
- Hidden coverall polygons may be omitted beneath rigid work protection to
  prevent clipping.
- Stable material IDs:
  `mat.survivor.coverall.navy`,
  `mat.survivor.protection.warm_gray`,
  `mat.survivor.accent.amber`,
  `mat.survivor.skin`,
  and `mat.survivor.hair`.
- Maximum two 2048×2048 texture sets; de-lit PBR color with no baked scene
  lighting, bloom, cast shadow, or glow cloud.
- Collision belongs to the Godot wrapper. The presentation GLB contains no
  generated collision, interaction volume, navigation geometry, or gameplay
  state.

## Budgets

| Budget | Target | Hard limit |
|---|---:|---:|
| Triangles | 16,000–26,000 | 32,000 |
| Runtime skinned mesh objects | 4–12 | 18 |
| Material slots | 4–6 | 8 |
| Unique texture sets | 1–2 | 2 |
| Published bones | shared profile | 64 |
| Skin influences | 4 | 4 |

## Rig and animation interfaces

- Reuse the exact Blender-owned `rig.crew.humanoid.v1` hierarchy after its
  Vanguard-first acceptance gate passes.
- The profile's weapon attachment bones may remain in the common hierarchy for
  compatibility but are unused; the survivor has no weapon, holster, attack
  source, muzzle, contact marker, or combat presentation.
- Required presentation coverage:
  `anim.humanoid.dialogue_idle`,
  `anim.humanoid.dialogue_speak`, and
  `anim.humanoid.dialogue_listen`.
- Holstered or armed locomotion, draw, fire, recovery, down, terminal,
  healing, and ability-specific clips are not survivor requirements.
- Provider rigs and animations are diagnostic or donor inputs only. Blender
  owns final topology, hierarchy, weights, clip cleanup, and GLB export.
- No facial lip sync, equipment system, weapon, shield, tool, or ability is
  authorized.

## Provider plan and prompt

Use signed-in Tripo Studio Build & Refine, HD Model, image-to-3D multi-view
with front, strict left, and back lossless crops. Keep the approved front-right
three-quarter crop for validation unless Studio exposes a truthful auxiliary
perspective slot; never label it as a strict right elevation. Use
`v3.1 – Best Quality`, Ultra, Generate in Parts with Balanced segmentation, and
8K Texture disabled. Start with one candidate. A second is allowed only for a
named defect such as duplicate face, changed identity, fused legs or hands,
missing back construction, or unusable joint/part boundaries.

Prompt:

```text
One isolated unarmed stylized low-poly older female frontier-station survivor
in a clean relaxed neutral A-pose. Preserve the approved turnaround identity:
lean practical build, warm brown skin, short salt-and-pepper textured hair with
one shaved side, tired intelligent face, dark navy padded maintenance coverall,
worn warm-gray reinforced work vest with two broad protective chest panels,
fitted sleeves with thick utility cuffs, compact tool belt with two closed
pouches, sturdy broad work boots, one small amber caution shoulder lamp,
restrained amber piping, and one tiny cyan station-interface tab. Same person,
outfit, proportions, hair, pouches, lamp, and colors from every supplied view.
Keep the coverall as one continuous deforming base with rigid work panels and
major accessories separable where practical. Civilian technical staff, visibly
less armored than the party. No weapon, firearm, shield, helmet, backpack,
loose tool, handheld object, combat armor, military silhouette, exposed
midriff, sexualized design, logo, text, number, scenery, pedestal, duplicate
body, action pose, baked lighting, floating parts, or extra limbs.
```

## Review and stop conditions

Review the static candidate at 7.5 m, 14.5 m, and 20 m. Reject identity or age
drift, military/party-armor drift, missing or dominant amber lamp, weapon or
tool geometry, changed hair, fused joints, duplicate face, blocked
articulation, unusable topology, unclear licensing/privacy, or a candidate that
cannot later fit the shared rig without redesign. Selection remains
provisional pending owner visual review. Do not begin final rig binding until
the Vanguard-first shared-rig gate passes.
