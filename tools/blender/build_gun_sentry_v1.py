"""Build the deterministic Phase 3 gun-sentry publication in Blender 5.2.

The sentry is a rigid assembly. It deliberately has no armature or authored
animation; Phase 4 drives the published pivots from authoritative gameplay.

Run from the repository root:

    blender --background --factory-startup --python-exit-code 1 \
      --python tools/blender/build_gun_sentry_v1.py

Add ``--replace`` only when intentionally rebuilding the exact source and GLB.
"""

from __future__ import annotations

import argparse
import errno
import json
import math
import os
import sys
import tempfile
import time
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

import bpy
from mathutils import Vector


ASSET_ID = "machine.security.gun_sentry.v1"
SOURCE_RELATIVE = Path("art/source") / ASSET_ID / "gun-sentry-v1.blend"
PUBLICATION_RELATIVE = Path("game/Assets/Published") / f"{ASSET_ID}.glb"
TRIANGLE_BUDGET = 8_000
MESH_BUDGET = 8
MATERIAL_NAMES = {
    "shell": "mat.security_sentry.shell.dark",
    "armor": "mat.security_sentry.armor.warm_gray",
    "threat": "mat.security_sentry.threat.red",
}
REPOSITORY = Path(
    os.environ.get("SPACE_ADVENTURE_REPOSITORY", Path(__file__).resolve().parents[2])
).resolve()


@contextmanager
def exclusive_file_lock(path: Path) -> Iterator[None]:
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    if os.name != "nt":
        path.parent.chmod(0o700)
    handle = path.open("a+b")
    try:
        if os.name != "nt":
            os.fchmod(handle.fileno(), 0o600)
        handle.seek(0, os.SEEK_END)
        if handle.tell() == 0:
            handle.write(b"\0")
            handle.flush()
        handle.seek(0)
        if os.name == "nt":
            import msvcrt

            while True:
                handle.seek(0)
                try:
                    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                    break
                except OSError as error:
                    if error.errno not in (errno.EACCES, errno.EDEADLK):
                        raise
                    time.sleep(0.1)
        else:
            import fcntl

            fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == "nt":
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
    finally:
        handle.close()


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.actions,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock)


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
    if emission:
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = emission
    return result


def godot_location(location: tuple[float, float, float]) -> tuple[float, float, float]:
    x, y, z = location
    return x, -z, y


def godot_dimensions(dimensions: tuple[float, float, float]) -> tuple[float, float, float]:
    x, y, z = dimensions
    return x, z, y


def add_box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    assigned_material: bpy.types.Material,
    *,
    bevel: float = 0.025,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=godot_location(location))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = godot_dimensions(dimensions)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(assigned_material)
    if bevel:
        modifier = obj.modifiers.new("edge_softening", "BEVEL")
        modifier.width = min(bevel, min(obj.dimensions) * 0.22)
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return obj


def add_cylinder(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    assigned_material: bpy.types.Material,
    *,
    vertices: int = 24,
    axis: str = "Y",
) -> bpy.types.Object:
    # Blender cylinders start along Z. Godot Y therefore needs no rotation;
    # Godot Z maps to Blender -Y and uses an X rotation.
    rotation = (math.pi / 2, 0.0, 0.0) if axis == "Z" else (0.0, 0.0, 0.0)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=godot_location(location),
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(assigned_material)
    bevel = obj.modifiers.new("edge_softening", "BEVEL")
    bevel.width = min(0.018, radius * 0.20)
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
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


