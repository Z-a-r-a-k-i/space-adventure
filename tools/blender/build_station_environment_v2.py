"""Build the production station-route v2 environment assemblies in Blender 5.2.

Run one explicit target from the repository root:

    blender --background --factory-startup --python-exit-code 1 \
      --python tools/blender/build_station_environment_v2.py -- \
      --asset structure

Available targets are structure, service-door, terminal, and airlock. Add
``--replace`` only when intentionally rebuilding that target's exact source and
publication paths.
"""

from __future__ import annotations

import argparse
import errno
import hashlib
import json
import math
import os
import sys
import tempfile
import time
import uuid
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

import bpy
from mathutils import Vector


REPOSITORY = Path(
    os.environ.get("SPACE_ADVENTURE_REPOSITORY", Path(__file__).resolve().parents[2])
).resolve()


def publication_lock_path(source: Path, publication: Path) -> Path:
    lock_key = f"{source.resolve()}\0{publication.resolve()}".encode("utf-8")
    digest = hashlib.sha256(lock_key).hexdigest()[:20]
    return Path(tempfile.gettempdir()) / f"space-adventure-art-{digest}.lock"


@contextmanager
def exclusive_file_lock(lock_path: Path) -> Iterator[None]:
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    lock_file = lock_path.open("a+b")
    try:
        lock_file.seek(0, os.SEEK_END)
        if lock_file.tell() == 0:
            lock_file.write(b"\0")
            lock_file.flush()
        lock_file.seek(0)
        if os.name == "nt":
            import msvcrt

            while True:
                lock_file.seek(0)
                try:
                    msvcrt.locking(lock_file.fileno(), msvcrt.LK_NBLCK, 1)
                    break
                except OSError as error:
                    if error.errno not in (errno.EACCES, errno.EDEADLK):
                        raise
                    time.sleep(0.1)
        else:
            import fcntl

            fcntl.flock(lock_file.fileno(), fcntl.LOCK_EX)
        try:
            yield
        finally:
            lock_file.seek(0)
            if os.name == "nt":
                msvcrt.locking(lock_file.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock_file.fileno(), fcntl.LOCK_UN)
    finally:
        lock_file.close()


