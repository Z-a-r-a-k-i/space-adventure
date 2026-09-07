# Art production

Read this only for asset work. Blender 5.2 owns editable sources and GLB
publication; Godot 4.7.1 owns runtime presentation. Use the
[roster](POC-ASSET-ROSTER.md), the asset's brief/reference, and the
[visual bible](../art/bible/frontier-station-v1.md). Build commands live in
[tools/blender](../tools/blender/README.md); machine installation is optional
[reference material](reference/art-workstation.md).

## Before production

The asset needs an approved ID, reference, brief, owner, and resolved
licensing/privacy state. Follow the checkout policy in [AGENTS.md](../AGENTS.md).
Offline production does not authorize live replacement or later gameplay;
the [roadmap](ROADMAP.md) owns those gates. Routine provider operations and
marker validation need no repeated confirmation; escalate a named unrepairable
defect, scope/licensing change, or an explicit owner review gate.

Use signed-in Tripo Studio, without an API key or direct Godot bridge. Check
the chosen input, privacy setting, and displayed cost before generation. Work
on one purposeful candidate; a retry needs a named defect. Preserve accepted
static sources even while character work changes. Structural and collision-
critical modules are authored dimensionally in Blender.

## Humanoids

1. Begin with one approved front-view, isolated, unarmed T-pose seed. Generate
   one image; a batch of candidates is not a multiview input. Use direct
   single-image HD; multiview requires a named coverage defect. Recorded
   defaults: Tripo v3.1 Best Quality, Ultra, Triangle 2M, 4K PBR; AI Complete,
   Generate in Parts, and 8K Texture off.
2. Inspect the unrigged source, including continuous shoulders/underarms,
   complete hands/feet, joints, silhouette, and materials. Finish topology or
   segmentation changes before rigging. A detached limb requires mesh repair,
   not repeated Mixamo marker submission.
3. Retopologize with Smart Low-Poly v2, Quad, target 10,000; retain usable UVs
   and record actual topology counts. Preserve the static GLB and Tripo Mixamo
   FBX ZIP with 4K textures; the ZIP is the material master. Upload only its
   geometry FBX to Mixamo.
4. Face front, use symmetry and Standard Skeleton (65), then validate chin,
   wrist, elbow, knee, and groin/hip markers and the Auto-Rigger motion preview.
   Download the accepted neutral rig as FBX Binary with skin. Record actual
   hierarchy/names/bone count from that file; the provider label is not proof.
5. Before Blender processing, play one untouched with-skin locomotion FBX in
   ignored Godot staging during sustained movement. It must stay grounded and
   alternate feet. Repair weights/joints and armor transitions in Blender.
6. Prefer Mixamo library donors without skin, 30 fps, no key reduction, and
   in-place movement. Default walk: Standard Walk, In Place, Overdrive 50,
   Character Arm-Space 50. Validate bone names, hierarchy, lengths, rest pose,
   and armature transform before transfer. Bake local pose deltas onto the
   accepted rest rig and root displacement in armature space. If the no-skin
   export changes that contract or fails grounding, record a character-specific
   matching-with-skin exception; never assign it directly because names match.
7. Preserve imported animated-armature rotation/scale. Normalize only by
   retargeting and baking evaluated world-space poses onto a separate rig.
   Repair loops, contacts, sockets, and required actions; export and fresh-import
   the exact GLB. Check every frame against the untouched baseline and
   [rig limits](../art/rigs/crew-humanoid-v1.md), including standing hip height.

Tripo Auto Rig, AI humanoid motion, or a non-T-pose source requires an explicit
recorded owner exception. Base rigs/idle/walk may precede combat; finalize draw,
attack, contact, recoil, recovery, and holster with authoritative gameplay
timing under [ATTACK-PRESENTATION.md](ATTACK-PRESENTATION.md).

## Static props, weapons, and machines

Use brief-specific topology/material budgets. Static pieces skip T-pose,
humanoid Quad-10k defaults, Mixamo, and skinning. Normalize dimensions, axes,
origin/mounting plane, UVs, materials, and any collision contract in Blender.
Fresh-import the exact exported GLB and inspect it in Godot.

Weapons are separate rigid assets; review grips, clearance, sockets, and motion
with the actual character. Non-humanoids are floating or stationary rigid
assemblies with a few aim/recoil/hover/shutdown pivots. No legs, quadrupeds,
organic deformation, or complex machine rigs. Doors may expose rigid leaves
and material states; gameplay drives them from observations.

## Publication and review

Verify class-specific bounds, units, axes, ground contact, topology, UVs,
materials, weights, hierarchy, actions, sockets/pivots, and fresh GLB import.
Review live in Tripo/Blender and inspect the exact publication in Godot at
7.5, 14.5, and 20 m with representative lighting. Technical validity alone
does not establish visual quality or satisfy an explicit human approval gate.

`asset_gallery.tscn` reviews the separate Vanguard carbine;
`humanoid_gallery.tscn` reviews Survivor/Protector; `hostile_gallery.tscn`
reviews Enforcer idle/walk and rigid sentry aim/recoil. Their headless checks
verify integration. Assembled combat motion, grip, and projectile feedback
require the [live review profiles](testing.md), not an isolated idle gallery.

Use at most one representative screenshot per checkpoint when a frozen handoff
helps; more captures need a named defect. Keep media/logs in ignored
`artifacts/`. Untouched provider exports stay in
`art/generated/<asset-id>/<run-id>/raw/`. Retain a compact manifest of provider,
task/version/settings, source path/size, rights, accepted output, and essential
metrics. Do not content-hash large 3D binaries. Accepted `.blend`/`.glb` use LFS.
Keep rejected experiments out of active publications; Git retains decisions.
