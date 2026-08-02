# Vanguard character v1

Status: visual direction approved; the retained neutral-pose derivative is not
authorized for production rigging, and a conforming T-pose source is pending.

## Identity

| Field | Value |
|---|---|
| Asset ID | `character.crew.vanguard.v1` |
| Role | Selectable humanoid protagonist |
| Height | 1.82 m target, ±2% |
| Outfit | One fixed complete outfit |
| Weapon | Separate `weapon.crew.vanguard_carbine.v1` |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/vanguard-character-turnaround-v1.png` |
| Rig profile | `art/rigs/crew-humanoid-v1.md` |

Preserve the sturdy athletic silhouette, approved face and short dark hair,
trimmed beard, navy technical undersuit, warm-gray armor, chunky boots and
gloves, restrained cyan accents, belt, pouches, and empty carry hardware. Do
not fuse a weapon, shield, cape, scenery, or gameplay object into the mesh.

## Production authorization

The project owner approved offline source production on 2026-07-24 and resumed
active Vanguard work on 2026-07-28. Work stays on the dedicated art branch and
may prepare sources, rigs, animations, and isolated reviews. The live greybox
remains until the complete character-plus-carbine result is approved.

Gameplay attacks, timing, damage, abilities, VFX, audio, and live scene wiring
remain phase-blocked.

## Retained source

The retained static derivative is recorded under
`art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260723-01/`.
It is unrigged Tripo Smart Low-Poly v2 retopology using Quad and a 10,000 target.
The actual output is 13,280 faces: 11,343 quads and 1,937 triangles.

This source predates the shared T-pose contract. Successful Mixamo ingestion
does not waive that contract, so the source is retained only for comparison
and may not advance to the production Auto-Rigger gate. Regenerate the
Vanguard as an unrigged T-pose model before rigging. The only alternative is a
named, project-owner-approved grandfathered exception recorded in both ADR
0016 and `docs/POC-ASSET-ROSTER.md`; no such exception is currently approved.

No earlier Vanguard rig, animation proof, Blender character source, or
published character GLB is active.

## Mesh and runtime limits

- continuous deforming surface through shoulders, elbows, hips, and knees;
- rigid armor may remain separate named objects;
- maximum 35,000 runtime triangles, eight materials, two 2048 texture sets,
  64 published bones, and four skin influences per vertex;
- origin at ground center, unit scale, no shear, and grounded boot soles; and
- collision and gameplay volumes remain in Godot, not the presentation GLB.

## Rig and animation sequence

1. Generate, retopologize, and approve a conforming unrigged T-pose source,
   then upload its FBX-and-textures ZIP to Mixamo.
2. A human confirms every chin, wrist, elbow, knee, and groin/hip marker before
   Auto-Rigger submission.
3. Review the rig preview, then download the neutral FBX Binary with skin.
4. In Blender, repair weights at the chin, neck, shoulders, wrists, hips,
   knees, ankles, gloves, and armor transitions.
5. Select existing Mixamo library clips and download them without skin for the
   accepted skeleton, preferring in-place variants.
6. Blender owns clip cleanup, root handling, looping, contacts, weapon
   constraints, sockets, names, and final GLB export.

Required presentation coverage is holstered idle and locomotion, draw, armed
idle and locomotion, raise/aim, fire/recoil, recovery, holster, dialogue idle,
speaking and listening, terminal interaction, healing use, hit reaction, and
down. Use the contract names in `art/rigs/crew-humanoid-v1.md`.

## Assembly and approval

Publish `socket.weapon.hand_primary` and `socket.weapon.holster_primary`.
Validate primary hand, support hand, stock and shoulder clearance, muzzle line,
holster clearance, and draw path with the separate carbine.

Approval requires direct Blender deformation and animation review followed by
the exact GLB in Godot at 14.5 m and 20 m. One representative screenshot per
checkpoint is enough when a frozen handoff image is useful.
