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


def require_file(path: pathlib.Path) -> None:
    if not path.is_file():
        raise RuntimeError(f"Required provider input is missing: {path}")


def import_fbx(
    path: pathlib.Path,
    global_scale: float,
) -> tuple[list[bpy.types.Object], list[bpy.types.Action]]:
    existing_objects = set(bpy.data.objects)
    existing_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(
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
) -> bpy.types.Action:
    action.name = name
    action.use_fake_user = True
    action["source_provider"] = "Mixamo"
    action["source_clip"] = source_clip
    action["root_motion"] = "source-in-place"
    action["loop_candidate"] = True
    return action


def load_animation_action(
    source: pathlib.Path,
    global_scale: float,
    target_armature: bpy.types.Object,
    name: str,
    source_clip: str,
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
    if mismatched_rest_bones:
        raise RuntimeError(
            f"Animation rest-pose mismatch for {source}: {mismatched_rest_bones[:8]}"
        )

    action = rename_action(actions[0], name, source_clip)
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
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
    if not 1.72 <= height <= 1.86:
        raise RuntimeError(
            f"Published Vanguard GLB reimported at invalid height {height:.5f} m"
        )
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(
            f"Published Vanguard GLB reimported {len(armatures)} armatures; expected one"
        )
    walk_actions = [action for action in bpy.data.actions if action.name == WALK_ACTION]
    if len(walk_actions) != 1:
        raise RuntimeError(
            f"Published Vanguard GLB reimported {len(walk_actions)} walk actions; expected one"
        )
    walk_world = validate_in_place(armatures[0], walk_actions[0])
    return {
        "minimum": [round(value, 5) for value in minimum],
        "maximum": [round(value, 5) for value in maximum],
        "center": [round(value, 5) for value in center],
        "height": round(height, 5),
        "walk_world": walk_world,
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


def main() -> None:
    for path in (RIGGED_SOURCE, WALK_SOURCE):
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
            "mixamorig:Hips",
            (0.24, 0.08, -0.05),
            (8.0, -12.0, 92.0),
        ),
    ]

    if not 1.72 <= standing_maximum - ground_minimum <= 1.86:
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
            for action in (idle, locomotion, walk)
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
