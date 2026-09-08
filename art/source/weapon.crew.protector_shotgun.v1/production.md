# Protector shotgun production

Asset: `weapon.crew.protector_shotgun.v1` · party preview accepted by the owner
on 2026-09-08, after gameplay and presentation repairs.

Source is retained Tripo candidate 01, task `83d827a9-9149-4aea-8ef1-d52fb5c83a21`,
under `art/generated/weapon.crew.protector_shotgun.v1/prod-tripo-v31bq-20260723-01/`.
The untouched 58,116,884-byte raw GLB remains in that run's ignored cache;
provider rights and reference identity remain in its run records.

The existing cleaned Blender backup was recovered as `protector-shotgun-v1.blend`:
7,388 triangles, one mesh, three simplified materials, 0.84 m length. The
party builder normalizes the barrel to Godot `-Z` and preserves primary grip,
support grip, and muzzle markers. `tools/blender/build_party_presentation.py`
rebuilds the fitted character and weapon into ignored
`game/Assets/LocalBaseline/party/` for review. The accepted weapon is included as
`game/Assets/Published/weapon.crew.protector_shotgun.v1.glb`.

Technical and graphical checks covered topology budgets, grip/carry fit,
materials, and tactical-camera readability. The published bytes match the
owner's accepted preview.
