"""Build the conforming Vanguard Mixamo source and Godot GLB.

Run with Blender 5.2 LTS from the repository root:

    blender --background --python tools/blender/build_vanguard_character_v1.py

The provider FBX files remain in the ignored workstation cache recorded by the
Vanguard raw-export manifest.  This script is the reproducible normalization,
clip-assembly, and publication boundary.
"""

from __future__ import annotations

import json
import math
import pathlib
import tempfile
import zipfile

import bpy


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
RUN_ROOT = (
    REPOSITORY_ROOT
    / "art"
    / "generated"
    / "character.crew.vanguard.v1"
    / "prod-tripo-v31bq-20260803-02"
)
MIXAMO_CACHE = RUN_ROOT / "raw" / "mixamo"
RIGGED_SOURCE = MIXAMO_CACHE / "vanguard-unarmed-idle-with-skin.fbx"
WALK_SOURCE = MIXAMO_CACHE / "vanguard-standard-walk-in-place-with-skin.fbx"
DRAW_SOURCE = MIXAMO_CACHE / "grab-rifle-from-back-no-skin.fbx"
ARMED_IDLE_SOURCE = MIXAMO_CACHE / "rifle-aiming-idle-no-skin.fbx"
ARMED_WALK_SOURCE = MIXAMO_CACHE / "rifle-walk-in-place-no-skin.fbx"
FIRE_SOURCE = MIXAMO_CACHE / "firing-rifle-no-skin.fbx"
HOLSTER_SOURCE = MIXAMO_CACHE / "put-back-rifle-no-skin.fbx"
DOWN_SOURCE = MIXAMO_CACHE / "rifle-death-no-skin.fbx"
TRIPO_TEXTURE_SOURCE = RUN_ROOT / "raw" / "vanguard-tpose-quad10k-4k.zip"
BLEND_OUTPUT = (
    REPOSITORY_ROOT
    / "art"
    / "source"
    / "character.crew.vanguard.v1"
    / "vanguard-v1.blend"
)
GLB_OUTPUT = (
    REPOSITORY_ROOT
    / "game"
    / "Assets"
    / "Published"
    / "character.crew.vanguard.v1.glb"
)

TARGET_HEIGHT_METERS = 1.82
MINIMUM_HEIGHT_METERS = 1.7836
MAXIMUM_HEIGHT_METERS = 1.8564
EXPECTED_BONES = (
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
)
IDLE_ACTION = "anim.humanoid.idle_holstered"
LOCOMOTION_ACTION = "anim.humanoid.locomotion_holstered"
WALK_ACTION = "anim.humanoid.walk_holstered"
DRAW_ACTION = "anim.humanoid.draw_primary"
ARMED_IDLE_ACTION = "anim.humanoid.idle_armed"
ARMED_LOCOMOTION_ACTION = "anim.humanoid.locomotion_armed"
FIRE_ACTION = "anim.humanoid.attack_primary"
HOLSTER_ACTION = "anim.humanoid.holster_primary"
DOWN_ACTION = "anim.humanoid.down"


def require_file(path: pathlib.Path) -> None:
    if not path.is_file():
        raise RuntimeError(f"Required provider input is missing: {path}")


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


def get_single_armature(objects: list[bpy.types.Object], source: pathlib.Path) -> bpy.types.Object:
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"{source} imported {len(armatures)} armatures; expected one")
    return armatures[0]


def rename_action(
    action: bpy.types.Action,
    name: str,
    source_clip: str,
    loop_candidate: bool = True,
) -> bpy.types.Action:
    action.name = name
    action.use_fake_user = True
    action["source_provider"] = "Mixamo"
    action["source_clip"] = source_clip
    action["root_motion"] = "source-in-place"
    action["loop_candidate"] = loop_candidate
    return action


def action_channelbags(action: bpy.types.Action) -> list[object]:
    return [
        channelbag
        for layer in action.layers
        for strip in layer.strips
        for channelbag in strip.channelbags
    ]


