# Blender production tools

Scripts resolve the repository containing `tools/blender/`. Use
`SPACE_ADVENTURE_REPOSITORY` or an explicit `--repository` argument when a tool
supports another worktree.

Current scripts cover the retained Vanguard carbine source. Rejected prop
candidates, Vanguard character construction, Tripo retargeting, walk
experiments, and rejected retopology scripts were removed. New character tools
must target the accepted Mixamo/Blender pipeline rather than revive them.

Provider payloads remain in each ignored run-local `raw/` cache. Verify the
manifested path and byte size without hashing large 3D binaries.

Review renders are temporary diagnostics under ignored `artifacts/`. Normal
mesh and animation review happens directly in Blender and Godot.
