# Blender production tools

Scripts resolve the repository containing `tools/blender/`. Use
`SPACE_ADVENTURE_REPOSITORY` or an explicit `--repository` argument when a tool
supports another worktree.

Current scripts cover the accepted Mixamo Vanguard assembly and carbine, the
deterministic Phase 3 station structure v2, service door, evacuation airlock and service
terminal, the profile-driven production humanoids, and the rigid Security Gun
Sentry. Earlier rejected prop candidates, character-construction experiments,
Tripo retargeting, walk experiments, and rejected retopology scripts were
removed. New character tools must target the accepted Mixamo/Blender pipeline
rather than revive them.

Provider payloads remain in each ignored run-local `raw/` cache. Verify the
manifested path and byte size without hashing large 3D binaries.

Review renders are temporary diagnostics under ignored `artifacts/`. Normal
mesh and animation review happens directly in Blender and Godot.

Build one deterministic Phase 3 environment publication from the repository
root with Blender 5.2:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset structure
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset service-surround
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

The accepted Vanguard uses `build_vanguard_character_v1.py`; its donor assembly
invokes `repair_solo_presentation.repair_vanguard()` before the exact-GLB
fresh-reimport gate. `build_vanguard_carbine_v1.py` invokes the same module's
axis normalization so the published barrel and muzzle point along Godot `-Z`.
The handling repair preserves the accepted rig and skin, fits armed poses,
and authors draw/holster around the actual weapon sockets.

Running `repair_solo_presentation.py` directly edits the existing carbine and
Vanguard Blender sources and overwrites both published GLBs. It is an
intentional asset-repair operation, not a read-only verification command;
normal documentation or gameplay checks do not require Blender. Preserve
the accepted inputs and follow the publication and direct Godot review gates
in [ART-PIPELINE.md](../../docs/ART-PIPELINE.md). Current provenance and repair
details live in the [Vanguard production record](../../art/source/character.crew.vanguard.v1/production.md)
and [carbine production record](../../art/source/weapon.crew.vanguard_carbine.v1/production.md).

For the local party/UI preview:

```text
blender --background --factory-startup --python-exit-code 1 --python tools/blender/build_party_presentation.py
blender --background --factory-startup --python-exit-code 1 --python tools/blender/render_crew_portraits.py
```

The party builder edits Protector and shotgun sources and exports to ignored
`game/Assets/LocalBaseline/party/`. It needs the retained cleaned shotgun and
accepted character sources. After reviewing a rebuild, copy `protector.glb` to
`game/Assets/Published/character.crew.protector.v1.glb` and `shotgun.glb` to
`game/Assets/Published/weapon.crew.protector_shotgun.v1.glb`, then import and run
the party review. [Current acceptance](../../art/source/weapon.crew.protector_shotgun.v1/production.md).
The portrait tool writes the two HUD PNGs from the accepted character rigs.

For the crew death clips only:

```text
blender --background --python-exit-code 1 --python tools/blender/repair_crew_death.py
blender --background --python-exit-code 1 --python tools/blender/repair_crew_death.py -- --verify-only
```

The first command edits both crew sources and overwrites both published crew
GLBs after fresh-import checks of floor contact and the
final poses. It needs the retained Rifle Death donor in Vanguard's raw cache.
`--verify-only` checks those existing GLBs without editing assets.