def parent_keep_world(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_world = world


def add_empty(name: str, location: tuple[float, float, float]) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.12
    obj.location = godot_location(location)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_sentry() -> list[bpy.types.Object]:
    shell = material(
        MATERIAL_NAMES["shell"],
        (0.025, 0.045, 0.070, 1.0),
        metallic=0.64,
        roughness=0.42,
    )
    armor = material(
        MATERIAL_NAMES["armor"],
        (0.34, 0.36, 0.35, 1.0),
        metallic=0.58,
        roughness=0.38,
    )
    threat = material(
        MATERIAL_NAMES["threat"],
        (0.78, 0.018, 0.028, 1.0),
        metallic=0.12,
        roughness=0.24,
        emission=5.5,
    )

    base_parts = [
        add_cylinder("base.foot", (0, 0.07, 0), 0.45, 0.14, shell, vertices=32),
        add_cylinder("base.ring", (0, 0.16, 0), 0.34, 0.12, armor, vertices=32),
        add_box("base.lower", (0, 0.37, 0), (0.50, 0.32, 0.50), shell, bevel=0.055),
        add_box("base.column", (0, 0.83, 0), (0.32, 0.62, 0.32), armor, bevel=0.045),
        add_box("base.neck", (0, 1.22, 0), (0.46, 0.18, 0.40), shell, bevel=0.035),
    ]
    for x, z in ((-0.32, -0.24), (0.32, -0.24), (-0.32, 0.24), (0.32, 0.24)):
        base_parts.append(
            add_cylinder(f"base.bolt.{x}.{z}", (x, 0.155, z), 0.035, 0.05, armor, vertices=12)
        )
    base = join("Base", base_parts)
    base["asset_id"] = ASSET_ID
    base["rigid_assembly"] = True
    base["ground_origin"] = True
    base["forward_axis"] = "-Z"
    base["up_axis"] = "+Y"

    aim_pivot = add_empty("Aim_Pivot", (0, 1.47, 0))
    parent_keep_world(aim_pivot, base)
    aim_pivot["pivot_contract"] = "aim"
    aim_pivot["yaw_min_degrees"] = -60.0
    aim_pivot["yaw_max_degrees"] = 60.0
    aim_pivot["pitch_min_degrees"] = -15.0
    aim_pivot["pitch_max_degrees"] = 25.0

    housing_parts = [
        add_box("housing.core", (0, 1.68, 0.12), (0.84, 0.56, 0.66), shell, bevel=0.075),
        add_box("housing.front", (0, 1.66, -0.24), (0.62, 0.40, 0.12), armor, bevel=0.038),
        add_box("housing.top", (0, 2.00, 0.13), (0.62, 0.12, 0.46), armor, bevel=0.035),
        add_box("housing.crown", (0, 2.105, 0.16), (0.38, 0.09, 0.28), shell, bevel=0.028),
        add_box("housing.left", (-0.44, 1.69, 0.13), (0.12, 0.42, 0.46), armor, bevel=0.032),
        add_box("housing.right", (0.44, 1.69, 0.13), (0.12, 0.42, 0.46), armor, bevel=0.032),
        add_box("housing.vent_left", (-0.445, 1.70, 0.02), (0.025, 0.22, 0.20), shell, bevel=0.006),
        add_box("housing.vent_right", (0.445, 1.70, 0.02), (0.025, 0.22, 0.20), shell, bevel=0.006),
    ]
    housing = join("Gun_Housing", housing_parts)
    parent_keep_world(housing, aim_pivot)

    sensor = join(
        "Threat_Sensor",
        [
            add_box("sensor.recess", (0, 1.88, -0.315), (0.34, 0.13, 0.035), shell, bevel=0.010),
            add_box("sensor.lens", (0, 1.88, -0.34), (0.24, 0.065, 0.025), threat, bevel=0.014),
        ],
    )
    parent_keep_world(sensor, aim_pivot)
    sensor["presentation_role"] = "hostile_sensor"

    recoil = add_empty("Recoil", (0, 1.62, -0.30))
    parent_keep_world(recoil, aim_pivot)
    recoil["pivot_contract"] = "recoil"
    recoil["translation_axis_local"] = "+Z"
    recoil["maximum_travel_metres"] = 0.08

    barrel = join(
        "Barrel",
        [
            add_cylinder("barrel.sleeve", (0, 1.62, -0.29), 0.18, 0.14, armor, vertices=32, axis="Z"),
            add_cylinder("barrel.core", (0, 1.62, -0.40), 0.105, 0.22, shell, vertices=32, axis="Z"),
            add_cylinder("barrel.muzzle", (0, 1.62, -0.515), 0.145, 0.06, armor, vertices=32, axis="Z"),
            add_cylinder("barrel.bore", (0, 1.62, -0.547), 0.075, 0.006, shell, vertices=32, axis="Z"),
        ],
    )
    parent_keep_world(barrel, recoil)

    muzzle = add_empty("socket.attack.muzzle.primary", (0, 1.62, -0.55))
    parent_keep_world(muzzle, recoil)
    muzzle.empty_display_size = 0.08
    muzzle["socket_contract"] = "socket.attack.muzzle.primary"
    muzzle["forward_axis"] = "-Z"
    muzzle["up_axis"] = "+Y"

    return [base, aim_pivot, housing, sensor, recoil, barrel, muzzle]


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, point.x)
            minimum.y = min(minimum.y, point.y)
            minimum.z = min(minimum.z, point.z)
            maximum.x = max(maximum.x, point.x)
            maximum.y = max(maximum.y, point.y)
            maximum.z = max(maximum.z, point.z)
    return minimum, maximum


