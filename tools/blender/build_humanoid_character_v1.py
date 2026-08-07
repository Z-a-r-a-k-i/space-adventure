"""Build a profile-defined Mixamo humanoid without changing the Vanguard builder.

Run from the repository root with Blender 5.2 LTS:

    blender --background --python tools/blender/build_humanoid_character_v1.py \
      -- --profile tools/blender/profiles/station-survivor-v1.json

Provider files remain in the ignored run-local cache. The builder stages both
the Blender source and GLB, reimports the exact staged GLB, and replaces the
published outputs only after every gate passes.
"""

from __future__ import annotations

import argparse
import array
import colorsys
import json
import math
import os
import pathlib
import shutil
import sys
import tempfile
import zipfile

import bpy


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
REQUIRED_BONES = {
    "mixamorig:Hips",
    "mixamorig:Spine",
    "mixamorig:Spine1",
    "mixamorig:Spine2",
    "mixamorig:Neck",
    "mixamorig:Head",
    "mixamorig:LeftHand",
    "mixamorig:RightHand",
    "mixamorig:LeftFoot",
    "mixamorig:RightFoot",
}


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", required=True)
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])


def repository_path(value: str) -> pathlib.Path:
    path = (REPOSITORY_ROOT / value).resolve()
    if REPOSITORY_ROOT not in path.parents:
        raise RuntimeError(f"Profile path escapes the repository: {value}")
    return path


def require_file(path: pathlib.Path) -> None:
    if not path.is_file():
        raise RuntimeError(f"Required input is missing: {path}")


def publish_all(
    pairs: tuple[tuple[pathlib.Path, pathlib.Path], ...],
) -> None:
    incoming_paths = [
        destination.with_name(destination.name + ".incoming")
        for _, destination in pairs
    ]
    try:
        for (staged, _), incoming in zip(pairs, incoming_paths, strict=True):
            shutil.copy2(staged, incoming)
        for (_, destination), incoming in zip(pairs, incoming_paths, strict=True):
            os.replace(incoming, destination)
    finally:
        for incoming in incoming_paths:
            incoming.unlink(missing_ok=True)


def import_fbx(
    path: pathlib.Path,
    global_scale: float,
) -> tuple[list[bpy.types.Object], list[bpy.types.Action]]:
    existing_objects = set(bpy.data.objects)
    existing_actions = set(bpy.data.actions)
    bpy.ops.wm.fbx_import(
        filepath=str(path),
        use_anim=True,
        global_scale=global_scale,
    )
    objects = [obj for obj in bpy.data.objects if obj not in existing_objects]
    actions = [action for action in bpy.data.actions if action not in existing_actions]
    return objects, actions


def single_armature(
    objects: list[bpy.types.Object],
    source: pathlib.Path,
) -> bpy.types.Object:
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"{source} imported {len(armatures)} armatures; expected one")
    return armatures[0]


def world_bounds(
    meshes: list[bpy.types.Object],
) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    points: list[object] = []
    for mesh in meshes:
        evaluated = mesh.evaluated_get(dependency_graph)
        temporary_mesh = evaluated.to_mesh()
        try:
            points.extend(evaluated.matrix_world @ vertex.co for vertex in temporary_mesh.vertices)
        finally:
            evaluated.to_mesh_clear()
    if not points:
        raise RuntimeError("No evaluated mesh vertices were available for bounds")
    minimum = tuple(min(point[axis] for point in points) for axis in range(3))
    maximum = tuple(max(point[axis] for point in points) for axis in range(3))
    return minimum, maximum


