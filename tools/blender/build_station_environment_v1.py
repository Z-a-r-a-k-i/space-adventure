"""Author the Phase 3 station structure, airlock, and terminal in Blender 5.2.

The three assets are deterministic static/rigid assemblies built from the
approved reference sheets. They contain no collision, rig, animation, baked
lighting, or gameplay identity. Godot keeps those responsibilities in the
authored station-route wrappers.

Run from the repository root:

    blender --background --factory-startup \
      --python tools/blender/build_station_environment_v1.py

The script refuses to overwrite an existing source or publication.
"""

from __future__ import annotations

import json
import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPOSITORY = Path(
    os.environ.get(
        "SPACE_ADVENTURE_REPOSITORY",
        Path(__file__).resolve().parents[2],
    )
).resolve()


def material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    metallic: float,
    roughness: float,
    emission: float = 0.0,
) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    result.diffuse_color = color
    principled = result.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Material '{name}' has no Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission > 0:
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = emission
    return result


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            datablocks.remove(datablock)


def add_box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    assigned_material: bpy.types.Material,
    *,
    bevel: float = 0.035,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    # Builders use the Godot/glTF contract (+Y up, -Z front). Blender authors
    # in +Z up, +Y front, so convert once at this construction boundary.
    blender_location = (location[0], -location[2], location[1])
    blender_dimensions = (dimensions[0], dimensions[2], dimensions[1])
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=blender_location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = blender_dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(assigned_material)
    if bevel > 0:
        modifier = obj.modifiers.new("edge_softening", "BEVEL")
        modifier.width = min(bevel, min(blender_dimensions) * 0.24)
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return obj


def join(name: str, objects: list[bpy.types.Object]) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    return result


def wall(
    name: str,
    center: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    dark: bpy.types.Material,
    armor: bpy.types.Material,
) -> bpy.types.Object:
    x, y, z = center
    width, height, depth = dimensions
    vertical = width < depth
    pieces = [add_box(f"{name}.body", center, dimensions, dark, bevel=0.045)]
    if vertical:
        pieces.extend(
            [
                add_box(
                    f"{name}.cap_low",
                    (x, 0.20, z),
                    (width + 0.06, 0.34, depth),
                    armor,
                    bevel=0.025,
                ),
                add_box(
                    f"{name}.cap_high",
                    (x, height - 0.20, z),
                    (width + 0.06, 0.34, depth),
                    armor,
                    bevel=0.025,
                ),
            ]
        )
    else:
        pieces.extend(
            [
                add_box(
                    f"{name}.cap_low",
                    (x, 0.20, z),
                    (width, 0.34, depth + 0.06),
                    armor,
                    bevel=0.025,
                ),
                add_box(
                    f"{name}.cap_high",
                    (x, height - 0.20, z),
                    (width, 0.34, depth + 0.06),
                    armor,
                    bevel=0.025,
                ),
            ]
        )
    result = join(name, pieces)
    result["camera_occluder"] = True
    return result


def floor_panel(
    name: str,
    center: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    dark: bpy.types.Material,
    armor: bpy.types.Material,
) -> bpy.types.Object:
    x, y, z = center
    width, height, depth = dimensions
    pieces = [add_box(f"{name}.base", center, dimensions, dark, bevel=0.045)]
    step = 1.75
    if depth > width:
        count = max(1, int(depth / step))
        for index in range(count):
            panel_z = z - ((count - 1) * step / 2) + index * step
            pieces.append(
                add_box(
                    f"{name}.panel_{index:02}",
                    (x, y + height * 0.52, panel_z),
                    (width - 0.28, 0.025, min(1.55, depth / count - 0.08)),
                    armor,
                    bevel=0.02,
                )
            )
    else:
        count = max(1, int(width / step))
        for index in range(count):
            panel_x = x - ((count - 1) * step / 2) + index * step
            pieces.append(
                add_box(
                    f"{name}.panel_{index:02}",
                    (panel_x, y + height * 0.52, z),
                    (min(1.55, width / count - 0.08), 0.025, depth - 0.28),
                    armor,
                    bevel=0.02,
                )
            )
    return join(name, pieces)