def normalize_compatible_donor(
    target_armature: bpy.types.Object,
    donor_armature: bpy.types.Object,
    action: bpy.types.Action,
    source: pathlib.Path,
) -> float:
    """Validate Mixamo's standardized no-skin rest-pose representation."""

    target_bones = {bone.name: bone for bone in target_armature.data.bones}
    donor_bones = {bone.name: bone for bone in donor_armature.data.bones}
    length_mismatches: list[tuple[str, float]] = []
    maximum_rest_delta = 0.0
    for name, target_bone in target_bones.items():
        donor_bone = donor_bones[name]
        length_delta = abs(target_bone.length - donor_bone.length)
        if length_delta > 0.001:
            length_mismatches.append((name, length_delta))
        maximum_rest_delta = max(
            maximum_rest_delta,
            max(
                abs(target_bone.matrix_local[row][column] - donor_bone.matrix_local[row][column])
                for row in range(4)
                for column in range(4)
            ),
        )
    if length_mismatches:
        raise RuntimeError(
            f"Mixamo donor bone lengths differ for {source}: {length_mismatches[:8]}"
        )

    hierarchy_mismatches = [
        name
        for name, target_bone in target_bones.items()
        if (target_bone.parent.name if target_bone.parent else None)
        != (donor_bones[name].parent.name if donor_bones[name].parent else None)
    ]
    if hierarchy_mismatches:
        raise RuntimeError(
            f"Mixamo donor hierarchy differs for {source}: {hierarchy_mismatches[:8]}"
        )

    action["donor_rest_pose_variant"] = "mixamo-no-skin"
    action["donor_maximum_rest_matrix_delta"] = maximum_rest_delta
    return 1.0


def retarget_mixamo_action(
    target_armature: bpy.types.Object,
    donor_armature: bpy.types.Object,
    donor_action: bpy.types.Action,
    end_frame: int | None = None,
) -> bpy.types.Action:
    """Bake Mixamo's local animation deltas onto the accepted skinned rest pose."""

    scene = bpy.context.scene
    donor_armature.animation_data_create()
    donor_armature.animation_data.action = donor_action
    target_armature.animation_data_create()
    previous_target_action = target_armature.animation_data.action
    if previous_target_action is None:
        raise RuntimeError("The accepted Vanguard rig has no reference idle action")
    previous_frame = scene.frame_current
    scene.frame_set(math.floor(previous_target_action.frame_range[0]))
    bpy.context.view_layer.update()
    target_root_pose_origins = {
        bone.name: target_armature.pose.bones[bone.name].matrix.translation.copy()
        for bone in target_armature.data.bones
        if bone.parent is None
    }
    baked_action = bpy.data.actions.new(donor_action.name + "_retargeted")
    baked_action.use_fake_user = True
    target_armature.animation_data.action = baked_action

    ordered_names = sorted(
        (bone.name for bone in target_armature.data.bones),
        key=lambda name: len(target_armature.data.bones[name].parent_recursive),
    )
    start = math.floor(donor_action.frame_range[0])
    end = math.ceil(donor_action.frame_range[1])
    if end_frame is not None:
        if end_frame < start or end_frame > end:
            raise RuntimeError(
                f"Invalid retarget trim {start}-{end_frame} for donor range {start}-{end}"
            )
        end = end_frame
    try:
        for pose_bone in target_armature.pose.bones:
            pose_bone.rotation_mode = "QUATERNION"
        scene.frame_set(start)
        bpy.context.view_layer.update()
        donor_root_pose_origins = {
            name: donor_armature.pose.bones[name].matrix.translation.copy()
            for name in ordered_names
            if donor_armature.data.bones[name].parent is None
        }
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            for name in ordered_names:
                target_pose = target_armature.pose.bones[name]
                donor_pose = donor_armature.pose.bones[name]
                # matrix_basis is the animated local delta from each skeleton's
                # own rest pose. Copying that delta retains the accepted skinned
                # rest pose while avoiding the global-space correction that can
                # rotate a standing donor onto the ground.
                local_delta = donor_pose.matrix_basis.copy()
                if name in donor_root_pose_origins:
                    # Mixamo no-skin FBXs encode the absolute standing hip
                    # height in the root location because their Hips rest bone
                    # sits at the armature origin. Root displacement must be
                    # transferred in armature space; its local axes differ from
                    # the accepted skinned rest representation.
                    local_delta.translation = (0.0, 0.0, 0.0)
                target_pose.matrix_basis = local_delta
                if name in donor_root_pose_origins:
                    root_matrix = target_pose.matrix.copy()
                    root_matrix.translation = (
                        target_root_pose_origins[name]
                        + donor_pose.matrix.translation
                        - donor_root_pose_origins[name]
                    )
                    target_pose.matrix = root_matrix
            bpy.context.view_layer.update()
            for name in ordered_names:
                pose_bone = target_armature.pose.bones[name]
                pose_bone.keyframe_insert("location", frame=frame, group=name)
                pose_bone.keyframe_insert("rotation_quaternion", frame=frame, group=name)
                pose_bone.keyframe_insert("scale", frame=frame, group=name)
    finally:
        scene.frame_set(previous_frame)
        target_armature.animation_data.action = previous_target_action

    baked_action["retarget_method"] = "mixamo-local-basis-bake"
    baked_action["source_frame_start"] = start
    baked_action["source_frame_end"] = end
    return baked_action


