# Vanguard character v1

Status: conforming 4K T-pose source, Mixamo rig, idle, and walk approved and
integrated in the station-route prototype.

## Identity

| Field | Value |
|---|---|
| Asset ID | `character.crew.vanguard.v1` |
| Role | Active humanoid protagonist |
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

The project owner approved offline source production on 2026-07-24 and, on
2026-08-03, approved the final front-view seed, Quad-10k result, Mixamo marker
placement, Auto-Rigger preview, and integrated idle-and-walk presentation. The
Vanguard model may replace its character greybox. The separate carbine and
weapon-handling sequence remain at their own approval gate.

Gameplay attacks, timing, damage, abilities, VFX, and audio remain
roadmap-scoped.

## Active source

The production run is
`art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260803-02/`.
Tripo direct single-image HD task `d3851b9b-abed-4d47-a7e9-972d560fcd0c`
used v3.1 Best Quality, Ultra Mesh Quality, Triangle 2M, 4K PBR, AI Complete
off, Generate in Parts off, and 8K Texture off. Smart Low-Poly v2 Quad target
10,000 produced 11,588 provider faces and 10,592 vertices.

The geometry-only FBX passed Mixamo Auto-Rigger with symmetry and Standard
Skeleton (65) after project-owner approval of the chin, wrist, elbow, knee,
and groin/hip markers. The owner also approved the resulting motion preview.
`Unarmed Idle` and in-place `Standard Walk` with skin provide the accepted rig
and motion sources. The no-skin diagnostic did not preserve the accepted rest
pose and is rejected for this character.

The reproducible Blender source is
`art/source/character.crew.vanguard.v1/vanguard-v1.blend`; the exact published
GLB is `game/Assets/Published/character.crew.vanguard.v1.glb`. Detailed source
settings, byte sizes, validation metrics, and direct-Godot baseline results are
recorded in the production run metadata and source production record.

## Mesh and runtime limits

- continuous deforming surface through shoulders, elbows, hips, and knees;
- rigid armor may remain separate named objects;
- maximum 35,000 runtime triangles, eight materials, two 2048 texture sets,
  64 published bones, and four skin influences per vertex;
- origin at ground center, unit scale, no shear, and grounded boot soles; and
- collision and gameplay volumes remain in Godot, not the presentation GLB.

## Rig and animation sequence

1. Generate and approve one unrigged front-view T-pose source using the shared
   direct single-image Tripo settings.
2. Complete Tripo Smart Low-Poly v2 Quad retopology with a 10,000 target before
   rigging.
3. Upload the geometry-only FBX to Mixamo, enable symmetry and Standard
   Skeleton (65), place all anatomical markers, validate the complete layout,
   and submit the Auto-Rigger without a routine human confirmation gate.
4. Review the preview, download the accepted neutral rig with skin, and repair
   weights in Blender where required.
5. Download stock Mixamo clips without skin by default. For Vanguard, the
   documented with-skin donor exception preserves the accepted rest pose and
   armature-object transform.
6. Prove one untouched with-skin locomotion FBX in an ignored direct Godot
   baseline before Blender processes it.
7. Use `Standard Walk` with In Place on, Overdrive 50, Character Arm-Space 50,
   30 fps, and no keyframe reduction for exploration walking.
8. Preserve the animated Mixamo armature-object transform. If normalization is
   required, retarget and bake evaluated world-space poses onto a separate rig
   instead of applying the source transform.
9. Validate every frame of the exported cycle, fresh-reimport the exact GLB,
   and compare sustained Godot movement with the untouched baseline.

Required presentation coverage remains holstered idle and locomotion, draw,
armed idle and locomotion, raise/aim, fire/recoil, recovery, holster, dialogue
idle, speaking and listening, terminal interaction, healing use, hit reaction,
and down. Only holstered idle and exploration walking are active in the current
prototype; later clips use the contracts in `art/rigs/crew-humanoid-v1.md`.

## Assembly and approval

Publish `socket.weapon.hand_primary` and `socket.weapon.holster_primary`.
Validate primary hand, support hand, stock and shoulder clearance, muzzle line,
holster clearance, and draw path with the separate carbine.

Approval requires direct Blender deformation and animation review followed by
the exact GLB in Godot at representative gameplay camera distances. One
representative screenshot per checkpoint is enough when a frozen handoff image
is useful.