def promote_staged_artifacts(
    staged_artifacts: tuple[tuple[Path, Path], ...],
    *,
    replace: bool,
    transaction_id: str,
    lock_path: Path,
) -> None:
    backups: dict[Path, Path] = {}
    promoted: set[Path] = set()

    with exclusive_file_lock(lock_path):
        try:
            if not replace:
                late_collisions = [
                    target for target, _ in staged_artifacts if target.exists()
                ]
                if late_collisions:
                    raise FileExistsError(
                        "Refusing to overwrite: "
                        + ", ".join(map(str, late_collisions))
                    )

            if replace:
                for target, _ in staged_artifacts:
                    if not target.exists():
                        continue
                    backup = target.with_name(
                        f".{target.stem}.{transaction_id}.backup{target.suffix}"
                    )
                    os.replace(target, backup)
                    backups[target] = backup

            for target, staged in staged_artifacts:
                if replace:
                    os.replace(staged, target)
                else:
                    os.link(staged, target)
                    promoted.add(target)
                    staged.unlink()
                    continue
                promoted.add(target)
        except OSError as error:
            rollback_errors = []
            for target, _ in staged_artifacts:
                try:
                    backup = backups.get(target)
                    if backup is not None and backup.exists():
                        os.replace(backup, target)
                    elif target in promoted and target.exists():
                        target.unlink()
                except OSError as rollback_error:  # pragma: no cover - OS failure path
                    rollback_errors.append(f"{target}: {rollback_error}")
            if rollback_errors:
                raise RuntimeError(
                    "Artifact replacement failed and rollback was incomplete: "
                    + "; ".join(rollback_errors)
                ) from error
            raise
        else:
            for backup in backups.values():
                if backup.exists():
                    backup.unlink()


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
    for datablocks in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
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
    # Authoring arguments use the Godot/glTF contract (+Y up, -Z front).
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
    occluder_id: str,
    center: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    dark: bpy.types.Material,
    armor: bpy.types.Material,
) -> bpy.types.Object:
    x, _, z = center
    width, height, depth = dimensions
    vertical = width < depth
    pieces = [add_box(f"{name}.body", center, dimensions, dark, bevel=0.045)]
    if vertical:
        pieces.extend(
            [
                add_box(f"{name}.lower_cap", (x, 0.20, z), (width + 0.06, 0.34, depth), armor, bevel=0.025),
                add_box(f"{name}.upper_cap", (x, height - 0.20, z), (width + 0.06, 0.34, depth), armor, bevel=0.025),
            ]
        )
    else:
        pieces.extend(
            [
                add_box(f"{name}.lower_cap", (x, 0.20, z), (width, 0.34, depth + 0.06), armor, bevel=0.025),
                add_box(f"{name}.upper_cap", (x, height - 0.20, z), (width, 0.34, depth + 0.06), armor, bevel=0.025),
            ]
        )
    result = join(name, pieces)
    result["camera_occluder"] = True
    result["occluder_id"] = occluder_id
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
    long_axis = max(width, depth)
    panel_count = max(1, int(long_axis / 1.75))
    if depth >= width:
        for index in range(panel_count):
            panel_z = z - depth / 2 + (index + 0.5) * depth / panel_count
            pieces.append(add_box(
                f"{name}.panel_{index:02}",
                (x, y + height * 0.52, panel_z),
                (width - 0.28, 0.025, depth / panel_count - 0.10),
                armor,
                bevel=0.02,
            ))
    else:
        for index in range(panel_count):
            panel_x = x - width / 2 + (index + 0.5) * width / panel_count
            pieces.append(add_box(
                f"{name}.panel_{index:02}",
                (panel_x, y + height * 0.52, z),
                (width / panel_count - 0.10, 0.025, depth - 0.28),
                armor,
                bevel=0.02,
            ))
    return join(name, pieces)