def build_structure() -> tuple[str, list[str]]:
    asset_id = "kit.station.structure.v1"
    dark = material("mat.station.structure.dark", (0.026, 0.045, 0.070, 1), metallic=0.42, roughness=0.58)
    armor = material("mat.station.structure.armor", (0.20, 0.25, 0.30, 1), metallic=0.55, roughness=0.43)
    cyan = material("mat.station.structure.route_cyan", (0.02, 0.58, 0.82, 1), metallic=0.16, roughness=0.30, emission=4.0)

    objects: list[bpy.types.Object] = []
    objects.append(floor_panel("Floor_Vertical", (0, -0.10, 2.5), (4, 0.20, 9), dark, armor))
    objects.append(floor_panel("Floor_East", (5.5, -0.10, 0), (7, 0.20, 4), dark, armor))
    objects.extend(
        [
            wall("Wall_West", (-2.15, 1.30, 2.5), (0.30, 2.60, 9.30), dark, armor),
            wall("Wall_North", (0, 1.30, 7.15), (4.30, 2.60, 0.30), dark, armor),
            wall("Wall_InnerEast", (2.15, 1.30, 4.575), (0.30, 2.60, 5.15), dark, armor),
            wall("Wall_South", (3.5, 1.30, -2.15), (11.30, 2.60, 0.30), dark, armor),
            wall("Wall_BranchNorth", (5.575, 1.30, 2.15), (7.15, 2.60, 0.30), dark, armor),
        ]
    )
    for name, location in (
        ("Post_Junction", (2.15, 1.4, 2.15)),
        ("Post_AirlockNorth", (9.15, 1.3, 1.6)),
        ("Post_AirlockSouth", (9.15, 1.3, -1.6)),
    ):
        objects.append(add_box(name, location, (0.42, 2.8, 0.42), armor, bevel=0.055))
    objects.append(add_box("Header_Airlock", (9.15, 2.48, 0), (0.42, 0.40, 2.85), armor, bevel=0.045))
    objects.append(add_box("RouteStrip_Vertical", (0, 0.025, 3.05), (0.10, 0.025, 7.30), cyan, bevel=0.01))
    objects.append(add_box("RouteStrip_East", (5.15, 0.025, 0), (6.30, 0.025, 0.10), cyan, bevel=0.01))
    for obj in objects:
        obj["asset_id"] = asset_id
    return asset_id, [obj.name for obj in objects]


def build_airlock() -> tuple[str, list[str]]:
    asset_id = "assembly.station.evacuation_airlock.v1"
    dark = material("mat.station.airlock.dark", (0.025, 0.042, 0.065, 1), metallic=0.46, roughness=0.50)
    armor = material("mat.station.airlock.armor", (0.25, 0.29, 0.32, 1), metallic=0.62, roughness=0.38)
    green = material("mat.station.airlock.status_green", (0.12, 0.92, 0.34, 1), metallic=0.12, roughness=0.28, emission=5.0)

    frame_parts = [
        add_box("frame.left", (-1.43, 1.40, 0), (0.34, 2.80, 0.42), armor, bevel=0.055),
        add_box("frame.right", (1.43, 1.40, 0), (0.34, 2.80, 0.42), armor, bevel=0.055),
        add_box("frame.header", (0, 2.64, 0), (2.58, 0.34, 0.42), armor, bevel=0.055),
        add_box("frame.track", (0, 0.07, 0), (2.58, 0.14, 0.34), dark, bevel=0.025),
    ]
    frame = join("Frame", frame_parts)
    left_parts = [
        add_box("left.body", (-0.62, 1.30, 0), (1.19, 2.42, 0.22), dark, bevel=0.045),
        add_box("left.panel", (-0.62, 1.32, -0.13), (0.92, 1.80, 0.035), armor, bevel=0.03),
        add_box("left.edge", (-0.04, 1.30, -0.14), (0.08, 2.16, 0.045), armor, bevel=0.015),
    ]
    right_parts = [
        add_box("right.body", (0.62, 1.30, 0), (1.19, 2.42, 0.22), dark, bevel=0.045),
        add_box("right.panel", (0.62, 1.32, -0.13), (0.92, 1.80, 0.035), armor, bevel=0.03),
        add_box("right.edge", (0.04, 1.30, -0.14), (0.08, 2.16, 0.045), armor, bevel=0.015),
    ]
    left = join("Door_Left", left_parts)
    right = join("Door_Right", right_parts)
    status = add_box("Status_Header", (0, 2.64, -0.24), (0.82, 0.10, 0.035), green, bevel=0.015)
    control = join(
        "Control_Panel",
        [
            add_box("control.body", (1.47, 1.35, 0), (0.26, 0.55, 0.20), dark, bevel=0.035),
            add_box("control.screen", (1.47, 1.47, -0.12), (0.13, 0.15, 0.025), green, bevel=0.012),
        ],
    )
    objects = [frame, left, right, status, control]
    for obj in objects:
        obj["asset_id"] = asset_id
    left["rigid_part"] = "door_left"
    right["rigid_part"] = "door_right"
    return asset_id, [obj.name for obj in objects]