def load_animation_action(
    source: pathlib.Path,
    global_scale: float,
    target_armature: bpy.types.Object,
    name: str,
    source_clip: str,
    end_frame: int | None = None,
    loop_candidate: bool = True,
) -> bpy.types.Action:
    objects, actions = import_fbx(source, global_scale)
    temporary_armature = get_single_armature(objects, source)
    if len(actions) != 1:
        raise RuntimeError(f"{source} imported {len(actions)} actions; expected one")

    object_transform_delta = max(
        abs(target_armature.matrix_world[row][column] - temporary_armature.matrix_world[row][column])
        for row in range(4)
        for column in range(4)
    )
    if object_transform_delta > 1e-5:
        raise RuntimeError(
            f"Animation armature-object transform mismatch for {source}: "
            f"maximum delta={object_transform_delta:.6f}"
        )

    target_bones = {bone.name for bone in target_armature.data.bones}
    source_bones = {bone.name for bone in temporary_armature.data.bones}
    if source_bones != target_bones:
        missing = sorted(target_bones - source_bones)
        extra = sorted(source_bones - target_bones)
        raise RuntimeError(
            f"Animation skeleton mismatch for {source}: missing={missing}, extra={extra}"
        )
    mismatched_rest_bones = []
    for bone_name in sorted(target_bones):
        target_matrix = target_armature.data.bones[bone_name].matrix_local
        source_matrix = temporary_armature.data.bones[bone_name].matrix_local
        maximum_delta = max(
            abs(target_matrix[row][column] - source_matrix[row][column])
            for row in range(4)
            for column in range(4)
        )
        if maximum_delta > 1e-4:
            mismatched_rest_bones.append((bone_name, maximum_delta))
    action = actions[0]
    if mismatched_rest_bones:
        normalize_compatible_donor(target_armature, temporary_armature, actions[0], source)
        action = retarget_mixamo_action(
            target_armature,
            temporary_armature,
            actions[0],
            end_frame=end_frame,
        )
    elif end_frame is not None:
        raise RuntimeError(
            f"Trimmed donor {source} unexpectedly matches the accepted rest pose"
        )

    action = rename_action(action, name, source_clip, loop_candidate)
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    if action != actions[0]:
        bpy.data.actions.remove(actions[0])
    bpy.data.orphans_purge(do_recursive=True)
    return action


def limit_skin_influences(mesh: bpy.types.Object) -> int:
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    maximum = 0
    unweighted = 0
    for vertex in mesh.data.vertices:
        weighted = [group for group in vertex.groups if group.weight > 1e-6]
        maximum = max(maximum, len(weighted))
        if not weighted:
            unweighted += 1
    if unweighted:
        raise RuntimeError(f"Vanguard mesh has {unweighted} unweighted vertices")
    if maximum > 4:
        raise RuntimeError(f"Vanguard mesh retains {maximum} influences on a vertex")
    return maximum