def build_structure() -> tuple[str, str, list[str], int, int]:
    asset_id = "kit.station.structure.v2"
    dark = material("mat.station.structure.dark", (0.026, 0.045, 0.070, 1), metallic=0.42, roughness=0.58)
    armor = material("mat.station.structure.armor", (0.20, 0.25, 0.30, 1), metallic=0.55, roughness=0.43)
    cyan = material("mat.station.structure.route_cyan", (0.02, 0.58, 0.82, 1), metallic=0.16, roughness=0.30, emission=4.0)

    objects: list[bpy.types.Object] = [
        floor_panel("Floor_StartRoom", (-10, -0.10, 7), (6, 0.20, 6), dark, armor),
        floor_panel("Floor_SoloCombatArena", (-10, -0.10, 0), (10, 0.20, 8), dark, armor),
        floor_panel("Floor_ProtectorRoom", (-1.5, -0.10, 0), (7, 0.20, 6), dark, armor),
        floor_panel("Floor_MainPartyArena", (0, -0.10, 8), (12, 0.20, 10), dark, armor),
        floor_panel("Floor_FinalAirlockApproach", (9, -0.10, 8), (6, 0.20, 6), dark, armor),
    ]

    wall_specs = [
        ("Wall_Start_North", "presentation.wall.start.north", (-10, 1.30, 10.15), (6.30, 2.60, 0.30)),
        ("Wall_Start_West", "presentation.wall.start.west", (-13.15, 1.30, 7), (0.30, 2.60, 6.30)),
        ("Wall_Start_East", "presentation.wall.start.east", (-6.85, 1.30, 7), (0.30, 2.60, 6.30)),
        ("Wall_Start_SouthWest", "presentation.wall.start.south_west", (-12.25, 1.30, 4), (1.50, 2.60, 0.30)),
        ("Wall_Start_SouthEast", "presentation.wall.start.south_east", (-7.75, 1.30, 4), (1.50, 2.60, 0.30)),
        ("Wall_Solo_West", "presentation.wall.solo.west", (-15.15, 1.30, 0), (0.30, 2.60, 8.30)),
        ("Wall_Solo_South", "presentation.wall.solo.south", (-10, 1.30, -4.15), (10.30, 2.60, 0.30)),
        ("Wall_Solo_NorthWest", "presentation.wall.solo.north_west", (-14, 1.30, 4), (2.00, 2.60, 0.30)),
        ("Wall_Solo_NorthEast", "presentation.wall.solo.north_east", (-6, 1.30, 4), (2.00, 2.60, 0.30)),
        ("Wall_Solo_EastSouth", "presentation.wall.solo.east_south", (-5, 1.30, -2.75), (0.30, 2.60, 2.50)),
        ("Wall_Solo_EastNorth", "presentation.wall.solo.east_north", (-5, 1.30, 2.75), (0.30, 2.60, 2.50)),
        ("Wall_Protector_South", "presentation.wall.protector.south", (-1.5, 1.30, -3.15), (7.30, 2.60, 0.30)),
        ("Wall_Protector_East", "presentation.wall.protector.east", (2.15, 1.30, 0), (0.30, 2.60, 6.30)),
        ("Wall_Main_West", "presentation.wall.main.west", (-6.15, 1.30, 8), (0.30, 2.60, 10.30)),
        ("Wall_Main_North", "presentation.wall.main.north", (0, 1.30, 13.15), (12.30, 2.60, 0.30)),
        ("Wall_Main_SouthWest", "presentation.wall.main.south_west", (-5.5, 1.30, 2.85), (1.00, 2.60, 0.30)),
        ("Wall_Main_SouthEast", "presentation.wall.main.south_east", (4, 1.30, 2.85), (4.00, 2.60, 0.30)),
        ("Wall_Main_EastSouth", "presentation.wall.main.east_south", (6.15, 1.30, 4), (0.30, 2.60, 2.00)),
        ("Wall_Main_EastNorth", "presentation.wall.main.east_north", (6.15, 1.30, 12), (0.30, 2.60, 2.00)),
        ("Wall_Final_North", "presentation.wall.final.north", (9, 1.30, 11.15), (6.30, 2.60, 0.30)),
        ("Wall_Final_South", "presentation.wall.final.south", (9, 1.30, 4.85), (6.30, 2.60, 0.30)),
        ("Wall_Final_EastNorth", "presentation.wall.final.east_north", (12, 1.30, 10.25), (0.30, 2.60, 1.50)),
        ("Wall_Final_EastSouth", "presentation.wall.final.east_south", (12, 1.30, 5.75), (0.30, 2.60, 1.50)),
    ]
    objects.extend(wall(name, occluder_id, center, dimensions, dark, armor)
                   for name, occluder_id, center, dimensions in wall_specs)

    for name, location in (
        ("Post_StartNorthWest", (-13.15, 1.40, 10.15)),
        ("Post_StartNorthEast", (-6.85, 1.40, 10.15)),
        ("Post_SoloSouthWest", (-15.15, 1.40, -4.15)),
        ("Post_SoloSouthEast", (-5, 1.40, -4.15)),
        ("Post_MainNorthWest", (-6.15, 1.40, 13.15)),
        ("Post_MainNorthEast", (6.15, 1.40, 13.15)),
        ("Post_FinalNorthEast", (12.15, 1.40, 11.15)),
        ("Post_FinalSouthEast", (12.15, 1.40, 4.85)),
    ):
        objects.append(add_box(name, location, (0.38, 2.80, 0.38), armor, bevel=0.055))

    route_strips = [
        ("RouteStrip_Start", (-10, 0.025, 7.15), (0.10, 0.025, 5.40)),
        ("RouteStrip_SoloVertical", (-10, 0.025, 1.95), (0.10, 0.025, 3.80)),
        ("RouteStrip_SoloHorizontal", (-7.5, 0.025, 0), (4.90, 0.025, 0.10)),
        ("RouteStrip_Protector", (-2.45, 0.025, 0), (4.70, 0.025, 0.10)),
        ("RouteStrip_MainVertical", (0, 0.025, 4.0), (0.10, 0.025, 8.00)),
        ("RouteStrip_Final", (6, 0.025, 8), (12.00, 0.025, 0.10)),
    ]
    objects.extend(add_box(name, center, dimensions, cyan, bevel=0.008)
                   for name, center, dimensions in route_strips)

    for obj in objects:
        obj["asset_id"] = asset_id
    return asset_id, "structure-v2", [obj.name for obj in objects], 30_000, 4


