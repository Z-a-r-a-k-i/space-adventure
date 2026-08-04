# Station evacuation airlock v1 production record

Status: technically validated and integrated; final owner visual approval pending

- Authoring tool: Blender 5.2.0 LTS
- Method: deterministic dimensional construction from the approved reference
- Rebuild target: `tools/blender/build_station_environment_v2.py --asset airlock`
- Editable source: `art/source/assembly.station.evacuation_airlock.v1/airlock-v1.blend` (110,095 bytes)
- Runtime publication: `game/Assets/Published/assembly.station.evacuation_airlock.v1.glb` (101,956 bytes)
- Geometry: 5 mesh objects, 1,404 triangles
- Materials: 3
- Blender bounds: `(-1.60, -0.21, 0.00)` to `(1.60, 0.2575, 2.81)`
- Rigid parts: `Door_Left`, `Door_Right`
- Export contract: Blender `+Z` up/`+Y` front to glTF `+Y` up/`-Z` front
- Provider operation: none; dimensionally authored offline

The exact GLB was reviewed upright in Blender and in the live Godot route. Its
rigid leaves opened from authoritative completion state during a complete
graphical playthrough on 2026-08-02. Godot retains collision, interaction, and
timing authority.