def measure_global_scale(source: pathlib.Path, target_height: float) -> float:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects, _ = import_fbx(source, global_scale=1.0)
    armature = single_armature(objects, source)
    if armature.animation_data is not None:
        armature.animation_data.action = None
    meshes = [obj for obj in objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Scale preflight imported {len(meshes)} meshes; expected one")
    bpy.context.view_layer.update()
    minimum, maximum = world_bounds(meshes)
    height = maximum[2] - minimum[2]
    if height <= 1e-6:
        raise RuntimeError(f"Scale preflight found invalid source height {height}")
    return target_height / height


def action_channelbags(action: bpy.types.Action) -> list[object]:
    return [
        channelbag
        for layer in action.layers
        for strip in layer.strips
        for channelbag in strip.channelbags
    ]


def rename_action(
    action: bpy.types.Action,
    name: str,
    source_clip: str,
    loop: bool,
) -> bpy.types.Action:
    action.name = name
    if action.name != name:
        raise RuntimeError(
            f"Action name {name} collides with an existing action; Blender assigned "
            f"{action.name}"
        )
    action.use_fake_user = True
    action["source_provider"] = "Mixamo"
    action["source_clip"] = source_clip
    action["root_motion"] = "source-in-place"
    action["loop_candidate"] = loop
    return action


def validate_uniform_action_samples(
    action: bpy.types.Action,
    minimum_points: int,
) -> None:
    reference_frames: tuple[float, ...] | None = None
    for channelbag in action_channelbags(action):
        for fcurve in channelbag.fcurves:
            points = fcurve.keyframe_points
            if len(points) < minimum_points:
                continue
            frames = tuple(float(point.co.x) for point in points)
            interval = frames[1] - frames[0]
            if interval <= 0.0 or any(
                not math.isclose(
                    frames[index] - frames[index - 1],
                    interval,
                    rel_tol=0.0,
                    abs_tol=1e-4,
                )
                for index in range(2, len(frames))
            ):
                raise RuntimeError(
                    f"Action {action.name} contains a non-uniformly sampled F-curve: "
                    f"{fcurve.data_path}[{fcurve.array_index}]"
                )
            if reference_frames is None:
                reference_frames = frames
                continue
            if len(frames) != len(reference_frames) or any(
                not math.isclose(frame, reference, rel_tol=0.0, abs_tol=1e-4)
                for frame, reference in zip(frames, reference_frames, strict=False)
            ):
                raise RuntimeError(
                    f"Action {action.name} has inconsistent sampled frame layouts: "
                    f"{fcurve.data_path}[{fcurve.array_index}]"
                )


def close_loop(action: bpy.types.Action, blend_frames: int) -> None:
    validate_uniform_action_samples(action, minimum_points=2)
    for channelbag in action_channelbags(action):
        for fcurve in channelbag.fcurves:
            points = fcurve.keyframe_points
            if len(points) < 2:
                continue
            first_value = points[0].co.y
            tail_start = max(1, len(points) - max(2, blend_frames) - 1)
            denominator = max(1, len(points) - 1 - tail_start)
            for index in range(tail_start, len(points)):
                weight = (index - tail_start) / denominator
                weight = weight * weight * (3.0 - 2.0 * weight)
                point = points[index]
                point.co.y = point.co.y * (1.0 - weight) + first_value * weight
                point.interpolation = "LINEAR"
            points[-1].co.y = first_value
            fcurve.update()


def phase_offset_action(action: bpy.types.Action, offset_frames: int) -> None:
    validate_uniform_action_samples(action, minimum_points=3)
    for channelbag in action_channelbags(action):
        for fcurve in channelbag.fcurves:
            points = fcurve.keyframe_points
            if len(points) < 3:
                continue
            if not math.isclose(
                points[-1].co.y,
                points[0].co.y,
                rel_tol=0.0,
                abs_tol=1e-5,
            ):
                raise RuntimeError(
                    f"Action {action.name} is not a closed loop and cannot be phase "
                    f"offset safely: {fcurve.data_path}[{fcurve.array_index}]"
                )
    for channelbag in action_channelbags(action):
        for fcurve in channelbag.fcurves:
            points = fcurve.keyframe_points
            if len(points) < 3:
                continue
            period = len(points) - 1
            offset = offset_frames % period
            values = [points[index].co.y for index in range(period)]
            rotated = values[offset:] + values[:offset]
            for index, value in enumerate(rotated):
                points[index].co.y = value
                points[index].interpolation = "LINEAR"
            points[-1].co.y = points[0].co.y
            points[-1].interpolation = "LINEAR"
            fcurve.update()
    action["phase_offset_frames"] = offset_frames


def validate_donor_skeleton(
    target: bpy.types.Object,
    donor: bpy.types.Object,
    source: pathlib.Path,
) -> None:
    transform_delta = max(
        abs(target.matrix_world[row][column] - donor.matrix_world[row][column])
        for row in range(4)
        for column in range(4)
    )
    if transform_delta > 1e-5:
        raise RuntimeError(
            f"Animation armature transform mismatch for {source}: {transform_delta:.6f}"
        )
    target_bones = {bone.name for bone in target.data.bones}
    donor_bones = {bone.name for bone in donor.data.bones}
    if donor_bones != target_bones:
        raise RuntimeError(
            f"Animation skeleton mismatch for {source}: "
            f"missing={sorted(target_bones - donor_bones)}, "
            f"extra={sorted(donor_bones - target_bones)}"
        )
    hierarchy_mismatches: list[tuple[str, str | None, str | None]] = []
    rest_pose_mismatches: list[tuple[str, float]] = []
    for bone_name in sorted(target_bones):
        target_bone = target.data.bones[bone_name]
        donor_bone = donor.data.bones[bone_name]
        target_parent = target_bone.parent.name if target_bone.parent else None
        donor_parent = donor_bone.parent.name if donor_bone.parent else None
        if target_parent != donor_parent:
            hierarchy_mismatches.append((bone_name, target_parent, donor_parent))
        delta = max(
            abs(target_bone.matrix_local[row][column] - donor_bone.matrix_local[row][column])
            for row in range(4)
            for column in range(4)
        )
        if delta > 1e-5:
            rest_pose_mismatches.append((bone_name, delta))
    if hierarchy_mismatches:
        raise RuntimeError(
            f"Animation skeleton hierarchy mismatch for {source}: "
            f"{hierarchy_mismatches[:8]}"
        )
    if rest_pose_mismatches:
        raise RuntimeError(
            f"Animation rest-pose mismatch for {source}: {rest_pose_mismatches[:8]}"
        )


def load_action(
    source: pathlib.Path,
    global_scale: float,
    target_armature: bpy.types.Object,
    spec: dict[str, object],
) -> bpy.types.Action:
    objects, actions = import_fbx(source, global_scale)
    donor = single_armature(objects, source)
    if len(actions) != 1:
        raise RuntimeError(f"{source} imported {len(actions)} actions; expected one")
    validate_donor_skeleton(target_armature, donor, source)
    action = rename_action(
        actions[0],
        str(spec["name"]),
        str(spec["source_clip"]),
        bool(spec.get("loop", False)),
    )
    if bool(spec.get("loop", False)):
        close_loop(action, int(spec.get("loop_blend_frames", 6)))
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)
    return action