BoxSpec = tuple[
    str,
    tuple[float, float, float],
    tuple[float, float, float],
    str,
    float,
]


def build_door_assembly(
    asset_id: str,
    source_name: str,
    materials: dict[str, bpy.types.Material],
    part_specs: dict[str, tuple[BoxSpec, ...]],
    status_spec: BoxSpec,
) -> tuple[str, str, list[str], int, int]:
    def configured_box(spec: BoxSpec) -> bpy.types.Object:
        name, location, dimensions, material_key, bevel = spec
        return add_box(
            name,
            location,
            dimensions,
            materials[material_key],
            bevel=bevel,
        )

    frame = join("Frame", [configured_box(spec) for spec in part_specs["Frame"]])
    left = join(
        "Door_Left", [configured_box(spec) for spec in part_specs["Door_Left"]]
    )
    right = join(
        "Door_Right", [configured_box(spec) for spec in part_specs["Door_Right"]]
    )
    status = configured_box(status_spec)
    control = join(
        "Control_Panel",
        [configured_box(spec) for spec in part_specs["Control_Panel"]],
    )
    objects = [frame, left, right, status, control]
    for obj in objects:
        obj["asset_id"] = asset_id
    left["rigid_part"] = "door_left"
    right["rigid_part"] = "door_right"
    return asset_id, source_name, [obj.name for obj in objects], 4_000, 3


def build_service_door() -> tuple[str, str, list[str], int, int]:
    asset_id = "assembly.station.service_door.v1"
    materials = {
        "dark": material(
            "mat.station.service_door.dark",
            (0.025, 0.042, 0.065, 1),
            metallic=0.46,
            roughness=0.50,
        ),
        "armor": material(
            "mat.station.service_door.armor",
            (0.25, 0.29, 0.32, 1),
            metallic=0.62,
            roughness=0.38,
        ),
        "status": material(
            "mat.station.service_door.status_amber",
            (0.95, 0.36, 0.04, 1),
            metallic=0.10,
            roughness=0.28,
            emission=4.0,
        ),
    }
    part_specs = {
        "Frame": (
            ("frame.left", (-1.375, 1.325, 0), (0.25, 2.65, 0.28), "armor", 0.045),
            ("frame.right", (1.375, 1.325, 0), (0.25, 2.65, 0.28), "armor", 0.045),
            ("frame.header", (0, 2.525, 0), (2.50, 0.25, 0.28), "armor", 0.045),
            ("frame.track", (0, 0.075, 0), (2.50, 0.15, 0.35), "dark", 0.025),
        ),
        "Door_Left": (
            ("left.body", (-0.625, 1.25, 0), (1.25, 2.25, 0.18), "dark", 0.04),
            ("left.panel", (-0.625, 1.28, -0.105), (0.94, 1.66, 0.03), "armor", 0.025),
            ("left.seam", (-0.03, 1.25, -0.115), (0.06, 1.98, 0.03), "armor", 0.012),
        ),
        "Door_Right": (
            ("right.body", (0.625, 1.25, 0), (1.25, 2.25, 0.18), "dark", 0.04),
            ("right.panel", (0.625, 1.28, -0.105), (0.94, 1.66, 0.03), "armor", 0.025),
            ("right.seam", (0.03, 1.25, -0.115), (0.06, 1.98, 0.03), "armor", 0.012),
        ),
        "Control_Panel": (
            ("control.body", (1.355, 1.28, -0.08), (0.22, 0.48, 0.16), "dark", 0.025),
            ("control.status", (1.355, 1.36, -0.165), (0.10, 0.13, 0.02), "status", 0.008),
        ),
    }
    status_spec: BoxSpec = (
        "Status_Strip",
        (0, 2.515, -0.16),
        (0.78, 0.09, 0.03),
        "status",
        0.012,
    )
    return build_door_assembly(
        asset_id,
        "service-door-v1",
        materials,
        part_specs,
        status_spec,
    )


