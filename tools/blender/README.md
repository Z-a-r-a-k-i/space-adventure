# Blender production tools

Scripts resolve the repository containing `tools/blender/`. Use
`SPACE_ADVENTURE_REPOSITORY` or an explicit `--repository` argument when a tool
supports another worktree.

Current scripts cover the retained Vanguard carbine source and the deterministic
Phase 3 station structure, evacuation airlock, and service terminal. Rejected prop
candidates, Vanguard character construction, Tripo retargeting, walk
experiments, and rejected retopology scripts were removed. New character tools
must target the accepted Mixamo/Blender pipeline rather than revive them.

Provider payloads remain in each ignored run-local `raw/` cache. Verify the
manifested path and byte size without hashing large 3D binaries.

Review renders are temporary diagnostics under ignored `artifacts/`. Normal
mesh and animation review happens directly in Blender and Godot.

Build the three deterministic Phase 3 environment publications from the
repository root with Blender 5.2:

```text
blender --background --factory-startup --python tools/blender/build_station_environment_v1.py
```

For a clean single-asset rebuild, pass `-- --asset structure`, `airlock`, or
`terminal`. The script deliberately refuses to overwrite existing sources or
publications.