def add_socket(
    armature: bpy.types.Object,
    name: str,
    bone: str,
    location: tuple[float, float, float],
    rotation_degrees: tuple[float, float, float],
) -> bpy.types.Object:
    socket = bpy.data.objects.new(name, None)
    socket.empty_display_type = "ARROWS"
    socket.empty_display_size = 0.08
    socket.parent = armature
    socket.parent_type = "BONE"
    socket.parent_bone = bone
    socket.location = location
    socket.rotation_euler = tuple(math.radians(value) for value in rotation_degrees)
    socket["socket_contract"] = name
    bpy.context.scene.collection.objects.link(socket)
    return socket


def load_packed_texture_from_zip(
    archive_path: pathlib.Path,
    semantic: str,
) -> bpy.types.Image:
    with zipfile.ZipFile(archive_path) as archive:
        candidates = [
            name
            for name in archive.namelist()
            if semantic.lower() in pathlib.PurePosixPath(name).name.lower()
        ]
        if len(candidates) != 1:
            raise RuntimeError(
                f"Expected one {semantic} texture in {archive_path}, found {len(candidates)}"
            )
        member = candidates[0]
        with tempfile.TemporaryDirectory() as temporary_directory:
            texture_path = pathlib.Path(temporary_directory) / pathlib.PurePosixPath(member).name
            texture_path.write_bytes(archive.read(member))
            image = bpy.data.images.load(str(texture_path), check_existing=False)
            image.name = texture_path.name
            image.pack()
            return image


def repair_mixamo_materials() -> None:
    def image_semantics(image: bpy.types.Image) -> str:
        return f"{image.name} {image.filepath}".lower()

    for material in bpy.data.materials:
        if not material.use_nodes or material.node_tree is None:
            continue
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        principled = next(
            (node for node in nodes if node.type == "BSDF_PRINCIPLED"),
            None,
        )
        if principled is None:
            continue

        texture_nodes = [node for node in nodes if node.type == "TEX_IMAGE"]
        if not texture_nodes:
            continue

        missing_nodes = [
            node
            for node in nodes
            if node.type == "TEX_IMAGE"
            and (node.image is None or node.image.size[0] == 0 or node.image.size[1] == 0)
        ]
        metallic_images = [
            image
            for image in bpy.data.images
            if image.size[0] > 0 and "metallic" in image_semantics(image)
        ]
        if not metallic_images:
            metallic_images = [
                load_packed_texture_from_zip(TRIPO_TEXTURE_SOURCE, "metallic")
            ]
        if len(metallic_images) != 1:
            raise RuntimeError(
                f"Expected one packed metallic image, found {len(metallic_images)}"
            )
        metallic_nodes = [
            node
            for node in texture_nodes
            if node.type == "TEX_IMAGE"
            and node.image is not None
            and "metallic" in image_semantics(node.image)
            and node.image.size[0] > 0
        ]
        if len(metallic_nodes) > 1:
            raise RuntimeError(
                f"Expected at most one metallic node in {material.name}, found {len(metallic_nodes)}"
            )
        if metallic_nodes:
            metallic_node = metallic_nodes[0]
        else:
            metallic_node = nodes.new("ShaderNodeTexImage")
            metallic_node.name = "Metallic"
            metallic_node.label = "Metallic"
            metallic_node.image = metallic_images[0]
        metallic_node.image.colorspace_settings.name = "Non-Color"
        for link in list(principled.inputs["Specular IOR Level"].links):
            links.remove(link)
        for link in list(principled.inputs["Metallic"].links):
            links.remove(link)
        links.new(metallic_node.outputs["Color"], principled.inputs["Metallic"])
        for node in missing_nodes:
            nodes.remove(node)

        for node in nodes:
            if node.type == "TEX_IMAGE" and node.image is not None:
                semantics = image_semantics(node.image)
                if "normal" in semantics or "rough" in semantics:
                    node.image.colorspace_settings.name = "Non-Color"


def evaluated_mesh_world_bounds(
    mesh: bpy.types.Object,
) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(dependency_graph)
    temporary_mesh = evaluated.to_mesh()
    try:
        world = evaluated.matrix_world
        points = [world @ vertex.co for vertex in temporary_mesh.vertices]
        minimum = tuple(min(point[axis] for point in points) for axis in range(3))
        maximum = tuple(max(point[axis] for point in points) for axis in range(3))
        return minimum, maximum
    finally:
        evaluated.to_mesh_clear()


