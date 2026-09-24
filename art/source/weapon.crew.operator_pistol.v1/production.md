# Operator pistol production

The 2026-09-12 owner request promotes the retained separate pistol into the
Medic kit. [Brief](../../briefs/operator-pistol-v1.md) owns shape and budget.

Source identity: retained Blender reconstruction in
`operator-pistol-v1.blend1` (155,401 bytes), copied read-only to ignored
`artifacts/art-expansion/raw/pistol.blend`. It reconstructs the selected
Tripo candidate 02 from task `615ff81e-441e-4cea-b123-109cc93d65a3` under the
existing project/provider rights. The retained raw candidate is 56,269,344 bytes
in its original generated run. No provider generation, upload or purchase
occurred. The source's genuine open bore replaces the raw shallow muzzle.

Current editable source: `operator-pistol-v1.blend`. Publication:
`game/Assets/Published/weapon.crew.operator_pistol.v1.glb`. Three rigid meshes,
three materials and 3,840 triangles; no armature, animation, texture dependency
or support grip. The root is the primary grip. Physical barrel and muzzle now
face Blender +Y / Godot -Z; marker orientation is identity in the published
weapon frame. Dimensions remain approximately 0.08 × 0.29 × 0.19 m.

The Medic source owns fitted hand/right-thigh sockets, the downward holster
orientation and one-handed motion. The left hand is free. Production preserves
the retained open-muzzle reconstruction and existing material identities.

Re-export from a clean checkout using Blender 5.2:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_crew_expansion.py -- --asset pistol --publish-existing
blender --background --factory-startup --python-exit-code 1 --python tools/blender/verify_station_crew_expansion.py
```

Omit `--publish-existing` only to repeat normalization from the retained ignored
backup. The verifier checks exact fresh import, size budget, rigid-only content,
primary/muzzle markers, absent support grip and published -Z muzzle direction.
Blender assembly review checked the open bore, hand fit and holster direction;
owner acceptance includes the final integrated Godot handling review.
