"""Review the exact Vanguard + carbine GLBs without modifying either asset.

This script evaluates one exported one-frame presentation pose, attaches the
weapon root through the full socket transform in a disposable scene, records
overlap/gap/line-of-fire measurements, and renders the result. It does not save
a .blend or either input GLB.

Run:

    blender --background --factory-startup \
      --python tools/blender/review_vanguard_carbine_assembly_v1.py -- \
      <weapon.glb> <vanguard.glb> <output-directory> <manifest.json> \
      [action-name]
"""

from __future__ import annotations

import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


ASSET_ID = "weapon.crew.vanguard_carbine.v1"
CHARACTER_ID = "character.crew.vanguard.v1"


def parse_paths() -> tuple[Path, Path, Path, Path, str]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError(
            "Expected -- <weapon.glb> <vanguard.glb> <output-dir> <manifest.json>"
        ) from exc
    values = sys.argv[separator + 1 :]
    if len(values) not in (4, 5):
        raise RuntimeError(
            "Expected weapon GLB, Vanguard GLB, output directory, manifest, "
            "and optional action name"
        )
    paths = [Path(value).resolve() for value in values[:4]]
    action_name = (
        values[4] if len(values) == 5 else "anim.humanoid.idle_armed"
    )
    return paths[0], paths[1], paths[2], paths[3], action_name


def vec(value: Vector) -> list[float]:
    return [round(float(component), 8) for component in value]


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                minimum[axis] = min(minimum[axis], point[axis])
                maximum[axis] = max(maximum[axis], point[axis])
    return minimum, maximum


def world_bvh(
    obj: bpy.types.Object,
    depsgraph: bpy.types.Depsgraph,
) -> BVHTree:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        vertices = [tuple(evaluated.matrix_world @ vertex.co) for vertex in mesh.vertices]
        polygons = [tuple(polygon.vertices) for polygon in mesh.polygons]
        tree = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)
        if tree is None:
            raise RuntimeError(f"Could not build BVH for {obj.name}")
        return tree
    finally:
        evaluated.to_mesh_clear()


def collision_metrics(
    weapon_meshes: list[bpy.types.Object],
    character_meshes: list[bpy.types.Object],
) -> dict[str, object]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    weapon_trees = [(obj.name, world_bvh(obj, depsgraph)) for obj in weapon_meshes]
    character_trees = [
        (obj.name, world_bvh(obj, depsgraph)) for obj in character_meshes
    ]
    pair_metrics: list[dict[str, object]] = []
    overlap_total = 0
    for weapon_name, weapon_tree in weapon_trees:
        for character_name, character_tree in character_trees:
            overlaps = weapon_tree.overlap(character_tree)
            count = len(overlaps)
            overlap_total += count
            if count:
                pair_metrics.append(
                    {
                        "weapon_mesh": weapon_name,
                        "character_mesh": character_name,
                        "triangle_pair_overlaps": count,
                    }
                )
    return {
        "triangle_pair_overlaps": overlap_total,
        "overlapping_mesh_pairs": pair_metrics,
    }


def weighted_geometry_center(
    armature: bpy.types.Object,
    meshes: list[bpy.types.Object],
    group_names: set[str],
    minimum_weight: float = 0.35,
) -> Vector:
    """Return an evaluated center for geometry assigned to named bones."""

    depsgraph = bpy.context.evaluated_depsgraph_get()
    points: list[Vector] = []
    for obj in meshes:
        if obj.find_armature() != armature:
            continue
        group_indexes = {
            group.index
            for group in obj.vertex_groups
            if group.name in group_names
        }
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            for source_vertex, evaluated_vertex in zip(
                obj.data.vertices,
                mesh.vertices,
            ):
                weight = sum(
                    membership.weight
                    for membership in source_vertex.groups
                    if membership.group in group_indexes
                )
                if weight >= minimum_weight:
                    points.append(evaluated.matrix_world @ evaluated_vertex.co)
        finally:
            evaluated.to_mesh_clear()
    if not points:
        raise RuntimeError(
            "Could not measure the support palm from imported skin weights"
        )
    return sum(points, Vector()) / len(points)


def muzzle_line_metrics(
    muzzle: Vector,
    direction: Vector,
    character_meshes: list[bpy.types.Object],
) -> dict[str, object]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    hits: list[dict[str, object]] = []
    direction = direction.normalized()
    for obj in character_meshes:
        tree = world_bvh(obj, depsgraph)
        hit = tree.ray_cast(muzzle + direction * 0.002, direction, 10.0)
        if hit[0] is not None and hit[3] is not None:
            hits.append(
                {
                    "character_mesh": obj.name,
                    "distance_m": round(float(hit[3]), 8),
                    "position_m": vec(hit[0]),
                }
            )
    hits.sort(key=lambda value: value["distance_m"])
    return {
        "origin_m": vec(muzzle),
        "direction": vec(direction),
        "character_hits": hits,
        "clear": not hits,
    }


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.use_nodes = True
    material.diffuse_color = color
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = 0.02
    principled.inputs["Roughness"].default_value = 0.72
    if emission_strength:
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = emission_strength
    return material