def evaluated_mesh_bounds(mesh: bpy.types.Object) -> tuple[float, float]:
    minimum, maximum = evaluated_mesh_world_bounds(mesh)
    return minimum[2], maximum[2]


def measure_fbx_global_scale(source: pathlib.Path) -> float:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects, _ = import_fbx(source, global_scale=1.0)
    armature = get_single_armature(objects, source)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(
            f"Scale preflight imported {len(meshes)} meshes from {source}; expected one"
        )
    if armature.animation_data is not None:
        armature.animation_data.action = None
    bpy.context.view_layer.update()
    source_minimum, source_maximum = evaluated_mesh_bounds(meshes[0])
    source_height = source_maximum - source_minimum
    if source_height <= 1e-6:
        raise RuntimeError(f"Scale preflight found invalid source height {source_height}")
    return TARGET_HEIGHT_METERS / source_height


def ground_imported_objects(
    imported_objects: list[bpy.types.Object],
    mesh: bpy.types.Object,
) -> tuple[float, float, float, float]:
    minimum, maximum = evaluated_mesh_world_bounds(mesh)
    center_x = (minimum[0] + maximum[0]) * 0.5
    center_y = (minimum[1] + maximum[1]) * 0.5
    imported_set = set(imported_objects)
    roots = [obj for obj in imported_objects if obj.parent not in imported_set]
    if not roots:
        raise RuntimeError("Vanguard import has no transform root to ground")
    for root in roots:
        root.location.x -= center_x
        root.location.y -= center_y
        root.location.z -= minimum[2]
    bpy.context.view_layer.update()

    adjusted_minimum, adjusted_maximum = evaluated_mesh_world_bounds(mesh)
    adjusted_center_x = (adjusted_minimum[0] + adjusted_maximum[0]) * 0.5
    adjusted_center_y = (adjusted_minimum[1] + adjusted_maximum[1]) * 0.5
    if abs(adjusted_minimum[2]) > 0.002:
        raise RuntimeError(
            f"Vanguard grounding left a {adjusted_minimum[2]:.5f} m boot offset"
        )
    if max(abs(adjusted_center_x), abs(adjusted_center_y)) > 0.002:
        raise RuntimeError(
            "Vanguard planar normalization left the body away from the origin: "
            f"x={adjusted_center_x:.5f}, y={adjusted_center_y:.5f}"
        )
    return (
        adjusted_minimum[2],
        adjusted_maximum[2],
        adjusted_center_x,
        adjusted_center_y,
    )


