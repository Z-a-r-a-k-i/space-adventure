# Station service terminal v1 production record

Status: technically validated and integrated; final owner visual approval pending

- Authoring tool: Blender 5.2.0 LTS
- Method: deterministic dimensional construction from the approved reference
- Rebuild target: `tools/blender/build_station_environment_v2.py --asset terminal`
- Editable source: `art/source/prop.station.service_terminal.v1/terminal-v1.blend` (110,334 bytes)
- Runtime publication: `game/Assets/Published/prop.station.service_terminal.v1.glb` (72,328 bytes)
- Geometry: 4 mesh objects, 972 triangles
- Materials: 3
- Blender bounds: `(-0.39, -0.21, 0.00)` to `(0.39, 0.2497, 1.29)`
- Export contract: Blender `+Z` up/`+Y` front to glTF `+Y` up/`-Z` front
- Provider operation: none; dimensionally authored offline

The material count was reduced to the brief maximum before publication. The
exact GLB passed Blender review, Godot import, optional-terminal interaction,
and a complete graphical route playthrough on 2026-08-02. Godot retains the
authored wrapper, collision, and stable interaction ID.
