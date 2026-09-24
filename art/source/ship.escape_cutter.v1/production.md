# Escape cutter production

The owner authorized the existing cutter exterior design for station departure
on 2026-09-12. The [brief](../../briefs/escape-cutter-v1.md) owns the bounded
shape and interface; [manifest.json](manifest.json) owns exact exported metrics,
socket coordinates, source/publication byte sizes, and current validation status.

Source identity: original dimensionally authored Blender geometry and PBR
materials from the project-owned
[2026-07-29 concept](../../concepts/station-escape-ship-combat-v1/escape-cutter-exterior-turnaround-v1.png).
No third-party models, textures, paid generation, or new provider inputs.

Reproduce from the repository root with Blender 5.2:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python-exit-code 1 --python tools/blender/build_escape_cutter.py -- --replace --render
```

The builder saves `model.blend`, exports the GLB, fresh-imports that exact
payload, validates bounds/budgets/rigid hierarchy/markers, and promotes the
validated source, publication, and manifest together. `--replace` explicitly
permits overwriting this asset only. Review media is ignored under
`artifacts/reviews/ship.escape_cutter.v1/v01/`.

The ramp is published open and closes by local X rotation recorded on
`pivot.ramp`; `pivot.door` is a separate stowed door leaf. Engine sockets point
rearward and carry separate cyan cores. The interior socket is a presentation
disappearance point in a shallow exterior recess, not a traversable ship room.
Agent Godot boarding/departure inspection passed; owner visual acceptance remains pending.