def add_area(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    size: float,
    color: tuple[float, float, float],
    target: Vector,
) -> bpy.types.Object:
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name=name, object_data=data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    point_at(obj, target)
    return obj


def add_gap_line(
    start: Vector,
    end: Vector,
    material: bpy.types.Material,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name="review.support_gap.geometry", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = 0.007
    curve.bevel_resolution = 2
    curve.materials.append(material)
    spline = curve.splines.new(type="POLY")
    spline.points.add(1)
    spline.points[0].co = (*start, 1.0)
    spline.points[1].co = (*end, 1.0)
    obj = bpy.data.objects.new(name="review.support_gap", object_data=curve)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def socket_attachment_matrix(
    armature: bpy.types.Object,
    socket: bpy.types.PoseBone,
    hand_rest_rotation,
) -> Matrix:
    socket_world = armature.matrix_world @ socket.matrix
    weapon_rotation = socket_world.to_3x3() @ hand_rest_rotation.inverted()
    return (
        Matrix.Translation(socket_world.translation)
        @ weapon_rotation.to_4x4()
    )


def attach_root(root: bpy.types.Object, transform: Matrix) -> None:
    root.matrix_world = transform
    bpy.context.view_layer.update()


def render(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    output_dir: Path,
    name: str,
    target: Vector,
    location: Vector,
    *,
    projection: str,
    ortho_scale: float | None = None,
) -> dict[str, object]:
    camera.data.type = projection
    camera.location = location
    point_at(camera, target)
    if projection == "ORTHO":
        if ortho_scale is None:
            raise RuntimeError("Orthographic render requires a scale")
        camera.data.ortho_scale = ortho_scale
    else:
        camera.data.lens_unit = "FOV"
        camera.data.angle = math.radians(48.0)
    output = output_dir / f"{name}.png"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return {
        "name": name,
        "file": output.name,
        "bytes": output.stat().st_size,
        "projection": projection.lower(),
        "camera_location_m": vec(location),
        "target_m": vec(target),
        "ortho_scale_m": ortho_scale,
    }


def main(
    weapon_glb: Path,
    character_glb: Path,
    output_dir: Path,
    manifest_path: Path,
    action_name: str,
) -> dict[str, object]:
    for path in (weapon_glb, character_glb):
        if not path.is_file():
            raise FileNotFoundError(path)
    if manifest_path.exists():
        raise FileExistsError(f"Refusing to overwrite {manifest_path}")
    output_dir.mkdir(parents=True, exist_ok=True)
    if any(output_dir.iterdir()):
        raise FileExistsError(f"Assembly output directory is not empty: {output_dir}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Exact Vanguard Carbine Assembly Review"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    character_import = bpy.ops.import_scene.gltf(
        filepath=str(character_glb),
        import_pack_images=False,
        import_shading="NORMALS",
    )
    character_objects = set(scene.objects)
    character_meshes = sorted(
        (
            obj
            for obj in character_objects
            if obj.type == "MESH"
            and not any(
                collection.name == "glTF_not_exported"
                for collection in obj.users_collection
            )
        ),
        key=lambda obj: obj.name,
    )
    armatures = [obj for obj in character_objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one Vanguard armature, found {len(armatures)}")
    armature = armatures[0]
    action_names = sorted(action.name for action in bpy.data.actions)
    action = bpy.data.actions.get(action_name)
    if action is None:
        raise RuntimeError(f"Assembly action is missing: {action_name}")
    armature.animation_data_create()
    armature.animation_data.action = action
    scene.frame_set(1)
    bpy.context.view_layer.update()

    weapon_import = bpy.ops.import_scene.gltf(
        filepath=str(weapon_glb),
        import_pack_images=False,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()
    weapon_objects = [obj for obj in scene.objects if obj not in character_objects]
    weapon_meshes = sorted(
        (obj for obj in weapon_objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    root = bpy.data.objects.get(ASSET_ID)
    primary = bpy.data.objects.get("socket.grip.primary")
    support = bpy.data.objects.get("socket.grip.support")
    muzzle = bpy.data.objects.get("socket.attack.muzzle.primary")
    if None in (root, primary, support, muzzle):
        raise RuntimeError("Weapon root or marker missing after exact assembly import")

    hand_bone = armature.pose.bones.get("socket.weapon.hand_primary")
    holster_bone = armature.pose.bones.get("socket.weapon.holster_primary")
    left_hand_bone = armature.pose.bones.get("hand_l")
    hand_rest_bone = armature.data.bones.get("socket.weapon.hand_primary")
    if None in (hand_bone, holster_bone, left_hand_bone, hand_rest_bone):
        raise RuntimeError("Vanguard attachment or left-hand bone missing")
    hand_rest_rotation = (
        armature.matrix_world @ hand_rest_bone.matrix_local
    ).to_3x3()
    hand_transform = socket_attachment_matrix(
        armature,
        hand_bone,
        hand_rest_rotation,
    )
    holster_transform = socket_attachment_matrix(
        armature,
        holster_bone,
        hand_rest_rotation,
    )
    hand_position = hand_transform.translation
    holster_position = holster_transform.translation
    left_wrist_position = armature.matrix_world @ left_hand_bone.head
    left_palm_position = weighted_geometry_center(
        armature,
        character_meshes,
        {"hand_l"},
    )

    attach_root(root, hand_transform)
    hand_weapon_minimum, hand_weapon_maximum = object_bounds(weapon_meshes)
    support_position = support.matrix_world.translation.copy()
    muzzle_position = muzzle.matrix_world.translation.copy()
    support_gap = (support_position - left_palm_position).length
    hand_collision = collision_metrics(weapon_meshes, character_meshes)
    hand_muzzle_line = muzzle_line_metrics(
        muzzle_position,
        (root.matrix_world.to_3x3() @ Vector((0.0, -1.0, 0.0))).normalized(),
        character_meshes,
    )
    hand_metrics = {
        "socket_position_m": vec(hand_position),
        "primary_grip_position_m": vec(primary.matrix_world.translation),
        "support_grip_position_m": vec(support_position),
        "left_wrist_position_m": vec(left_wrist_position),
        "left_palm_position_m": vec(left_palm_position),
        "support_hand_gap_m": round(float(support_gap), 8),
        "weapon_bounds_min_m": vec(hand_weapon_minimum),
        "weapon_bounds_max_m": vec(hand_weapon_maximum),
        "collisions": hand_collision,
        "muzzle_line": hand_muzzle_line,
    }

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = bpy.data.worlds.new("review.world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.018,
        0.026,
        0.043,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.23
    scene.world = world

    character_minimum, character_maximum = object_bounds(character_meshes)
    character_center = (character_minimum + character_maximum) * 0.5
    bpy.ops.mesh.primitive_plane_add(
        size=8.0,
        location=(0.0, 0.0, character_minimum.z - 0.004),
    )
    ground = bpy.context.active_object
    ground.name = "review.ground"
    ground.data.materials.append(
        make_material("review.ground.material", (0.055, 0.068, 0.09, 1.0))
    )
    camera_data = bpy.data.cameras.new("review.camera.data")
    camera_data.clip_start = 0.01
    camera_data.clip_end = 50.0
    camera = bpy.data.objects.new("review.camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    add_area(
        "review.key",
        (-3.6, -3.8, 4.5),
        420.0,
        2.4,
        (0.78, 0.88, 1.0),
        character_center,
    )
    add_area(
        "review.fill",
        (3.8, -2.0, 2.3),
        200.0,
        2.8,
        (0.46, 0.67, 1.0),
        character_center,
    )
    add_area(
        "review.rim",
        (1.4, 3.6, 4.2),
        360.0,
        2.0,
        (1.0, 0.43, 0.20),
        character_center,
    )
    gap_line = add_gap_line(
        left_palm_position,
        support_position,
        make_material(
            "review.support_gap.material",
            (0.95, 0.18, 0.08, 1.0),
            emission_strength=1.5,
        ),
    )

    renders: list[dict[str, object]] = []
    renders.append(
        render(
            scene,
            camera,
            output_dir,
            "hand-attachment-front-right-3q",
            Vector((0.0, -0.05, 0.94)),
            Vector((3.2, -4.2, 2.7)),
            projection="ORTHO",
            ortho_scale=2.75,
        )
    )
    renders.append(
        render(
            scene,
            camera,
            output_dir,
            "hand-attachment-close",
            (hand_weapon_minimum + hand_weapon_maximum) * 0.5,
            Vector((1.8, -1.9, 1.45)),
            projection="ORTHO",
            ortho_scale=1.25,
        )
    )
    renders.append(
        render(
            scene,
            camera,
            output_dir,
            "hand-attachment-side",
            Vector((0.0, -0.05, 0.94)),
            Vector((-4.0, -0.05, 1.05)),
            projection="ORTHO",
            ortho_scale=2.75,
        )
    )
    tactical_target = Vector((0.0, 0.0, 0.88))
    for distance in (14.5, 20.0):
        pitch = 0.90
        horizontal = distance * math.cos(pitch)
        vertical = distance * math.sin(pitch)
        renders.append(
            render(
                scene,
                camera,
                output_dir,
                f"hand-attachment-tactical-{str(distance).replace('.', '-') }m",
                tactical_target,
                Vector(
                    (
                        horizontal * 0.32,
                        -horizontal * 0.947,
                        tactical_target.z + vertical,
                    )
                ),
                projection="PERSP",
            )
        )

    attach_root(root, holster_transform)
    gap_line.hide_render = True
    holster_weapon_minimum, holster_weapon_maximum = object_bounds(weapon_meshes)
    holster_collision = collision_metrics(weapon_meshes, character_meshes)
    holster_metrics = {
        "socket_position_m": vec(holster_position),
        "primary_grip_position_m": vec(primary.matrix_world.translation),
        "weapon_bounds_min_m": vec(holster_weapon_minimum),
        "weapon_bounds_max_m": vec(holster_weapon_maximum),
        "collisions": holster_collision,
    }
    renders.append(
        render(
            scene,
            camera,
            output_dir,
            "holster-attachment-front-right-3q",
            Vector((0.0, 0.0, 0.94)),
            Vector((3.2, -4.2, 2.7)),
            projection="ORTHO",
            ortho_scale=2.75,
        )
    )
    renders.append(
        render(
            scene,
            camera,
            output_dir,
            "holster-attachment-close",
            (holster_weapon_minimum + holster_weapon_maximum) * 0.5,
            Vector((1.8, -1.9, 1.25)),
            projection="ORTHO",
            ortho_scale=1.35,
        )
    )

    defects: list[dict[str, object]] = []
    if support_gap > 0.04:
        defects.append(
            {
                "severity": "blocker",
                "code": "support-hand-pose-gap",
                "finding": (
                    f"The {action_name} left hand is {support_gap:.3f} m from "
                    "socket.grip.support."
                ),
            }
        )
    if hand_collision["triangle_pair_overlaps"]:
        defects.append(
            {
                "severity": "review",
                "code": "held-contact-overlap",
                "finding": (
                    "The fitted held pose contains mesh contact at hands/stock; "
                    "inspect the close render for visible body or armor clipping."
                ),
                "triangle_pair_overlaps": hand_collision["triangle_pair_overlaps"],
            }
        )
    if holster_collision["triangle_pair_overlaps"]:
        defects.append(
            {
                "severity": "blocker",
                "code": "holster-attachment-body-overlap",
                "finding": (
                    "The provisional holster attachment intersects the "
                    "staged Vanguard body/armor."
                ),
                "triangle_pair_overlaps": holster_collision[
                    "triangle_pair_overlaps"
                ],
            }
        )
    if not hand_muzzle_line["clear"]:
        defects.append(
            {
                "severity": "blocker",
                "code": "muzzle-line-character-hit",
                "finding": "The attached muzzle forward ray intersects Vanguard.",
                "hits": hand_muzzle_line["character_hits"],
            }
        )
    blocker_count = sum(
        1 for defect in defects if defect["severity"] == "blocker"
    )

    manifest = {
        "asset_id": ASSET_ID,
        "compatible_character": CHARACTER_ID,
        "decision": "pass" if blocker_count == 0 else "revise",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "weapon_glb": {
            "path": str(weapon_glb),
            "bytes": weapon_glb.stat().st_size,
        },
        "character_glb": {
            "path": str(character_glb),
            "bytes": character_glb.stat().st_size,
        },
        "character_import_result": sorted(character_import),
        "weapon_import_result": sorted(weapon_import),
        "character_armature": armature.name,
        "character_bones": len(armature.data.bones),
        "character_action_datablocks": action_names,
        "evaluated_action": action_name,
        "evaluated_frame": 1,
        "attachment_interpretation": (
            "Full exported pose-socket transform. The primary hand socket rest "
            "orientation is the weapon-frame correction; the holster socket "
            "therefore carries its reviewed relative rotation."
        ),
        "hand_attachment": hand_metrics,
        "holster_attachment": holster_metrics,
        "defects": defects,
        "renders": renders,
        "scope": (
            "Disposable exact staging assembly review only. Neither staging "
            "GLB nor the character source was modified. Gameplay identity, "
            "timing, range, and damage were not inferred."
        ),
    }
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    manifest["manifest_path"] = str(manifest_path)
    return manifest


weapon_path, vanguard_path, review_dir, review_manifest, pose_action = parse_paths()
result = main(
    weapon_path,
    vanguard_path,
    review_dir,
    review_manifest,
    pose_action,
)
print(json.dumps(result, indent=2, sort_keys=True))