def validate_exported_glb(path: pathlib.Path) -> dict[str, object]:
    """Reimport the published file and reject transform regressions at the boundary."""

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    bpy.ops.import_scene.gltf(filepath=str(path))
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(
            f"Published Vanguard GLB reimported {len(armatures)} armatures; expected one"
        )
    idle_actions = [action for action in bpy.data.actions if action.name == IDLE_ACTION]
    if len(idle_actions) != 1:
        raise RuntimeError(
            f"Published Vanguard GLB reimported {len(idle_actions)} idle actions; expected one"
        )
    armatures[0].animation_data_create()
    armatures[0].animation_data.action = idle_actions[0]
    bpy.context.scene.frame_set(math.floor(idle_actions[0].frame_range[0]))
    bpy.context.view_layer.update()
    meshes = [obj for obj in bpy.context.scene.objects if obj.name == "VanguardBody"]
    if len(meshes) != 1:
        raise RuntimeError(
            f"Published Vanguard GLB reimported {len(meshes)} body meshes; expected one"
        )
    minimum, maximum = evaluated_mesh_world_bounds(meshes[0])
    center = tuple((minimum[axis] + maximum[axis]) * 0.5 for axis in range(3))
    height = maximum[2] - minimum[2]
    if abs(minimum[2]) > 0.01:
        raise RuntimeError(
            f"Published Vanguard GLB has a {minimum[2]:.5f} m boot offset"
        )
    if max(abs(center[0]), abs(center[1])) > 0.10:
        raise RuntimeError(
            "Published Vanguard GLB is displaced from its gameplay origin: "
            f"center=({center[0]:.5f}, {center[1]:.5f})"
        )
    if not MINIMUM_HEIGHT_METERS <= height <= MAXIMUM_HEIGHT_METERS:
        raise RuntimeError(
            f"Published Vanguard GLB reimported at invalid height {height:.5f} m"
        )
    walk_actions = [action for action in bpy.data.actions if action.name == WALK_ACTION]
    if len(walk_actions) != 1:
        raise RuntimeError(
            f"Published Vanguard GLB reimported {len(walk_actions)} walk actions; expected one"
        )
    walk_world = validate_in_place(armatures[0], walk_actions[0])
    actions_by_name = {action.name: action for action in bpy.data.actions}
    required_actions = {
        IDLE_ACTION,
        LOCOMOTION_ACTION,
        WALK_ACTION,
        DRAW_ACTION,
        ARMED_IDLE_ACTION,
        ARMED_LOCOMOTION_ACTION,
        FIRE_ACTION,
        HOLSTER_ACTION,
        DOWN_ACTION,
    }
    if set(actions_by_name) != required_actions:
        raise RuntimeError(
            "Published Vanguard GLB action mismatch: "
            f"expected={sorted(required_actions)}, actual={sorted(actions_by_name)}"
        )
    standing_action_hips = {
        name: validate_standing_action(armatures[0], actions_by_name[name])
        for name in (
            DRAW_ACTION,
            ARMED_IDLE_ACTION,
            ARMED_LOCOMOTION_ACTION,
            FIRE_ACTION,
            HOLSTER_ACTION,
        )
    }
    down_action_hips = validate_down_action(armatures[0], actions_by_name[DOWN_ACTION])
    return {
        "minimum": [round(value, 5) for value in minimum],
        "maximum": [round(value, 5) for value in maximum],
        "center": [round(value, 5) for value in center],
        "height": round(height, 5),
        "walk_world": walk_world,
        "standing_action_hips": standing_action_hips,
        "down_action_hips": down_action_hips,
    }


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.fps = 30
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene.unit_settings.scale_length = 1.0
    scene.frame_start = 1
    scene.frame_end = 58


def validate_in_place(
    armature: bpy.types.Object,
    action: bpy.types.Action,
) -> dict[str, object]:
    """Validate the evaluated world-space cycle, never raw pose-bone curves."""

    scene = bpy.context.scene
    previous_frame = scene.frame_current
    armature.animation_data_create()
    previous_action = armature.animation_data.action
    armature.animation_data.action = action
    start = math.floor(action.frame_range[0])
    end = math.ceil(action.frame_range[1])
    samples: dict[str, list[tuple[float, float, float]]] = {
        "hips": [],
        "left_foot": [],
        "right_foot": [],
    }
    try:
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            for sample_name, bone_name in (
                ("hips", "mixamorig:Hips"),
                ("left_foot", "mixamorig:LeftFoot"),
                ("right_foot", "mixamorig:RightFoot"),
            ):
                translation = (
                    armature.matrix_world @ armature.pose.bones[bone_name].matrix
                ).translation
                samples[sample_name].append(tuple(translation))
    finally:
        armature.animation_data.action = previous_action
        scene.frame_set(previous_frame)

    hips = samples["hips"]
    planar_ranges = [
        max(sample[axis] for sample in hips) - min(sample[axis] for sample in hips)
        for axis in (0, 1)
    ]
    endpoint_delta = math.hypot(
        hips[-1][0] - hips[0][0],
        hips[-1][1] - hips[0][1],
    )
    vertical_range = max(sample[2] for sample in hips) - min(sample[2] for sample in hips)
    foot_lift_ranges = {
        sample_name: max(sample[2] for sample in samples[sample_name])
        - min(sample[2] for sample in samples[sample_name])
        for sample_name in ("left_foot", "right_foot")
    }
    if max(planar_ranges) > 0.15 or endpoint_delta > 0.01:
        raise RuntimeError(
            "Mixamo walk is not in place in evaluated world space: "
            f"planar_ranges={planar_ranges}, endpoint_delta={endpoint_delta}"
        )
    if vertical_range > 0.15:
        raise RuntimeError(
            f"Mixamo walk has excessive evaluated hip lift: {vertical_range:.5f} m"
        )
    if min(foot_lift_ranges.values()) < 0.04:
        raise RuntimeError(
            f"Mixamo walk does not visibly alternate both feet: {foot_lift_ranges}"
        )
    return {
        "planar_ranges": [round(value, 5) for value in planar_ranges],
        "endpoint_delta": round(endpoint_delta, 5),
        "vertical_range": round(vertical_range, 5),
        "foot_lift_ranges": {
            key: round(value, 5) for key, value in foot_lift_ranges.items()
        },
    }