def build_airlock() -> tuple[str, str, list[str], int, int]:
    asset_id = "assembly.station.evacuation_airlock.v1"
    materials = {
        "dark": material(
            "mat.station.airlock.dark",
            (0.025, 0.042, 0.065, 1),
            metallic=0.46,
            roughness=0.50,
        ),
        "armor": material(
            "mat.station.airlock.armor",
            (0.25, 0.29, 0.32, 1),
            metallic=0.62,
            roughness=0.38,
        ),
        "status": material(
            "mat.station.airlock.status_green",
            (0.12, 0.92, 0.34, 1),
            metallic=0.12,
            roughness=0.28,
            emission=5.0,
        ),
    }
    part_specs = {
        "Frame": (
            ("frame.left", (-1.43, 1.40, 0), (0.34, 2.80, 0.42), "armor", 0.055),
            ("frame.right", (1.43, 1.40, 0), (0.34, 2.80, 0.42), "armor", 0.055),
            ("frame.header", (0, 2.64, 0), (2.58, 0.34, 0.42), "armor", 0.055),
            ("frame.track", (0, 0.07, 0), (2.58, 0.14, 0.34), "dark", 0.025),
        ),
        "Door_Left": (
            ("left.body", (-0.62, 1.30, 0), (1.19, 2.42, 0.22), "dark", 0.045),
            ("left.panel", (-0.62, 1.32, -0.13), (0.92, 1.80, 0.035), "armor", 0.03),
            ("left.edge", (-0.04, 1.30, -0.14), (0.08, 2.16, 0.045), "armor", 0.015),
        ),
        "Door_Right": (
            ("right.body", (0.62, 1.30, 0), (1.19, 2.42, 0.22), "dark", 0.045),
            ("right.panel", (0.62, 1.32, -0.13), (0.92, 1.80, 0.035), "armor", 0.03),
            ("right.edge", (0.04, 1.30, -0.14), (0.08, 2.16, 0.045), "armor", 0.015),
        ),
        "Control_Panel": (
            ("control.body", (1.47, 1.35, 0), (0.26, 0.55, 0.20), "dark", 0.035),
            ("control.screen", (1.47, 1.47, -0.12), (0.13, 0.15, 0.025), "status", 0.012),
        ),
    }
    status_spec: BoxSpec = (
        "Status_Header",
        (0, 2.64, -0.24),
        (0.82, 0.10, 0.035),
        "status",
        0.015,
    )
    return build_door_assembly(
        asset_id,
        "airlock-v1",
        materials,
        part_specs,
        status_spec,
    )


def build_terminal() -> tuple[str, str, list[str], int, int]:
    asset_id = "prop.station.service_terminal.v1"
    dark = material("mat.station.terminal.dark", (0.025, 0.040, 0.065, 1), metallic=0.42, roughness=0.53)
    armor = material("mat.station.terminal.armor", (0.27, 0.29, 0.32, 1), metallic=0.57, roughness=0.40)
    violet = material("mat.station.terminal.screen_violet", (0.38, 0.10, 0.90, 1), metallic=0.08, roughness=0.24, emission=4.5)
    body = join("Terminal_Body", [
        add_box("base", (0, 0.08, 0), (0.78, 0.16, 0.40), armor, bevel=0.045),
        add_box("pedestal", (0, 0.48, 0.01), (0.58, 0.68, 0.36), dark, bevel=0.045),
        add_box("shoulders", (0, 0.82, -0.015), (0.70, 0.20, 0.39), armor, bevel=0.04),
        add_box("hood", (0, 1.08, 0.015), (0.68, 0.42, 0.39), dark, bevel=0.05),
        add_box("left_rail", (-0.33, 0.68, -0.15), (0.075, 0.92, 0.075), armor, bevel=0.018),
        add_box("right_rail", (0.33, 0.68, -0.15), (0.075, 0.92, 0.075), armor, bevel=0.018),
    ])
    screen = add_box("Terminal_Screen", (0, 1.08, -0.215), (0.48, 0.30, 0.028), violet, bevel=0.025, rotation=(math.radians(-8), 0, 0))
    status = add_box("Terminal_Status", (0, 0.89, -0.214), (0.25, 0.045, 0.024), violet, bevel=0.01)
    access = add_box("Terminal_Access", (0, 0.43, -0.205), (0.37, 0.37, 0.025), armor, bevel=0.025)
    objects = [body, screen, status, access]
    for obj in objects:
        obj["asset_id"] = asset_id
    return asset_id, "terminal-v1", [obj.name for obj in objects], 4_000, 3


