# Station structure kit v1 production record

Status: technically validated and integrated; final owner visual approval pending

- Authoring tool: Blender 5.2.0 LTS
- Method: deterministic dimensional construction from the approved reference
- Script: `tools/blender/build_station_environment_v1.py`
- Editable source: `art/source/kit.station.structure.v1/structure-v1.blend` (122,980 bytes)
- Runtime publication: `game/Assets/Published/kit.station.structure.v1.glb` (246,832 bytes)
- Geometry: 13 mesh objects, 3,456 triangles
- Materials: 3
- Blender bounds: `(-2.33, -7.33, -0.20)` to `(9.36, 2.33, 2.80)`
- Export contract: Blender `+Z` up/`+Y` front to glTF `+Y` up/`-Z` front
- Provider operation: none; dimensionally authored offline

Live Blender review caught and corrected the initial axis conversion before
integration. The exact corrected GLB passed Godot import, headless route smoke,
camera cutaway, and a complete graphical playthrough on 2026-08-02. Godot
retains authored navigation, collision, lights, and stable gameplay IDs.