def remove_bones(
    armature: bpy.types.Object,
    actions: list[bpy.types.Action],
    names: list[str],
) -> None:
    if not names:
        return
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        removable = []
        for name in names:
            bone = armature.data.edit_bones.get(name)
            if bone is None:
                raise RuntimeError(f"Requested removable bone is missing: {name}")
            if bone.children:
                raise RuntimeError(
                    f"Removable bone {name} has children: "
                    f"{sorted(child.name for child in bone.children)}"
                )
            removable.append(bone)
        for bone in removable:
            armature.data.edit_bones.remove(bone)
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")
    for action in actions:
        for channelbag in action_channelbags(action):
            for fcurve in list(channelbag.fcurves):
                if any(f'pose.bones["{name}"]' in fcurve.data_path for name in names):
                    channelbag.fcurves.remove(fcurve)
    associated_meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and (
            obj.parent == armature
            or any(
                modifier.type == "ARMATURE" and modifier.object == armature
                for modifier in obj.modifiers
            )
        )
    ]
    for mesh in associated_meshes:
        for name in names:
            vertex_group = mesh.vertex_groups.get(name)
            if vertex_group is not None:
                mesh.vertex_groups.remove(vertex_group)


def limit_skin_influences(mesh: bpy.types.Object, limit: int) -> int:
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=limit)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    maximum = 0
    unweighted = 0
    for vertex in mesh.data.vertices:
        weighted = [group for group in vertex.groups if group.weight > 1e-6]
        maximum = max(maximum, len(weighted))
        if not weighted:
            unweighted += 1
    if unweighted:
        raise RuntimeError(f"Mesh has {unweighted} unweighted vertices")
    if maximum > limit:
        raise RuntimeError(f"Mesh retains {maximum} influences; limit is {limit}")
    return maximum


def load_texture_set(
    archive_path: pathlib.Path,
    maximum_size: int,
    asset_id: str,
) -> dict[str, bpy.types.Image]:
    semantics = {
        "base_color": "basecolor",
        "normal": "normal",
        "roughness": "roughness",
        "metallic": "metallic",
    }
    loaded: dict[str, bpy.types.Image] = {}
    with zipfile.ZipFile(archive_path) as archive:
        members = [name for name in archive.namelist() if not name.endswith("/")]
        ambiguous_members = {
            name: [
                semantic
                for semantic, token in semantics.items()
                if token in pathlib.PurePosixPath(name).name.lower()
            ]
            for name in members
        }
        ambiguous_members = {
            name: matches
            for name, matches in ambiguous_members.items()
            if len(matches) > 1
        }
        if ambiguous_members:
            raise RuntimeError(
                f"Textures in {archive_path} match multiple semantics: "
                f"{ambiguous_members}"
            )
        with tempfile.TemporaryDirectory() as temporary_directory:
            temporary_root = pathlib.Path(temporary_directory)
            for semantic, token in semantics.items():
                matches = [
                    name
                    for name in members
                    if token in pathlib.PurePosixPath(name).name.lower()
                ]
                if len(matches) != 1:
                    raise RuntimeError(
                        f"Expected one {semantic} texture in {archive_path}; found {matches}"
                    )
                member = matches[0]
                texture_path = temporary_root / pathlib.PurePosixPath(member).name
                texture_path.write_bytes(archive.read(member))
                image = bpy.data.images.load(str(texture_path), check_existing=False)
                image.colorspace_settings.name = (
                    "sRGB" if semantic == "base_color" else "Non-Color"
                )
                if max(image.size) > maximum_size:
                    width, height = image.size
                    ratio = maximum_size / max(width, height)
                    image.scale(
                        max(1, round(width * ratio)),
                        max(1, round(height * ratio)),
                    )
                    resized_path = temporary_root / f"{semantic}-{maximum_size}.png"
                    image.filepath_raw = str(resized_path)
                    image.file_format = "PNG"
                    image.save()
                    bpy.data.images.remove(image)
                    image = bpy.data.images.load(str(resized_path), check_existing=False)
                    image.colorspace_settings.name = (
                        "sRGB" if semantic == "base_color" else "Non-Color"
                    )
                image.name = f"{asset_id}.{semantic}"
                image.pack()
                loaded[semantic] = image
    return loaded


