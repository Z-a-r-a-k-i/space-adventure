"""Build the production station-route v2 environment assemblies in Blender 5.2.

Run one explicit target from the repository root:

    blender --background --factory-startup --python-exit-code 1 \
      --python tools/blender/build_station_environment_v2.py -- \
      --asset structure

Available targets are structure, service-surround, service-door, terminal, and airlock. Add
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
import struct
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
    vertex_color = result.node_tree.nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = "COLOR_0"
    multiply = result.node_tree.nodes.new("ShaderNodeMix")
    multiply.data_type = "RGBA"
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    multiply.inputs[6].default_value = color
    result.node_tree.links.new(vertex_color.outputs["Color"], multiply.inputs[7])
    result.node_tree.links.new(multiply.outputs[2], principled.inputs["Base Color"])
    if emission > 0:
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = emission
    return result


def tint_mesh(obj: bpy.types.Object, tint: tuple[float, float, float, float]) -> bpy.types.Object:
    # Match the exported channel name so Blender 5.2 retains the same color
    # attribute across every material slot of a joined mesh.
    colors = obj.data.color_attributes.get("COLOR_0")
    if colors is None:
        colors = obj.data.color_attributes.new(name="COLOR_0", type="BYTE_COLOR", domain="CORNER")
    for color in colors.data:
        color.color = tint
    obj.data.update()
    return obj


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
    bevel_segments: int = 2,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    tint: tuple[float, float, float, float] = (1, 1, 1, 1),
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
        modifier.segments = bevel_segments
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return tint_mesh(obj, tint)


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
    deck: bpy.types.Material,
    cyan: bpy.types.Material,
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
    # All medium detail belongs to the wall's one cutaway mesh.
    length = depth if vertical else width
    count = max(1, round(length / 2.0))
    step = length / count
    for index in range(count):
        along = -length / 2 + (index + 0.5) * step
        def detail(suffix, elevation, span, thickness, surface, bevel=0.012):
            for side in (-1, 1):
                if (name == "Wall_Protector_South" and side == 1 and index in (1, 2)
                    or name == "Wall_Main_SouthEast" and side == 1):
                    continue
                location = (x + side * (width / 2 + thickness / 2), elevation, z + along) if vertical else (x + along, elevation, z + side * (depth / 2 + thickness / 2))
                size = (thickness, 0.70, span) if vertical else (span, 0.70, thickness)
                tint = (1, 1, 1, 1)
                if name.startswith("Wall_Protector") and suffix == "panel_upper":
                    surface, tint = armor, (0.70, 0.50, 0.29, 1)
                elif name in ("Wall_Solo_South", "Wall_Solo_West") and suffix == "panel_lower":
                    surface, tint = armor, (0.47, 0.44, 0.34, 1)
                elif name.startswith("Wall_Final") and suffix == "panel_upper":
                    surface, tint = armor, (0.45, 0.62, 0.54, 1)
                pieces.append(add_box(f"{name}.{suffix}_{index}_{side}", location, size, surface,
                    bevel=bevel, bevel_segments=1, tint=tint))
        detail("panel_lower", 0.90, step - 0.20, 0.035, deck)
        detail("panel_upper", 1.77, step - 0.20, 0.035, deck)
        # Short luminaires punctuate the base instead of washing entire walls.
        if index % 2 == 0:
            for side in (-1, 1):
                location = (x + side * (width / 2 + 0.026), 0.43, z + along) if vertical else (x + along, 0.43, z + side * (depth / 2 + 0.026))
                size = (0.025, 0.065, min(0.68, step - 0.3)) if vertical else (min(0.68, step - 0.3), 0.065, 0.025)
                pieces.append(add_box(f"{name}.light_{index}_{side}", location, size, cyan, bevel=0))
        if index > 0 and not (name == "Wall_Protector_South" and index == 2
                             or name == "Wall_Main_SouthEast"):
            offset = -length / 2 + index * step
            location = (x, 1.30, z + offset) if vertical else (x + offset, 1.30, z)
            size = (width + 0.10, 2.18, 0.12) if vertical else (0.12, 2.18, depth + 0.10)
            pieces.append(add_box(f"{name}.rib_{index}", location, size, armor, bevel=0.018, bevel_segments=1))
    # Large inset assemblies identify rooms, while remaining in the wall's
    # existing cutaway mesh and outside the navigable deck.
    if name == "Wall_Protector_South":
        for index, locker_x in enumerate((-2.65, -1.5, -0.35)):
            pieces.append(add_box(f"crew_locker_{index}", (locker_x, 1.23, -3.165),
                (1.02, 2.12, 0.362), armor, bevel=0.06, bevel_segments=1,
                tint=(0.90, 0.53, 0.27, 1)))
            pieces.append(add_box(f"crew_locker_inset_{index}", (locker_x, 1.37, -2.974),
                (0.70, 1.22, 0.012), dark, bevel=0.035, bevel_segments=1))
            pieces.append(add_box(f"crew_locker_handle_{index}", (locker_x + 0.22, 1.14, -2.957),
                (0.07, 0.38, 0.012), armor, bevel=0.008, bevel_segments=1))
            pieces.append(add_box(f"crew_locker_vent_{index}", (locker_x, 2.07, -2.975),
                (0.58, 0.13, 0.02), armor, bevel=0, tint=(0.32, 0.38, 0.40, 1)))
        pieces.append(add_box("crew_locker_header", (-1.5, 2.47, -2.957),
            (3.9, 0.25, 0.012), armor, bevel=0.035, bevel_segments=1,
            tint=(0.90, 0.53, 0.27, 1)))
    elif name == "Wall_Main_SouthEast":
        pieces.append(add_box("transit_power_backplate", (4, 1.28, 2.835),
            (3.7, 2.2, 0.362), armor, bevel=0.08, bevel_segments=1,
            tint=(0.37, 0.65, 0.66, 1)))
        for index, fan_x in enumerate((3.15, 4.85)):
            bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.67, depth=0.022,
                location=(fan_x, -3.031, 1.36), rotation=(math.pi / 2, 0, 0))
            fan = bpy.context.object
            fan.name = f"transit_extractor_{index}"
            fan.data.materials.append(dark)
            pieces.append(tint_mesh(fan, (1, 1, 1, 1)))
            for angle in (0, math.pi / 3, 2 * math.pi / 3):
                pieces.append(add_box(f"transit_extractor_grille_{index}_{angle:.2f}",
                    (fan_x, 1.36, 3.047), (1.16, 0.10, 0.004), armor,
                    bevel=0.012, bevel_segments=1, rotation=(0, angle, 0),
                    tint=(0.48, 0.58, 0.59, 1)))
        pieces.append(add_box("transit_power_header", (4, 2.53, 3.043),
            (3.90, 0.25, 0.012), armor, bevel=0.04, bevel_segments=1,
            tint=(0.37, 0.65, 0.66, 1)))
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
    deck: bpy.types.Material,
) -> bpy.types.Object:
    x, y, z = center
    width, height, depth = dimensions
    pieces = [add_box(f"{name}.base", center, dimensions, dark, bevel=0.045)]
    panel_span = 3 if name in ("Floor_SoloCombatArena", "Floor_MainPartyArena") else 2
    columns, rows = max(1, round(width / panel_span)), max(1, round(depth / panel_span))
    for column in range(columns):
        for row in range(rows):
            panel_x = x - width / 2 + (column + 0.5) * width / columns
            panel_z = z - depth / 2 + (row + 0.5) * depth / rows
            pieces.append(add_box(f"{name}.panel_{column}_{row}",
                (panel_x, y + height * 0.52, panel_z),
                (width / columns - 0.07, 0.025, depth / rows - 0.07), armor, bevel=0.035, bevel_segments=1))
            pieces.append(add_box(f"{name}.inset_{column}_{row}",
                (panel_x, 0.020, panel_z),
                (width / columns - 0.18, 0.008, depth / rows - 0.18), deck, bevel=0))
    # Broad perimeter trim gives the room an engineered frame at tactical range.
    for side in (-1, 1):
        pieces.append(add_box(f"{name}.edge_x_{side}", (x + side * (width / 2 - 0.11), 0.020, z),
            (0.16, 0.024, depth - 0.10), armor, bevel=0.012))
        pieces.append(add_box(f"{name}.edge_z_{side}", (x, 0.020, z + side * (depth / 2 - 0.11)),
            (width - 0.10, 0.024, 0.16), armor, bevel=0.012))
    thresholds = {
        "Floor_StartRoom": (-10, 4.35, False),
        "Floor_SoloCombatArena": (-5.35, 0, True),
        "Floor_ProtectorRoom": (0, 2.67, False),
        "Floor_FinalAirlockApproach": (11.55, 8, True),
    }
    if name in thresholds:
        tx, tz, vertical = thresholds[name]
        pieces.append(add_box(f"{name}.threshold", (tx, 0.030, tz),
            (0.34, 0.010, 2.42) if vertical else (2.42, 0.010, 0.34), armor,
            bevel=0, tint=(0.54, 0.50, 0.40, 1)))
        for index, (across, length) in enumerate(((-0.87, 0.23), (-0.35, 0.35), (0.4, 0.19), (0.87, 0.3))):
            location = (tx, 0.037, tz + across) if vertical else (tx + across, 0.037, tz)
            size = (0.22, 0.004, length) if vertical else (length, 0.004, 0.22)
            pieces.append(add_box(f"{name}.threshold_wear_{index}", location, size, deck, bevel=0))
    return join(name, pieces)


def build_structure() -> tuple[str, str, list[str], int, int]:
    asset_id = "kit.station.structure.v2"
    dark = material("mat.station.structure.dark", (0.036, 0.046, 0.057, 1), metallic=0.32, roughness=0.76)
    armor = material("mat.station.structure.armor", (0.30, 0.29, 0.27, 1), metallic=0.48, roughness=0.62)
    deck = material("mat.station.structure.deck", (0.050, 0.057, 0.064, 1), metallic=0.14, roughness=0.86)
    cyan = material("mat.station.structure.route_cyan", (0.035, 0.26, 0.30, 1), metallic=0.16, roughness=0.50, emission=1.2)

    objects: list[bpy.types.Object] = [
        floor_panel("Floor_StartRoom", (-10, -0.10, 7), (6, 0.20, 6), dark, armor, deck),
        floor_panel("Floor_SoloCombatArena", (-10, -0.10, 0), (10, 0.20, 8), dark, armor, deck),
        floor_panel("Floor_ProtectorRoom", (-1.5, -0.10, 0), (7, 0.20, 6), dark, armor, deck),
        floor_panel("Floor_MainPartyArena", (0, -0.10, 8), (12, 0.20, 10), dark, armor, deck),
        floor_panel("Floor_FinalAirlockApproach", (9, -0.10, 8), (6, 0.20, 6), dark, armor, deck),
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
    objects.extend(wall(name, occluder_id, center, dimensions, dark, armor, deck, cyan)
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


def build_service_surround() -> tuple[str, str, list[str], int, int]:
    """Recessed engineering layer; never contributes walkable or occluding geometry."""
    asset_id = "assembly.station.service_surround.v1"
    # A small baked fill keeps the service recesses legible beneath cast shadows.
    shell = material("mat.station.surround.shell", (0.012, 0.021, 0.031, 1), metallic=0.25, roughness=0.86, emission=0.22)
    frame = material("mat.station.surround.frame", (0.028, 0.040, 0.049, 1), metallic=0.30, roughness=0.84, emission=0.05)
    equipment = material("mat.station.surround.equipment", (0.070, 0.078, 0.081, 1), metallic=0.42, roughness=0.78, emission=0.025)
    lamp = material("mat.station.surround.utility_amber", (0.27, 0.105, 0.032, 1), metallic=0.0, roughness=0.70, emission=0.45)
    groups: dict[str, list[bpy.types.Object]] = {name: [] for name in
        ("Surround_Foundations", "Surround_ServiceBed", "Surround_Machinery", "Surround_UtilityLights")}

    def box(group, name, location, size, surface, bevel=0.0, tint=(1, 1, 1, 1)):
        groups[group].append(add_box(name, location, size, surface, bevel=bevel, bevel_segments=1, tint=tint))

    # Deep continuous backing covers camera orbit/zoom, with large-scale seams
    # instead of the small floor tiles reserved for the playable route.
    box("Surround_ServiceBed", "service_basin", (0, -5.1, 0), (256, 0.3, 256), shell)
    for offset in range(-120, 121, 12):
        surface = frame if abs(offset) <= 24 else shell
        box("Surround_ServiceBed", f"longitudinal_{offset}", (offset, -4.9, 0), (0.32, 0.26, 256), surface)
        box("Surround_ServiceBed", f"transverse_{offset}", (0, -4.9, offset), (256, 0.26, 0.32), surface)

    # Five separate deck foundations match the authored rooms, all below y=-.20.
    rooms = [(-10, 7, 6, 6), (-10, 0, 10, 8), (-1.5, 0, 7, 6), (0, 8, 12, 10), (9, 8, 6, 6)]
    for index, (x, z, width, depth) in enumerate(rooms):
        box("Surround_Foundations", f"foundation_{index}", (x, -1.05, z), (width + 0.30, 1.65, depth + 0.30), shell, 0.10)
        for side in (-1, 1):
            box("Surround_Foundations", f"rim_x_{index}_{side}", (x + side * (width / 2 + 0.10), -0.45, z), (0.20, 0.36, depth + 0.40), frame, 0.04)
            box("Surround_Foundations", f"rim_z_{index}_{side}", (x, -0.45, z + side * (depth / 2 + 0.10)), (width + 0.40, 0.36, 0.20), frame, 0.04)
        for dx in (-width / 2 + 0.7, width / 2 - 0.7):
            for dz in (-depth / 2 + 0.7, depth / 2 - 0.7):
                box("Surround_Foundations", f"support_{index}_{dx}_{dz}", (x + dx, -3.25, z + dz), (0.65, 3.30, 0.65), frame, 0.07)

    # Broad HVAC/heat-exchanger banks occupy the negative spaces around the
    # rooms. A recessed louver stack reads as machinery rather than extra floor.
    banks = [(-18, 0, 0), (-17, 9, 0), (-10, 13.5, 1), (-5, 17, 1), (3, 17, 1),
             (11, 15, 1), (16, 7, 0), (8, 0, 1), (-1, -7, 1), (-10, -8, 1),
             (-23, -9, 0), (22, -7, 0), (-18, 22, 1), (15, 25, 1)]
    for index, (x, z, across) in enumerate(banks):
        if index in (2, 6, 8, 11, 10, 12, 13):
            continue  # These locations have cylindrical coolant reservoirs below.
        breadth, length, height_scale = {
            0: (0.85, 0.72, 0.62), 1: (0.72, 1.0, 0.85),
            3: (1.18, 0.74, 1.25), 4: (1.18, 1.15, 1.75),
            5: (0.75, 0.70, 0.72), 7: (0.82, 0.68, 0.70),
            9: (1.08, 0.84, 1.1),
        }[index]
        def part(name, dx, y, dz, width, height, depth, surface, bevel=0):
            dx, dz = dx * breadth, dz * length
            scale_y = height_scale
            elevation = -4.8 + (y + 4.8) * scale_y
            location = (x + (dz if across else dx), elevation, z + (dx if across else dz))
            size = (depth * length, height * scale_y, width * breadth) if across else (width * breadth, height * scale_y, depth * length)
            tint = (0.78, 0.85, 0.88, 1) if index == 4 else (0.66, 0.63, 0.57, 1)
            box("Surround_Machinery", f"bank_{index}_{name}", location, size, surface, bevel, tint)
        part("plinth", 0, -4.45, 0, 3.3, 0.65, 6.8, frame, 0.12)
        part("housing", 0, -3.5, 0, 2.8, 1.4, 6.2, shell, 0.18)
        part("recess", 0, -2.77, 0, 2.3, 0.10, 5.5, shell)
        for side in (-1, 1):
            part(f"rail_{side}", side * 1.30, -2.67, 0, 0.22, 0.30, 6.0, equipment, 0.04)
        for rib in range(5):
            part(f"louver_{rib}", 0, -2.67, (rib - 2) * 1.05, 2.3, 0.22, 0.30, frame)
        # Only one end is lit; no route-like continuous luminous lines.
        lamp_height = -4.8 + (-2.69 + 4.8) * height_scale
        light_at = (x + (2.95 * length if across else 0), lamp_height, z + (0 if across else 2.95 * length))
        light_size = (0.12, 0.06, 0.70) if across else (0.70, 0.06, 0.12)
        if index in (3, 4, 7):
            box("Surround_UtilityLights", f"bank_light_{index}", light_at, light_size, lamp)

    # Bundled octagonal coolant pipes, with supports and collars at a readable scale.
    def pipe(name, start, end, radius, surface):
        a, b = Vector((start[0], -start[2], start[1])), Vector((end[0], -end[2], end[1]))
        direction = b - a
        bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=direction.length, location=(a + b) / 2)
        obj = bpy.context.object
        obj.name = name
        obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
        obj.data.materials.append(surface)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        distance_tint = (0.45, 0.48, 0.50, 1) if max(abs(start[0]), abs(start[2])) > 20 else (0.75, 0.78, 0.76, 1)
        groups["Surround_Machinery"].append(tint_mesh(obj, distance_tint))

    for index in (2, 6, 8):
        x, z, across = banks[index]
        def tank_point(cross, along, height=-3.25):
            return (x + (along if across else cross), height, z + (cross if across else along))
        box("Surround_Machinery", f"tank_rack_{index}", (x, -4.45, z),
            (6.6, 0.6, 3.4) if across else (3.4, 0.6, 6.6), frame, 0.10)
        for lane in (-0.78, 0.78):
            pipe(f"reservoir_{index}_{lane}", tank_point(lane, -2.6), tank_point(lane, 2.6), 0.65, equipment)
            for along in (-2.6, -1.7, 1.7, 2.6):
                pipe(f"reservoir_band_{index}_{lane}_{along}", tank_point(lane, along - 0.08),
                    tank_point(lane, along + 0.08), 0.71, frame)
            pipe(f"reservoir_feed_{index}_{lane}", tank_point(lane, 2.6), tank_point(lane, 3.3), 0.20, frame)
        box("Surround_UtilityLights", f"tank_light_{index}", tank_point(0, -2.8, -3.92),
            (0.12, 0.06, 0.70) if across else (0.70, 0.06, 0.12), lamp)

    for index, (x, z, length, across) in enumerate([(-15, 13, 21, True), (-15, -6, 26, True),
            (-21, -6, 22, False), (14, -6, 28, False), (-4, 21, 30, True)]):
        for lane in range(3):
            cross = (lane - 1) * 0.55
            start = (x, -3.55, z + cross) if across else (x + cross, -3.55, z)
            end = (x + length, -3.55, z + cross) if across else (x + cross, -3.55, z + length)
            pipe(f"coolant_{index}_{lane}", start, end, 0.19, equipment)
        for station in range(0, int(length), 4):
            location = (x + station, -3.85, z) if across else (x, -3.85, z + station)
            size = (0.25, 0.85, 2.1) if across else (2.1, 0.85, 0.25)
            box("Surround_Machinery", f"pipe_saddle_{index}_{station}", location, size, frame, 0.03)

    objects = [join(name, pieces) for name, pieces in groups.items()]
    for obj in objects:
        obj["asset_id"] = asset_id
        obj["presentation_only"] = True
        # This is a physical silhouette contract, not a collision proxy.
        highest = max((obj.matrix_world @ vertex.co).z for vertex in obj.data.vertices)
        if highest > -0.20:
            raise RuntimeError(f"Surround '{obj.name}' reaches the playable floor: {highest}")
    return asset_id, "service-surround-v1", [obj.name for obj in objects], 12_000, 4


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
    if "Lintel" in part_specs:
        objects.append(join("Lintel", [configured_box(spec) for spec in part_specs["Lintel"]]))
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
            roughness=0.78,
        ),
        "armor": material(
            "mat.station.service_door.armor",
            (0.33, 0.31, 0.27, 1),
            metallic=0.50,
            roughness=0.58,
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
            ("frame.track", (0, 0.075, 0), (2.50, 0.15, 0.35), "dark", 0.025),
        ),
        "Lintel": (
            ("frame.header", (0, 2.525, 0), (2.50, 0.25, 0.28), "armor", 0.045),
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
    for side_name, x in (("Left", -0.625), ("Right", 0.625)):
        key = f"Door_{side_name}"
        extra: list[BoxSpec] = []
        for front in (-1, 1):
            if front == 1:
                extra.append((f"{side_name}.rear_panel", (x, 1.28, 0.105),
                    (0.94, 1.66, 0.03), "armor", 0.025))
            extra.append((f"{side_name}.band_{front}", (x, 1.42, front * 0.124),
                (0.90, 0.20, 0.008), "dark", 0))
            for index, (offset, width) in enumerate(((-0.19, 0.23), (0.13, 0.38), (0.30, 0.12))):
                extra.append((f"{side_name}.wear_{front}_{index}",
                    (x + offset, 0.49 + index * 0.09, front * 0.124),
                    (width, 0.018, 0.008), "dark", 0))
        part_specs[key] += tuple(extra)
    for side in (-1, 1):
        part_specs["Frame"] += ((f"frame.threshold_edge_{side}",
            (0, 0.154, side * 0.135), (2.40, 0.008, 0.06), "armor", 0),)
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
        expected_colors = {name: tuple(bpy.data.materials[name].diffuse_color) for name in materials}
        expected_vertex_colors = {
            obj.name: {tuple(round(channel, 3) for channel in sample.color[:3])
                       for sample in obj.data.color_attributes["COLOR_0"].data}
            for obj in mesh_objects
        }
        expected_occluders = {
            obj.name: str(obj["occluder_id"])
            for obj in mesh_objects
            if "occluder_id" in obj
        }

        bpy.ops.wm.save_as_mainfile(filepath=str(staged_source), check_existing=False)
        bpy.context.view_layer.update()
        bpy.ops.export_scene.gltf(
            filepath=str(staged_publication),
            export_format="GLB",
            export_yup=True,
            export_apply=True,
            export_extras=True,
            export_cameras=False,
            export_lights=False,
            export_vertex_color="ACTIVE",
            export_all_vertex_colors=False,
        )

        with staged_publication.open("rb") as exported:
            exported.seek(12)
            json_length, chunk_kind = struct.unpack("<II", exported.read(8))
            if chunk_kind != 0x4E4F534A:
                raise RuntimeError("Published GLB has no leading JSON chunk")
            payload = json.loads(exported.read(json_length))
        for exported_material in payload["materials"]:
            color = exported_material.get("pbrMetallicRoughness", {}).get("baseColorFactor", (1, 1, 1, 1))
            if any(abs(actual - expected) > 0.0001 for actual, expected in
                   zip(color, expected_colors[exported_material["name"]], strict=True)):
                raise RuntimeError(f"Published GLB lost the base color for {exported_material['name']}")
        if any("COLOR_0" not in primitive["attributes"] for mesh in payload["meshes"] for primitive in mesh["primitives"]):
            raise RuntimeError("Published GLB lost an authored vertex-color layer")

        reset_scene()
        bpy.ops.import_scene.gltf(filepath=str(staged_publication))
        imported_meshes = [
            obj for obj in bpy.context.scene.objects if obj.type == "MESH"
        ]
        imported_names = sorted(obj.name for obj in imported_meshes)
        for obj in imported_meshes:
            imported_colors = {tuple(round(channel, 3) for channel in sample.color[:3])
                               for sample in obj.data.color_attributes[0].data}
            if imported_colors != expected_vertex_colors[obj.name]:
                raise RuntimeError(f"Fresh GLB reimport changed vertex colors for {obj.name}")
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
        choices=("structure", "service-surround", "service-door", "terminal", "airlock"),
    )
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args(script_arguments)


def main() -> None:
    arguments = parse_arguments()
    builders = {
        "structure": build_structure,
        "service-surround": build_service_surround,
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