def triangle_count(objects: list[bpy.types.Object]) -> int:
    count = 0
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(dependency_graph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            count += len(mesh.loop_triangles)
        finally:
            evaluated.to_mesh_clear()
    return count


def validate_pivot_contracts(by_name: dict[str, bpy.types.Object]) -> None:
    aim = by_name["Aim_Pivot"]
    recoil = by_name["Recoil"]
    expected_numbers = {
        (aim, "yaw_min_degrees"): -60.0,
        (aim, "yaw_max_degrees"): 60.0,
        (aim, "pitch_min_degrees"): -15.0,
        (aim, "pitch_max_degrees"): 25.0,
        (recoil, "maximum_travel_metres"): 0.08,
    }
    for (obj, key), expected in expected_numbers.items():
        value = obj.get(key)
        if value is None or not math.isclose(
            float(value), expected, rel_tol=0.0, abs_tol=1e-6
        ):
            raise RuntimeError(
                f"{obj.name} contract '{key}' must equal {expected}; found {value}"
            )
    if recoil.get("translation_axis_local") != "+Z":
        raise RuntimeError("Recoil contract must translate along local +Z")
    for obj in (aim, recoil):
        if any(abs(angle) > 1e-6 for angle in obj.rotation_euler):
            raise RuntimeError(f"{obj.name} must publish an identity rest rotation")
        if any(abs(value - 1.0) > 1e-6 for value in obj.scale):
            raise RuntimeError(f"{obj.name} must publish unit scale")
    local_recoil_axis = recoil.matrix_local.to_3x3().normalized() @ Vector(
        (0.0, 0.0, 1.0)
    )
    if local_recoil_axis.dot(Vector((0.0, 0.0, 1.0))) < 0.999999:
        raise RuntimeError(
            "Recoil local +Z axis is not aligned with the authored travel axis"
        )


def validate_scene(objects: list[bpy.types.Object]) -> dict[str, object]:
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    if len(mesh_objects) > MESH_BUDGET:
        raise RuntimeError(f"Sentry publishes {len(mesh_objects)} meshes; budget is {MESH_BUDGET}")
    triangles = triangle_count(objects)
    if triangles > TRIANGLE_BUDGET:
        raise RuntimeError(f"Sentry publishes {triangles} triangles; budget is {TRIANGLE_BUDGET}")
    material_names = {material.name for material in bpy.data.materials if material.users > 0}
    if material_names != set(MATERIAL_NAMES.values()):
        raise RuntimeError(f"Unexpected material roles: {sorted(material_names)}")
    if any(obj.type == "ARMATURE" for obj in objects) or bpy.data.actions:
        raise RuntimeError("Rigid sentry must not publish an armature or authored actions")

    by_name = {obj.name: obj for obj in objects}
    required = {
        "Base",
        "Aim_Pivot",
        "Gun_Housing",
        "Threat_Sensor",
        "Recoil",
        "Barrel",
        "socket.attack.muzzle.primary",
    }
    missing = required - by_name.keys()
    if missing:
        raise RuntimeError(f"Sentry hierarchy is missing: {sorted(missing)}")
    if by_name["Aim_Pivot"].parent != by_name["Base"]:
        raise RuntimeError("Aim_Pivot must be parented to Base")
    for child_name in ("Gun_Housing", "Threat_Sensor", "Recoil"):
        if by_name[child_name].parent != by_name["Aim_Pivot"]:
            raise RuntimeError(f"{child_name} must be parented to Aim_Pivot")
    for child_name in ("Barrel", "socket.attack.muzzle.primary"):
        if by_name[child_name].parent != by_name["Recoil"]:
            raise RuntimeError(f"{child_name} must be parented to Recoil")
    validate_pivot_contracts(by_name)

    minimum, maximum = world_bounds(objects)
    # Blender coordinates are X/right, Y/back, Z/up before glTF conversion.
    width = maximum.x - minimum.x
    depth = maximum.y - minimum.y
    height = maximum.z - minimum.z
    if abs(minimum.z) > 0.005:
        raise RuntimeError(f"Sentry is not grounded: minimum Z is {minimum.z:.5f} m")
    if not 2.12 <= height <= 2.18:
        raise RuntimeError(f"Sentry height {height:.4f} m is outside 2.15 m ±0.03")
    if width > 1.001 or depth > 1.001:
        raise RuntimeError(f"Sentry footprint {width:.4f} x {depth:.4f} exceeds 1.0 m")

    return {
        "triangles": triangles,
        "meshes": len(mesh_objects),
        "materials": sorted(material_names),
        "height_metres": round(height, 5),
        "footprint_metres": [round(width, 5), round(depth, 5)],
        "minimum_blender": [round(value, 5) for value in minimum],
        "maximum_blender": [round(value, 5) for value in maximum],
    }


def reconcile_interrupted_backups(targets: tuple[Path, ...]) -> None:
    for target in targets:
        pattern = f".{target.stem}.*.backup{target.suffix}"
        backups = sorted(
            target.parent.glob(pattern),
            key=lambda candidate: candidate.stat().st_mtime_ns,
            reverse=True,
        )
        if not backups:
            continue
        if not target.exists():
            os.replace(backups.pop(0), target)
        for backup in backups:
            backup.unlink()


def save_export_and_validate(
    objects: list[bpy.types.Object], replace: bool
) -> dict[str, object]:
    source = REPOSITORY / SOURCE_RELATIVE
    publication = REPOSITORY / PUBLICATION_RELATIVE
    source.parent.mkdir(parents=True, exist_ok=True)
    publication.parent.mkdir(parents=True, exist_ok=True)

    transaction = f"{os.getpid()}-{time.time_ns()}"
    cache_override = os.environ.get("XDG_CACHE_HOME", "")
    user_cache = (
        Path(cache_override)
        if os.path.isabs(cache_override)
        else Path.home() / ".cache"
    )
    lock_path = user_cache / "space-adventure" / "space-adventure-gun-sentry-v1.lock"
    with exclusive_file_lock(lock_path):
        reconcile_interrupted_backups((source, publication))
        if not replace and (source.exists() or publication.exists()):
            raise FileExistsError(
                f"Refusing to overwrite {source} or {publication}; pass --replace"
            )
    with tempfile.TemporaryDirectory(
        prefix=".space-adventure-sentry-", dir=publication.parent
    ) as temp_directory:
        staging = Path(temp_directory)
        staged_source = staging / source.name
        staged_publication = staging / publication.name
        bpy.ops.wm.save_as_mainfile(filepath=str(staged_source), compress=True)
        bpy.ops.export_scene.gltf(
            filepath=str(staged_publication),
            export_format="GLB",
            export_yup=True,
            export_extras=True,
            export_apply=True,
            export_animations=False,
        )
        authored_metrics = validate_scene(objects)

        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(staged_publication))
        imported = list(bpy.context.scene.objects)
        imported_metrics = validate_scene(imported)

        backups: dict[Path, Path] = {}
        promoted: list[Path] = []
        with exclusive_file_lock(lock_path):
            try:
                if not replace and (source.exists() or publication.exists()):
                    raise FileExistsError("Publication collision detected after staging")
                if replace:
                    for target in (source, publication):
                        if target.exists():
                            backup = target.with_name(
                                f".{target.stem}.{transaction}.backup{target.suffix}"
                            )
                            os.replace(target, backup)
                            backups[target] = backup
                for staged, target in (
                    (staged_source, source),
                    (staged_publication, publication),
                ):
                    os.replace(staged, target)
                    promoted.append(target)
            except Exception:
                for target in promoted:
                    if target.exists():
                        target.unlink()
                for target, backup in backups.items():
                    if backup.exists():
                        os.replace(backup, target)
                raise
            else:
                for backup in backups.values():
                    if backup.exists():
                        backup.unlink()

    return {
        "asset_id": ASSET_ID,
        "source": str(SOURCE_RELATIVE).replace("\\", "/"),
        "publication": str(PUBLICATION_RELATIVE).replace("\\", "/"),
        "authored": authored_metrics,
        "fresh_reimport": imported_metrics,
        "source_bytes": source.stat().st_size,
        "publication_bytes": publication.stat().st_size,
    }


def parse_arguments() -> argparse.Namespace:
    script_arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args(script_arguments)


def main() -> None:
    arguments = parse_arguments()
    reset_scene()
    objects = build_sentry()
    report = save_export_and_validate(objects, arguments.replace)
    print("SPACEADVENTURE_GUN_SENTRY " + json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