def build_material(
    name: str,
    images: dict[str, bpy.types.Image],
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    if material.name != name:
        raise RuntimeError(
            f"Material name {name} collides with an existing material; Blender "
            f"assigned {material.name}"
        )
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    base = nodes.new("ShaderNodeTexImage")
    base.image = images["base_color"]
    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.image = images["normal"]
    normal_map = nodes.new("ShaderNodeNormalMap")
    roughness = nodes.new("ShaderNodeTexImage")
    roughness.image = images["roughness"]
    metallic = nodes.new("ShaderNodeTexImage")
    metallic.image = images["metallic"]
    links.new(base.outputs["Color"], principled.inputs["Base Color"])
    links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    links.new(roughness.outputs["Color"], principled.inputs["Roughness"])
    links.new(metallic.outputs["Color"], principled.inputs["Metallic"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def sample_base_color(
    image_pixels: array.array,
    width: int,
    height: int,
    u: float,
    v: float,
) -> tuple[float, float, float]:
    clamped_u = min(1.0, max(0.0, u))
    clamped_v = min(1.0, max(0.0, v))
    x = min(width - 1, max(0, int(clamped_u * (width - 1))))
    y = min(height - 1, max(0, int(clamped_v * (height - 1))))
    index = (y * width + x) * 4
    return tuple(image_pixels[index + channel] for channel in range(3))


def assign_semantic_materials(
    mesh: bpy.types.Object,
    material_specs: list[dict[str, str]],
    images: dict[str, bpy.types.Image],
    strategy: str,
) -> list[bpy.types.Material]:
    # Role materials intentionally share one texture set. Their stable IDs and
    # object split allow later Godot overrides without republishing topology.
    materials = [build_material(spec["name"], images) for spec in material_specs]
    roles = {spec["role"]: index for index, spec in enumerate(material_specs)}
    required_roles_by_strategy = {
        "survivor-five-role": {"base", "protection", "accent", "skin", "hair"},
        "protector-two-role": {"surface", "undersuit"},
    }
    if strategy not in required_roles_by_strategy:
        raise RuntimeError(f"Unsupported humanoid material strategy: {strategy}")
    required_roles = required_roles_by_strategy[strategy]
    if set(roles) != required_roles:
        raise RuntimeError(
            f"Humanoid material roles for {strategy} must be {sorted(required_roles)}"
        )
    mesh.data.materials.clear()
    for material in materials:
        mesh.data.materials.append(material)

    uv_layer = mesh.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Humanoid mesh has no active UV layer")
    base_image = images["base_color"]
    width, height = base_image.size
    pixels = array.array("f", [0.0]) * (width * height * 4)
    base_image.pixels.foreach_get(pixels)
    world_centers = [mesh.matrix_world @ polygon.center for polygon in mesh.data.polygons]
    minimum_z = min(center.z for center in world_centers)
    maximum_z = max(center.z for center in world_centers)
    minimum_x = min(center.x for center in world_centers)
    maximum_x = max(center.x for center in world_centers)
    center_x = (minimum_x + maximum_x) * 0.5
    width_x = maximum_x - minimum_x
    counts = [0] * len(materials)

    for polygon, center in zip(mesh.data.polygons, world_centers, strict=True):
        coordinates = [uv_layer.data[index].uv for index in polygon.loop_indices]
        u = sum(coordinate.x for coordinate in coordinates) / len(coordinates)
        v = sum(coordinate.y for coordinate in coordinates) / len(coordinates)
        red, green, blue = sample_base_color(pixels, width, height, u, v)
        hue, saturation, value = colorsys.rgb_to_hsv(red, green, blue)
        height_ratio = (center.z - minimum_z) / max(1e-6, maximum_z - minimum_z)
        if strategy == "protector-two-role":
            role = (
                "undersuit"
                if 0.48 <= hue <= 0.75 and saturation > 0.12 and value < 0.62
                else "surface"
            )
        else:
            warm = hue < 0.17 or hue > 0.96
            exposed_hand = abs(center.x - center_x) > width_x * 0.38
            if height_ratio > 0.89 and value < 0.45:
                role = "hair"
            elif warm and saturation > 0.18 and (height_ratio > 0.73 or exposed_hand):
                role = "skin"
            elif warm and saturation > 0.42 and value > 0.28:
                role = "accent"
            elif 0.48 <= hue <= 0.75 and saturation > 0.12:
                role = "base"
            else:
                role = "protection"
        polygon.material_index = roles[role]
        counts[roles[role]] += 1

    smallest = sorted(mesh.data.polygons, key=lambda polygon: polygon.area)
    for material_index, count in enumerate(counts):
        if count:
            continue
        polygon = next(
            (
                candidate
                for candidate in smallest
                if counts[candidate.material_index] > 1
            ),
            None,
        )
        if polygon is None:
            raise RuntimeError(
                f"No donor polygon is available for material index {material_index}"
            )
        counts[polygon.material_index] -= 1
        polygon.material_index = material_index
        counts[material_index] += 1
    return materials


def split_by_material(
    mesh: bpy.types.Object,
    display_name: str,
) -> list[bpy.types.Object]:
    existing = set(bpy.data.objects)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH" and (obj is mesh or obj not in existing)]
    for part in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        part.select_set(True)
        bpy.context.view_layer.objects.active = part
        bpy.ops.object.material_slot_remove_unused()
        material_name = part.material_slots[0].material.name if part.material_slots else "unknown"
        suffix = material_name.split(".")[-1]
        part.name = f"{display_name}Body.{suffix}"
        part.data.name = f"{part.name}Mesh"
    return sorted(meshes, key=lambda obj: obj.name)


def ground_objects(
    imported_objects: list[bpy.types.Object],
    meshes: list[bpy.types.Object],
) -> tuple[float, float, float]:
    minimum, maximum = world_bounds(meshes)
    center_x = (minimum[0] + maximum[0]) * 0.5
    center_y = (minimum[1] + maximum[1]) * 0.5
    imported_set = set(imported_objects)
    roots = [obj for obj in imported_objects if obj.parent not in imported_set]
    if not roots:
        raise RuntimeError("Humanoid import has no transform root to ground")
    for root in roots:
        root.location.x -= center_x
        root.location.y -= center_y
        root.location.z -= minimum[2]
    bpy.context.view_layer.update()
    adjusted_minimum, adjusted_maximum = world_bounds(meshes)
    adjusted_center_x = (adjusted_minimum[0] + adjusted_maximum[0]) * 0.5
    adjusted_center_y = (adjusted_minimum[1] + adjusted_maximum[1]) * 0.5
    if abs(adjusted_minimum[2]) > 0.002:
        raise RuntimeError(f"Grounding left a {adjusted_minimum[2]:.5f} m foot offset")
    if max(abs(adjusted_center_x), abs(adjusted_center_y)) > 0.002:
        raise RuntimeError(
            f"Planar center is displaced: x={adjusted_center_x:.5f}, y={adjusted_center_y:.5f}"
        )
    return adjusted_minimum[2], adjusted_maximum[2], adjusted_center_x


def add_socket(
    armature: bpy.types.Object,
    spec: dict[str, object],
) -> bpy.types.Object:
    bone = str(spec["bone"])
    if armature.data.bones.get(bone) is None:
        raise RuntimeError(f"Socket {spec['name']} references missing bone {bone}")
    socket = bpy.data.objects.new(str(spec["name"]), None)
    socket.empty_display_type = "ARROWS"
    socket.empty_display_size = 0.08
    socket.parent = armature
    socket.parent_type = "BONE"
    socket.parent_bone = bone
    socket.location = tuple(float(value) for value in spec.get("location", [0, 0, 0]))
    socket.rotation_euler = tuple(
        math.radians(float(value)) for value in spec.get("rotation_degrees", [0, 0, 0])
    )
    socket["socket_contract"] = str(spec["name"])
    socket["local_forward"] = "-Z"
    socket["local_up"] = "+Y"
    bpy.context.scene.collection.objects.link(socket)
    return socket


def validate_action_motion(
    armature: bpy.types.Object,
    action: bpy.types.Action,
    minimum_foot_lift: float | None,
    maximum_planar_range: float,
    maximum_vertical_range: float,
    maximum_endpoint_delta: float,
) -> dict[str, float]:
    scene = bpy.context.scene
    previous_action = armature.animation_data.action if armature.animation_data else None
    previous_frame = scene.frame_current
    previous_basis = {
        bone.name: bone.matrix_basis.copy() for bone in armature.pose.bones
    }
    armature.animation_data_create()
    armature.animation_data.action = action
    frames = range(math.floor(action.frame_range[0]), math.ceil(action.frame_range[1]) + 1)
    hips: list[tuple[float, float, float]] = []
    left_foot: list[float] = []
    right_foot: list[float] = []
    try:
        for frame in frames:
            scene.frame_set(frame)
            translation = (
                armature.matrix_world @ armature.pose.bones["mixamorig:Hips"].matrix
            ).translation
            hips.append(tuple(translation))
            left_foot.append(
                (
                    armature.matrix_world
                    @ armature.pose.bones["mixamorig:LeftFoot"].matrix
                ).translation.z
            )
            right_foot.append(
                (
                    armature.matrix_world
                    @ armature.pose.bones["mixamorig:RightFoot"].matrix
                ).translation.z
            )
    finally:
        armature.animation_data.action = previous_action
        scene.frame_set(previous_frame)
        if previous_action is None:
            for bone_name, matrix in previous_basis.items():
                armature.pose.bones[bone_name].matrix_basis = matrix
        bpy.context.view_layer.update()
    planar_range = max(
        max(sample[axis] for sample in hips) - min(sample[axis] for sample in hips)
        for axis in (0, 1)
    )
    vertical_range = max(sample[2] for sample in hips) - min(sample[2] for sample in hips)
    endpoint_delta = math.sqrt(
        sum((hips[-1][axis] - hips[0][axis]) ** 2 for axis in range(3))
    )
    left_foot_lift = max(left_foot) - min(left_foot)
    right_foot_lift = max(right_foot) - min(right_foot)
    if (
        planar_range > maximum_planar_range
        or vertical_range > maximum_vertical_range
        or endpoint_delta > maximum_endpoint_delta
    ):
        raise RuntimeError(
            f"Action {action.name} fails in-place loop limits: "
            f"planar={planar_range:.5f}, vertical={vertical_range:.5f}, "
            f"endpoint={endpoint_delta:.5f}; maximums="
            f"{maximum_planar_range:.5f}/{maximum_vertical_range:.5f}/"
            f"{maximum_endpoint_delta:.5f}"
        )
    if minimum_foot_lift is not None and min(left_foot_lift, right_foot_lift) < minimum_foot_lift:
        raise RuntimeError(
            f"Action {action.name} lacks an alternating walk cycle: "
            f"left_lift={left_foot_lift:.5f}, right_lift={right_foot_lift:.5f}, "
            f"minimum={minimum_foot_lift:.5f}"
        )
    return {
        "planar_range": round(planar_range, 5),
        "vertical_range": round(vertical_range, 5),
        "endpoint_delta": round(endpoint_delta, 5),
        "left_foot_lift": round(left_foot_lift, 5),
        "right_foot_lift": round(right_foot_lift, 5),
    }


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.fps = 30
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene.unit_settings.scale_length = 1.0


def validate_exported_glb(
    path: pathlib.Path,
    profile: dict[str, object],
) -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    configure_scene()
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"GLB reimported {len(armatures)} armatures; expected one")
    if not int(profile["mesh_minimum"]) <= len(meshes) <= int(profile["mesh_limit"]):
        raise RuntimeError(f"GLB reimported invalid mesh count {len(meshes)}")
    unique_materials = {
        slot.material.name
        for mesh in meshes
        for slot in mesh.material_slots
        if slot.material is not None
    }
    if not int(profile["material_minimum"]) <= len(unique_materials) <= int(profile["material_limit"]):
        raise RuntimeError(f"GLB reimported invalid material count {len(unique_materials)}")
    for obj in bpy.context.scene.objects:
        if obj.animation_data is None:
            continue
        obj.animation_data.action = None
        for track in obj.animation_data.nla_tracks:
            track.mute = True
    reference_action_name = str(profile["actions"][0]["name"])
    reference_action = bpy.data.actions.get(reference_action_name)
    if reference_action is None:
        raise RuntimeError(f"GLB is missing reference action {reference_action_name}")
    armatures[0].animation_data_create()
    armatures[0].animation_data.action = reference_action
    bpy.context.scene.frame_set(math.floor(reference_action.frame_range[0]))
    bpy.context.view_layer.update()
    minimum, maximum = world_bounds(meshes)
    diagnostic_dimensions = [maximum[axis] - minimum[axis] for axis in range(3)]
    bone_count = len(armatures[0].data.bones)
    if bone_count > int(profile["bone_limit"]):
        raise RuntimeError(f"GLB reimported {bone_count} bones")
    expected_actions = {str(spec["name"]) for spec in profile["actions"]}
    imported_actions = {action.name for action in bpy.data.actions}
    if imported_actions != expected_actions:
        raise RuntimeError(
            f"GLB action mismatch: expected={sorted(expected_actions)}, "
            f"actual={sorted(imported_actions)}"
        )
    maximum_texture = int(profile["texture_maximum"])
    oversized = [image.name for image in bpy.data.images if max(image.size) > maximum_texture]
    if oversized:
        raise RuntimeError(f"GLB contains oversized textures: {oversized}")
    expected_sockets = {str(spec["name"]) for spec in profile.get("sockets", [])}
    imported_sockets = {
        obj.name
        for obj in bpy.context.scene.objects
        if obj.type == "EMPTY" and obj.name.startswith("socket.")
    }
    if imported_sockets != expected_sockets:
        raise RuntimeError(
            f"GLB socket mismatch: expected={sorted(expected_sockets)}, "
            f"actual={sorted(imported_sockets)}"
        )
    triangles = sum(
        sum(len(polygon.vertices) - 2 for polygon in mesh.data.polygons)
        for mesh in meshes
    )
    if triangles > int(profile["triangle_limit"]):
        raise RuntimeError(f"GLB has {triangles} triangles")
    return {
        "meshes": len(meshes),
        "materials": len(unique_materials),
        "triangles": triangles,
        "bones": bone_count,
        "blender_reimport_pose_bounds_diagnostic": {
            "minimum": [round(value, 5) for value in minimum],
            "maximum": [round(value, 5) for value in maximum],
            "dimensions": [round(value, 5) for value in diagnostic_dimensions],
        },
        "actions": sorted(imported_actions),
        "sockets": sorted(imported_sockets),
    }


def main() -> None:
    args = arguments()
    profile_path = repository_path(args.profile)
    profile = json.loads(profile_path.read_text(encoding="utf-8"))
    rigged_source = repository_path(str(profile["rigged_source"]))
    texture_archive = repository_path(str(profile["texture_archive"]))
    blend_output = repository_path(str(profile["blend_output"]))
    glb_output = repository_path(str(profile["glb_output"]))
    action_sources = {
        repository_path(str(spec["source"]))
        for spec in profile["actions"]
        if "source" in spec
    }
    for path in (rigged_source, texture_archive, *action_sources):
        require_file(path)

    target_height = float(profile["target_height_meters"])
    global_scale = measure_global_scale(rigged_source, target_height)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    configure_scene()
    base_objects, base_actions = import_fbx(rigged_source, global_scale)
    armature = single_armature(base_objects, rigged_source)
    base_meshes = [obj for obj in base_objects if obj.type == "MESH"]
    if len(base_meshes) != 1:
        raise RuntimeError(f"Rigged source imported {len(base_meshes)} meshes; expected one")
    mesh = base_meshes[0]
    armature.name = f"{profile['display_name']}Rig"
    armature.data.name = f"{profile['display_name']}Skeleton"
    mesh.name = f"{profile['display_name']}Body"
    mesh.data.name = f"{profile['display_name']}BodyMesh"
    for action in base_actions:
        bpy.data.actions.remove(action)

    bone_names = {bone.name for bone in armature.data.bones}
    missing_required = sorted(REQUIRED_BONES - bone_names)
    if missing_required:
        raise RuntimeError(f"Rig is missing required bones: {missing_required}")

    actions_by_name: dict[str, bpy.types.Action] = {}
    action_specs_by_name: dict[str, dict[str, object]] = {}
    actions: list[bpy.types.Action] = []
    for spec in profile["actions"]:
        name = str(spec["name"])
        if "copy_of" in spec:
            source_name = str(spec["copy_of"])
            if source_name not in actions_by_name:
                raise RuntimeError(f"Action {name} copies unknown action {source_name}")
            action = actions_by_name[source_name].copy()
            rename_action(
                action,
                name,
                str(spec["source_clip"]),
                bool(spec.get("loop", False)),
            )
            if "phase_offset_frames" in spec:
                phase_offset_action(action, int(spec["phase_offset_frames"]))
        else:
            action = load_action(
                repository_path(str(spec["source"])),
                global_scale,
                armature,
                spec,
            )
        actions_by_name[name] = action
        action_specs_by_name[name] = spec
        actions.append(action)

    remove_bones(armature, actions, [str(name) for name in profile.get("remove_bones", [])])
    bone_names = {bone.name for bone in armature.data.bones}
    if len(bone_names) > int(profile["bone_limit"]):
        raise RuntimeError(f"Rig has {len(bone_names)} bones after cleanup")

    maximum_influences = limit_skin_influences(mesh, int(profile["influence_limit"]))
    images = load_texture_set(
        texture_archive,
        int(profile["texture_maximum"]),
        str(profile["asset_id"]),
    )
    texture_sizes = {name: list(image.size) for name, image in images.items()}
    materials = assign_semantic_materials(
        mesh,
        profile["materials"],
        images,
        str(profile["material_strategy"]),
    )
    meshes = split_by_material(mesh, str(profile["display_name"]))
    if not int(profile["mesh_minimum"]) <= len(meshes) <= int(profile["mesh_limit"]):
        raise RuntimeError(f"Publication has invalid mesh count {len(meshes)}")

    armature.animation_data_create()
    armature.animation_data.action = actions[0]
    bpy.context.scene.frame_set(math.floor(actions[0].frame_range[0]))
    bpy.context.view_layer.update()
    ground_minimum, standing_maximum, center_x = ground_objects(base_objects, meshes)
    height = standing_maximum - ground_minimum
    tolerance = float(profile["height_tolerance_ratio"])
    if not target_height * (1.0 - tolerance) <= height <= target_height * (1.0 + tolerance):
        raise RuntimeError(f"Evaluated height {height:.5f} m is outside tolerance")

    sockets = [add_socket(armature, spec) for spec in profile.get("sockets", [])]
    for part in meshes:
        part["asset_id"] = str(profile["asset_id"])
        part["production_run"] = str(profile["run_id"])
    armature["rig_provider"] = "Mixamo"
    armature["source_pose"] = "T-pose"
    action_metrics = {
        action.name: validate_action_motion(
            armature,
            action,
            (
                float(action_specs_by_name[action.name]["minimum_foot_lift_meters"])
                if "minimum_foot_lift_meters" in action_specs_by_name[action.name]
                else None
            ),
            float(profile.get("maximum_planar_range_meters", 0.25)),
            float(profile.get("maximum_vertical_range_meters", 0.25)),
            float(profile.get("maximum_loop_endpoint_delta_meters", 0.03)),
        )
        for action in actions
    }

    triangles = sum(
        sum(len(polygon.vertices) - 2 for polygon in part.data.polygons)
        for part in meshes
    )
    if triangles > int(profile["triangle_limit"]):
        raise RuntimeError(f"Publication has {triangles} triangles")
    if not int(profile["material_minimum"]) <= len(materials) <= int(profile["material_limit"]):
        raise RuntimeError(f"Publication has invalid material count {len(materials)}")

    blend_output.parent.mkdir(parents=True, exist_ok=True)
    glb_output.parent.mkdir(parents=True, exist_ok=True)
    armature.animation_data.action = actions[0]
    bpy.context.scene.frame_set(math.floor(actions[0].frame_range[0]))
    bpy.context.view_layer.update()
    with tempfile.TemporaryDirectory(prefix="space-adventure-humanoid-") as staging_directory:
        staging_root = pathlib.Path(staging_directory)
        staged_blend = staging_root / blend_output.name
        staged_glb = staging_root / glb_output.name
        bpy.ops.wm.save_as_mainfile(filepath=str(staged_blend), compress=True)
        bpy.ops.object.select_all(action="DESELECT")
        for obj in (armature, *meshes, *sockets):
            obj.select_set(True)
        bpy.context.view_layer.objects.active = armature
        bpy.ops.export_scene.gltf(
            filepath=str(staged_glb),
            export_format="GLB",
            use_selection=True,
            export_animations=True,
            export_animation_mode="ACTIONS",
            export_merge_animation="ACTION",
            export_skins=True,
            export_def_bones=True,
            export_yup=True,
            export_apply=False,
            export_extras=True,
            export_materials="EXPORT",
            export_image_format="AUTO",
            export_optimize_animation_size=True,
            export_force_sampling=True,
        )
        exported = validate_exported_glb(staged_glb, profile)
        publish_all(
            (
                (staged_blend, blend_output),
                (staged_glb, glb_output),
            )
        )

    report = {
        "asset_id": profile["asset_id"],
        "profile": str(profile_path.relative_to(REPOSITORY_ROOT)),
        "fbx_global_scale": round(global_scale, 7),
        "height_meters": round(height, 5),
        "center_x_meters": round(center_x, 5),
        "triangles": triangles,
        "meshes": len(meshes),
        "materials": len(materials),
        "texture_set_count": 1,
        "texture_sizes": texture_sizes,
        "bones": len(bone_names),
        "maximum_skin_influences": maximum_influences,
        "action_metrics": action_metrics,
        "exported_glb": exported,
        "blend_output": str(blend_output.relative_to(REPOSITORY_ROOT)),
        "glb_output": str(glb_output.relative_to(REPOSITORY_ROOT)),
    }
    print("HUMANOID_BUILD=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