def evaluated_hip_heights(
    armature: bpy.types.Object,
    action: bpy.types.Action,
) -> list[float]:
    scene = bpy.context.scene
    previous_frame = scene.frame_current
    armature.animation_data_create()
    previous_action = armature.animation_data.action
    armature.animation_data.action = action
    start = math.floor(action.frame_range[0])
    end = math.ceil(action.frame_range[1])
    heights: list[float] = []
    try:
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            hips = armature.matrix_world @ armature.pose.bones["mixamorig:Hips"].matrix
            heights.append(hips.translation.z)
    finally:
        armature.animation_data.action = previous_action
        scene.frame_set(previous_frame)
    return heights


def validate_standing_action(
    armature: bpy.types.Object,
    action: bpy.types.Action,
) -> dict[str, float]:
    heights = evaluated_hip_heights(armature, action)
    minimum = min(heights)
    maximum = max(heights)
    if minimum < 0.75:
        raise RuntimeError(
            f"Standing action {action.name} drops the hips to {minimum:.5f} m"
        )
    return {"minimum": round(minimum, 5), "maximum": round(maximum, 5)}


def validate_down_action(
    armature: bpy.types.Object,
    action: bpy.types.Action,
) -> dict[str, float]:
    heights = evaluated_hip_heights(armature, action)
    if heights[0] < 0.75 or heights[-1] > 0.45:
        raise RuntimeError(
            f"Down action {action.name} does not move from standing to ground: "
            f"first={heights[0]:.5f}, last={heights[-1]:.5f}"
        )
    return {
        "first": round(heights[0], 5),
        "last": round(heights[-1], 5),
        "minimum": round(min(heights), 5),
        "maximum": round(max(heights), 5),
    }


