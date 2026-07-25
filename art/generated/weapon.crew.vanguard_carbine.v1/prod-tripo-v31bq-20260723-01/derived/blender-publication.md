# Blender publication — Vanguard carbine candidate 01

Status: provisional static pass; complete-assembly visual revision required

The untouched provider export remains in the ignored run-local `raw/` cache.
Blender 5.2.0 LTS owns the closed production mesh, materials, marker frames,
and GLB export.

## Published asset

- Editable source:
  `art/source/weapon.crew.vanguard_carbine.v1/vanguard-carbine-v1.blend`
  (272,005 bytes; introducing Git commit pending).
- Provisional GLB:
  `game/Assets/Published/weapon.crew.vanguard_carbine.v1.glb`
  (143,676 bytes; introducing Git commit pending).
- Geometry: one closed mesh, 7,398 triangles.
- Envelope: 0.82 m long × 0.13 m wide × 0.27 m high.
- Materials:
  `mat.vanguard_carbine.mechanism.dark`,
  `mat.vanguard_carbine.armor.warm_gray`, and
  `mat.vanguard_carbine.accent.cyan`.
- Interfaces:
  `socket.grip.primary`, `socket.grip.support`, and
  `socket.attack.muzzle.primary`.
- Embedded character, armature, animation, collision, attack logic, and
  gameplay definitions: none.

Fresh Blender re-import passed the source brief's static geometry, envelope,
material, marker, axis, and separation checks. The exact GLB also imported in
Godot and was reviewed directly in the isolated gallery at 7.5 m, 14.5 m, and
20 m.

## Complete assembly

The exact separate GLBs were assembled read-only in
`anim.humanoid.idle_armed`:

- primary root/socket offset: 0 m;
- support-palm gap: 0.00454767 m;
- muzzle line: clear;
- rear-right holster overlap: 0 triangle pairs; and
- held contact: 1,308 triangle-pair overlaps at hands and stock.

The mechanical review passes. The visual decision remains `revise` because
the generated glove topology reads open below both vertical grips at close
range. No corrective weapon deformation or hidden attachment offset was
baked. The 16 exported one-frame actions are interface landmarks, not the
finished shared animation library.

Current machine-readable reports are under `derived/v2-production/`.
Disposable review renders remain local under ignored `artifacts/`.
Superseded cleanup iterations remain only in the ignored workstation archive.
Final owner visual approval, the hand-grip revision, shared donor-retarget
proof, and gameplay attack binding remain pending.