def build_terminal() -> tuple[str, list[str]]:
    asset_id = "prop.station.service_terminal.v1"
    dark = material("mat.station.terminal.dark", (0.025, 0.040, 0.065, 1), metallic=0.42, roughness=0.53)
    armor = material("mat.station.terminal.armor", (0.27, 0.29, 0.32, 1), metallic=0.57, roughness=0.40)
    violet = material("mat.station.terminal.screen_violet", (0.38, 0.10, 0.90, 1), metallic=0.08, roughness=0.24, emission=4.5)

    body = join(
        "Terminal_Body",
        [
            add_box("base", (0, 0.08, 0), (0.78, 0.16, 0.40), armor, bevel=0.045),
            add_box("pedestal", (0, 0.48, 0.01), (0.58, 0.68, 0.36), dark, bevel=0.045),
            add_box("shoulders", (0, 0.82, -0.015), (0.70, 0.20, 0.39), armor, bevel=0.04),
            add_box("hood", (0, 1.08, 0.015), (0.68, 0.42, 0.39), dark, bevel=0.05),
            add_box("left_rail", (-0.33, 0.68, -0.15), (0.075, 0.92, 0.075), armor, bevel=0.018),
            add_box("right_rail", (0.33, 0.68, -0.15), (0.075, 0.92, 0.075), armor, bevel=0.018),
        ],
    )
    screen = add_box(
        "Terminal_Screen",
        (0, 1.08, -0.215),
        (0.48, 0.30, 0.028),
        violet,
        bevel=0.025,
        rotation=(math.radians(-8), 0, 0),
    )
    status = add_box("Terminal_Status", (0, 0.89, -0.214), (0.25, 0.045, 0.024), violet, bevel=0.01)
    access = add_box("Terminal_Access", (0, 0.43, -0.205), (0.37, 0.37, 0.025), armor, bevel=0.025)
    objects = [body, screen, status, access]
    for obj in objects:
        obj["asset_id"] = asset_id
    return asset_id, [obj.name for obj in objects]


def bounds() -> tuple[list[float], list[float]]:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in meshes:
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                minimum[axis] = min(minimum[axis], point[axis])
                maximum[axis] = max(maximum[axis], point[axis])
    return ([round(value, 4) for value in minimum], [round(value, 4) for value in maximum])


def save_and_export(asset_id: str, names: list[str], source_name: str) -> dict[str, object]:
    source = REPOSITORY / "art" / "source" / asset_id / f"{source_name}.blend"
    publication = REPOSITORY / "game" / "Assets" / "Published" / f"{asset_id}.glb"
    collisions = [path for path in (source, publication) if path.exists()]
    if collisions:
        raise FileExistsError("Refusing to overwrite: " + ", ".join(map(str, collisions)))
    source.parent.mkdir(parents=True, exist_ok=True)
    publication.parent.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene["asset_id"] = asset_id
    scene["axis_contract"] = "Blender +Z up/+Y front; exported +Y up/-Z front"
    scene["gameplay_authority"] = "Godot wrapper"
    scene["source_reference"] = "approved frontier-station reference sheet"
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    bpy.ops.export_scene.gltf(
        filepath=str(publication),
        export_format="GLB",
        export_yup=True,
        export_apply=True,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
    )

    mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
    minimum, maximum = bounds()
    return {
        "asset_id": asset_id,
        "source": str(source.relative_to(REPOSITORY)).replace("\\", "/"),
        "publication": str(publication.relative_to(REPOSITORY)).replace("\\", "/"),
        "objects": names,
        "mesh_count": len(mesh_objects),
        "triangles": sum(len(obj.data.loop_triangles) for obj in mesh_objects),
        "materials": sorted({slot.material.name for obj in mesh_objects for slot in obj.material_slots}),
        "bounds_min": minimum,
        "bounds_max": maximum,
        "source_bytes": source.stat().st_size,
        "publication_bytes": publication.stat().st_size,
    }


def main() -> None:
    requested_asset = None
    if "--" in sys.argv:
        script_arguments = sys.argv[sys.argv.index("--") + 1 :]
        if len(script_arguments) == 2 and script_arguments[0] == "--asset":
            requested_asset = script_arguments[1]
        elif script_arguments:
            raise ValueError("Expected optional arguments: --asset structure|airlock|terminal")

    builders = (
        ("structure", build_structure, "structure-v1"),
        ("airlock", build_airlock, "airlock-v1"),
        ("terminal", build_terminal, "terminal-v1"),
    )
    valid_assets = {asset_name for asset_name, _, _ in builders}
    if requested_asset is not None and requested_asset not in valid_assets:
        raise ValueError(f"Unknown asset '{requested_asset}'; expected one of {sorted(valid_assets)}")

    reports: list[dict[str, object]] = []
    for asset_name, builder, source_name in builders:
        if requested_asset is not None and asset_name != requested_asset:
            continue
        reset_scene()
        asset_id, names = builder()
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH":
                obj.data.calc_loop_triangles()
        reports.append(save_and_export(asset_id, names, source_name))
    print("SPACEADVENTURE_STATION_ASSETS " + json.dumps(reports, sort_keys=True))


if __name__ == "__main__":
    main()
