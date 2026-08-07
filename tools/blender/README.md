# Blender production tools

Scripts resolve the repository containing `tools/blender/`. Use
`SPACE_ADVENTURE_REPOSITORY` or an explicit `--repository` argument when a tool
supports another worktree.

Current scripts cover the retained Vanguard carbine source, the deterministic
Phase 3 station structure v2, service door, evacuation airlock and service
terminal, the profile-driven production humanoids, and the rigid Security Gun
Sentry. Rejected prop
candidates, Vanguard character construction, Tripo retargeting, walk
experiments, and rejected retopology scripts were removed. New character tools
must target the accepted Mixamo/Blender pipeline rather than revive them.

Provider payloads remain in each ignored run-local `raw/` cache. Verify the
manifested path and byte size without hashing large 3D binaries.

Review renders are temporary diagnostics under ignored `artifacts/`. Normal
mesh and animation review happens directly in Blender and Godot.

Build one deterministic Phase 3 environment publication from the repository
root with Blender 5.2:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset structure
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset service-door
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset terminal
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset airlock
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_gun_sentry_v1.py
```

The asset selector is required so regeneration is always explicit. The script
refuses to overwrite existing sources or publications unless the exact target
is supplied with `--replace`; use that option only for an intentional rebuild.
It fresh-reimports and validates the exact GLB before reporting success.

Build a production humanoid only from an accepted profile whose provider inputs
exist in the ignored run cache:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_humanoid_character_v1.py -- --profile tools/blender/profiles/<profile>.json
```

The sentry builder publishes a rigid `Base → Aim_Pivot → Recoil` hierarchy,
validates its dimensions, pivot metadata, muzzle contract and budgets, and then
fresh-reimports the staged GLB before atomically replacing the source and
publication paths. It accepts `--replace` only for an intentional rebuild.