def mesh_bounds_godot() -> tuple[list[float], list[float]]:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in meshes:
        for corner in obj.bound_box:
            blender_point = obj.matrix_world @ Vector(corner)
            point = Vector((blender_point.x, blender_point.z, -blender_point.y))
            for axis in range(3):
                minimum[axis] = min(minimum[axis], point[axis])
                maximum[axis] = max(maximum[axis], point[axis])
    return (
        [round(value, 4) for value in minimum],
        [round(value, 4) for value in maximum],
    )


def save_export_and_validate(
    asset_id: str,
    source_name: str,
    expected_names: list[str],
    triangle_budget: int,
    material_budget: int,
    replace: bool,
) -> dict[str, object]:
    source = REPOSITORY / "art" / "source" / asset_id / f"{source_name}.blend"
    publication = REPOSITORY / "game" / "Assets" / "Published" / f"{asset_id}.glb"
    collisions = [path for path in (source, publication) if path.exists()]
    if collisions and not replace:
        raise FileExistsError("Refusing to overwrite: " + ", ".join(map(str, collisions)))
    source.parent.mkdir(parents=True, exist_ok=True)
    publication.parent.mkdir(parents=True, exist_ok=True)

    transaction_id = uuid.uuid4().hex
    staged_source = source.with_name(
        f".{source.stem}.{transaction_id}.staged{source.suffix}"
    )
    staged_publication = publication.with_name(
        f".{publication.stem}.{transaction_id}.staged{publication.suffix}"
    )
    staged_artifacts = ((source, staged_source), (publication, staged_publication))
    lock_path = publication_lock_path(source, publication)

    try:
        scene = bpy.context.scene
        scene.unit_settings.system = "METRIC"
        scene.unit_settings.scale_length = 1.0
        scene["asset_id"] = asset_id
        scene["axis_contract"] = "Blender +Z up/+Y front; exported +Y up/-Z front"
        scene["gameplay_authority"] = "Godot wrapper"
        scene["source_reference"] = "approved frontier-station structure family"

        mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
        for obj in mesh_objects:
            obj.data.calc_loop_triangles()
        triangles = sum(len(obj.data.loop_triangles) for obj in mesh_objects)
        materials = sorted({
            slot.material.name
            for obj in mesh_objects
            for slot in obj.material_slots
            if slot.material is not None
        })
        if triangles > triangle_budget:
            raise RuntimeError(
                f"{asset_id} has {triangles} triangles; budget is {triangle_budget}"
            )
        if len(materials) > material_budget:
            raise RuntimeError(
                f"{asset_id} has {len(materials)} materials; budget is {material_budget}"
            )
        source_bounds = mesh_bounds_godot()
        expected_occluders = {
            obj.name: str(obj["occluder_id"])
            for obj in mesh_objects
            if "occluder_id" in obj
        }

        bpy.ops.wm.save_as_mainfile(filepath=str(staged_source), check_existing=False)
        bpy.ops.export_scene.gltf(
            filepath=str(staged_publication),
            export_format="GLB",
            export_yup=True,
            export_apply=True,
            export_extras=True,
            export_cameras=False,
            export_lights=False,
        )

        reset_scene()
        bpy.ops.import_scene.gltf(filepath=str(staged_publication))
        imported_meshes = [
            obj for obj in bpy.context.scene.objects if obj.type == "MESH"
        ]
        imported_names = sorted(obj.name for obj in imported_meshes)
        expected_sorted_names = sorted(expected_names)
        if imported_names != expected_sorted_names:
            missing_names = sorted(set(expected_sorted_names) - set(imported_names))
            unexpected_names = sorted(set(imported_names) - set(expected_sorted_names))
            raise RuntimeError(
                "Fresh GLB reimport mesh contract mismatch; "
                f"missing={missing_names}, unexpected={unexpected_names}"
            )

        for obj in imported_meshes:
            obj.data.calc_loop_triangles()
        imported_triangles = sum(
            len(obj.data.loop_triangles) for obj in imported_meshes
        )
        imported_materials = sorted({
            slot.material.name
            for obj in imported_meshes
            for slot in obj.material_slots
            if slot.material is not None
        })
        if imported_triangles > triangle_budget:
            raise RuntimeError(
                f"Fresh GLB reimport has {imported_triangles} triangles; "
                f"budget is {triangle_budget}"
            )
        if len(imported_materials) > material_budget:
            raise RuntimeError(
                f"Fresh GLB reimport has {len(imported_materials)} materials; "
                f"budget is {material_budget}"
            )

        imported_bounds = mesh_bounds_godot()
        if imported_bounds != source_bounds:
            raise RuntimeError(
                f"Fresh GLB reimport changed bounds from {source_bounds} "
                f"to {imported_bounds}"
            )

        imported_by_name = {obj.name: obj for obj in imported_meshes}
        for object_name, expected_occluder_id in expected_occluders.items():
            imported_occluder_id = imported_by_name[object_name].get("occluder_id")
            if imported_occluder_id != expected_occluder_id:
                raise RuntimeError(
                    f"Fresh GLB reimport mesh '{object_name}' has occluder_id "
                    f"'{imported_occluder_id}'; expected '{expected_occluder_id}'"
                )

        promote_staged_artifacts(
            staged_artifacts,
            replace=replace,
            transaction_id=transaction_id,
            lock_path=lock_path,
        )
    finally:
        for _, staged in staged_artifacts:
            if staged.exists():
                staged.unlink()

    return {
        "asset_id": asset_id,
        "source": str(source.relative_to(REPOSITORY)).replace("\\", "/"),
        "publication": str(publication.relative_to(REPOSITORY)).replace("\\", "/"),
        "objects": expected_names,
        "mesh_count": len(expected_names),
        "triangles": triangles,
        "triangle_budget": triangle_budget,
        "materials": materials,
        "material_budget": material_budget,
        "fresh_reimport_triangles": imported_triangles,
        "fresh_reimport_materials": imported_materials,
        "fresh_reimport_occluders": expected_occluders,
        "bounds_godot_min": source_bounds[0],
        "bounds_godot_max": source_bounds[1],
        "fresh_reimport": True,
        "fresh_reimport_meshes": imported_names,
        "source_bytes": source.stat().st_size,
        "publication_bytes": publication.stat().st_size,
    }


def parse_arguments() -> argparse.Namespace:
    script_arguments = []
    if "--" in sys.argv:
        script_arguments = sys.argv[sys.argv.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--asset",
        required=True,
        choices=("structure", "service-door", "terminal", "airlock"),
    )
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args(script_arguments)


def main() -> None:
    arguments = parse_arguments()
    builders = {
        "structure": build_structure,
        "service-door": build_service_door,
        "terminal": build_terminal,
        "airlock": build_airlock,
    }
    reset_scene()
    asset_id, source_name, names, triangle_budget, material_budget = builders[arguments.asset]()
    report = save_export_and_validate(
        asset_id,
        source_name,
        names,
        triangle_budget,
        material_budget,
        arguments.replace,
    )
    print("SPACEADVENTURE_STATION_ASSET " + json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