def main() -> None:
    for path in (
        RIGGED_SOURCE,
        WALK_SOURCE,
        DRAW_SOURCE,
        ARMED_IDLE_SOURCE,
        ARMED_WALK_SOURCE,
        FIRE_SOURCE,
        HOLSTER_SOURCE,
        DOWN_SOURCE,
        TRIPO_TEXTURE_SOURCE,
    ):
        require_file(path)
    BLEND_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    GLB_OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    fbx_global_scale = measure_fbx_global_scale(RIGGED_SOURCE)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    configure_scene()

    base_objects, base_actions = import_fbx(RIGGED_SOURCE, fbx_global_scale)
    armature = get_single_armature(base_objects, RIGGED_SOURCE)
    meshes = [obj for obj in base_objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Rigged Vanguard imported {len(meshes)} meshes; expected one")
    if len(base_actions) != 1:
        raise RuntimeError(f"Rigged Vanguard imported {len(base_actions)} actions; expected one")

    mesh = meshes[0]
    armature.name = "VanguardRig"
    armature.data.name = "VanguardSkeleton"
    mesh.name = "VanguardBody"
    mesh.data.name = "VanguardBodyMesh"

    bone_names = {bone.name for bone in armature.data.bones}
    missing_bones = sorted(set(EXPECTED_BONES) - bone_names)
    if missing_bones:
        raise RuntimeError(f"Vanguard rig is missing required bones: {missing_bones}")
    if len(bone_names) > 64:
        raise RuntimeError(f"Vanguard rig has {len(bone_names)} bones; maximum is 64")

    maximum_influences = limit_skin_influences(mesh)
    repair_mixamo_materials()
    idle = rename_action(
        base_actions[0],
        IDLE_ACTION,
        "Unarmed Idle",
    )
    walk = load_animation_action(
        WALK_SOURCE,
        fbx_global_scale,
        armature,
        WALK_ACTION,
        "Standard Walk (In Place)",
    )
    walk_world_validation = validate_in_place(armature, walk)
    locomotion = rename_action(
        walk.copy(),
        LOCOMOTION_ACTION,
        "Standard Walk (In Place)",
    )
    combat_actions = [
        load_animation_action(
            DRAW_SOURCE,
            fbx_global_scale,
            armature,
            DRAW_ACTION,
            "Grab Rifle From Back",
            loop_candidate=False,
        ),
        load_animation_action(ARMED_IDLE_SOURCE, fbx_global_scale, armature, ARMED_IDLE_ACTION, "Rifle Aiming Idle"),
        load_animation_action(ARMED_WALK_SOURCE, fbx_global_scale, armature, ARMED_LOCOMOTION_ACTION, "Rifle Walk (In Place)"),
        load_animation_action(
            FIRE_SOURCE,
            fbx_global_scale,
            armature,
            FIRE_ACTION,
            "Firing Rifle",
            loop_candidate=False,
        ),
        load_animation_action(
            HOLSTER_SOURCE,
            fbx_global_scale,
            armature,
            HOLSTER_ACTION,
            "Put Back Rifle",
            loop_candidate=False,
        ),
        load_animation_action(
            DOWN_SOURCE,
            fbx_global_scale,
            armature,
            DOWN_ACTION,
            "Rifle Death",
            loop_candidate=False,
        ),
    ]
    armature.animation_data_create()
    armature.animation_data.action = idle
    bpy.context.scene.frame_set(1)

    (
        ground_minimum,
        standing_maximum,
        planar_center_x,
        planar_center_y,
    ) = ground_imported_objects(base_objects, mesh)
    sockets = [
        add_socket(
            armature,
            "socket.weapon.hand_primary",
            "mixamorig:RightHand",
            (0.0, 0.0, 0.0),
            (0.0, 0.0, 0.0),
        ),
        add_socket(
            armature,
            "socket.weapon.holster_primary",
            "mixamorig:Spine2",
            (0.18, 0.10, 0.02),
            (8.0, -12.0, 92.0),
        ),
    ]

    if not MINIMUM_HEIGHT_METERS <= standing_maximum - ground_minimum <= MAXIMUM_HEIGHT_METERS:
        raise RuntimeError(
            "Vanguard evaluated height is outside the accepted animated envelope: "
            f"{standing_maximum - ground_minimum:.4f} m"
        )

    for index, material in enumerate(bpy.data.materials, start=1):
        material.name = "Vanguard" if len(bpy.data.materials) == 1 else f"Vanguard.{index:02d}"
    mesh["asset_id"] = "character.crew.vanguard.v1"
    mesh["production_run"] = "prod-tripo-v31bq-20260803-02"
    armature["rig_provider"] = "Mixamo"
    armature["source_pose"] = "T-pose"

    report = {
        "asset_id": "character.crew.vanguard.v1",
        "vertices": len(mesh.data.vertices),
        "polygons": len(mesh.data.polygons),
        "triangles": sum(len(polygon.vertices) - 2 for polygon in mesh.data.polygons),
        "materials": len(mesh.material_slots),
        "bones": len(bone_names),
        "maximum_skin_influences": maximum_influences,
        "fbx_global_scale": round(fbx_global_scale, 6),
        "ground_minimum_meters": round(ground_minimum, 5),
        "planar_center_meters": [round(planar_center_x, 5), round(planar_center_y, 5)],
        "evaluated_height_meters": round(standing_maximum - ground_minimum, 5),
        "walk_world_validation": walk_world_validation,
        "actions": {
            action.name: [round(value, 3) for value in action.frame_range]
            for action in (idle, locomotion, walk, *combat_actions)
        },
        "blend_output": str(BLEND_OUTPUT),
        "glb_output": str(GLB_OUTPUT),
    }

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUTPUT), compress=True)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in (armature, mesh, *sockets):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_OUTPUT),
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

    exported_glb = validate_exported_glb(GLB_OUTPUT)
    report["exported_glb"] = exported_glb
    print("VANGUARD_BUILD=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
