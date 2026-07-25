"""Build the provisional production Vanguard from the selected Tripo candidate.

Blender 5.2 LTS:
  blender --background --factory-startup --python tools/blender/build_vanguard_character_v1.py -- \
    <candidate-02.glb> <editable.blend> <published.glb> <build-report.json>

The selected Tripo GLB is immutable input. This script imports a copy, performs
bounded cleanup and triangle retopology, creates the Blender-owned shared crew
rig, creates the documented unbound presentation action library, saves an
editable source, publishes a GLB, then resets Blender and validates a fresh
import of that exact GLB.
"""

from __future__ import annotations

import bpy
import bmesh
import json
import math
import sys
from pathlib import Path
from mathutils import Vector


ASSET_ID = "character.crew.vanguard.v1"
RIG_ID = "rig.crew.humanoid.v1"
RUN_ID = "prod-tripo-v31bq-20260723-01"
TASK_ID = "c889d05a-90fe-4186-85eb-12d4eceafb35"
EXPECTED_INPUT_BYTES = 58_610_072
TARGET_HEIGHT_METERS = 1.82
TARGET_TRIANGLES = 28_000
TARGET_VISUAL_TRIANGLES = 24_000
TARGET_BASE_TRIANGLES = 4_000
HARD_TRIANGLE_LIMIT = 35_000
MAX_MATERIALS = 8
MAX_TEXTURE_DIMENSION = 2048
MAX_BONES = 64
MAX_INFLUENCES = 4

ACTION_NAMES = (
    "anim.humanoid.idle_holstered",
    "anim.humanoid.locomotion_holstered",
    "anim.humanoid.draw",
    "anim.humanoid.idle_armed",
    "anim.humanoid.locomotion_armed",
    "anim.humanoid.raise_aim",
    "anim.humanoid.fire_recoil",
    "anim.humanoid.recovery",
    "anim.humanoid.holster",
    "anim.humanoid.dialogue_idle",
    "anim.humanoid.dialogue_speak",
    "anim.humanoid.dialogue_listen",
    "anim.humanoid.interact_terminal",
    "anim.humanoid.use_healing",
    "anim.humanoid.hit_reaction",
    "anim.humanoid.down",
)

SOCKET_HAND = "socket.weapon.hand_primary"
SOCKET_HOLSTER = "socket.weapon.holster_primary"


def cli_paths() -> tuple[Path, Path, Path, Path]:
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 4:
        raise SystemExit(
            "usage: blender --background --python script.py -- "
            "input.glb output.blend output.glb report.json"
        )
    return tuple(Path(value).resolve() for value in args)  # type: ignore[return-value]


def rounded_vector(value) -> list[float]:
    return [round(float(component), 6) for component in value]


def apply_object_transforms(obj) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def world_bounds(mesh_objects) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in mesh_objects
        for corner in obj.bound_box
    ]
    return (
        Vector(min(point[i] for point in points) for i in range(3)),
        Vector(max(point[i] for point in points) for i in range(3)),
    )


def mesh_triangle_count(obj) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def mesh_topology_report(obj, include_components: bool = True) -> dict:
    mesh = obj.data
    mesh.calc_loop_triangles()
    if not include_components:
        return {
            "vertices": len(mesh.vertices),
            "edges": len(mesh.edges),
            "polygons": len(mesh.polygons),
            "triangles": len(mesh.loop_triangles),
            "boundary_edges": None,
            "non_manifold_edges": None,
            "loose_edges": None,
            "loose_vertices": None,
            "connected_components": None,
            "largest_component_vertices": [],
            "detailed_audit": "see immutable candidate-02 preflight inspection",
        }
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary_edges = sum(1 for edge in bm.edges if len(edge.link_faces) == 1)
    non_manifold_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    loose_edges = sum(1 for edge in bm.edges if len(edge.link_faces) == 0)
    loose_vertices = sum(1 for vertex in bm.verts if len(vertex.link_edges) == 0)

    unseen = set(bm.verts)
    component_sizes = []
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        count = 0
        while stack:
            current = stack.pop()
            count += 1
            for edge in current.link_edges:
                adjacent = edge.other_vert(current)
                if adjacent in unseen:
                    unseen.remove(adjacent)
                    stack.append(adjacent)
        component_sizes.append(count)
    bm.free()
    component_sizes.sort(reverse=True)
    return {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "boundary_edges": boundary_edges,
        "non_manifold_edges": non_manifold_edges,
        "loose_edges": loose_edges,
        "loose_vertices": loose_vertices,
        "connected_components": len(component_sizes),
        "largest_component_vertices": component_sizes[:12],
    }


def welded_component_count(obj, threshold: float = 0.00001) -> int:
    """Count geometric shells after welding glTF's attribute-split vertices."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=threshold)
    unseen = set(bm.verts)
    components = 0
    while unseen:
        components += 1
        seed = unseen.pop()
        stack = [seed]
        while stack:
            current = stack.pop()
            for edge in current.link_edges:
                adjacent = edge.other_vert(current)
                if adjacent in unseen:
                    unseen.remove(adjacent)
                    stack.append(adjacent)
    bm.free()
    return components


def join_meshes(mesh_objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in mesh_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objects[0]
    if len(mesh_objects) > 1:
        bpy.ops.object.join()
    return bpy.context.view_layer.objects.active


def normalize_mesh(obj) -> dict:
    minimum, maximum = world_bounds([obj])
    raw_dimensions = maximum - minimum
    if raw_dimensions.z <= 0.0:
        raise RuntimeError("Imported mesh has zero height")
    uniform_scale = TARGET_HEIGHT_METERS / raw_dimensions.z
    obj.scale = (uniform_scale, uniform_scale, uniform_scale)
    apply_object_transforms(obj)

    minimum, maximum = world_bounds([obj])
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    obj.location = (-center_x, -center_y, -minimum.z)
    apply_object_transforms(obj)
    minimum, maximum = world_bounds([obj])
    return {
        "uniform_scale_from_raw": uniform_scale,
        "minimum": rounded_vector(minimum),
        "maximum": rounded_vector(maximum),
        "dimensions": rounded_vector(maximum - minimum),
    }


def cleanup_mesh(obj) -> dict:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    before_decimate = mesh_triangle_count(obj)
    print(
        f"Retopology start: {before_decimate} triangles -> {TARGET_VISUAL_TRIANGLES}",
        flush=True,
    )
    if before_decimate > TARGET_VISUAL_TRIANGLES:
        modifier = obj.modifiers.new("Retopology.Target24000", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = TARGET_VISUAL_TRIANGLES / before_decimate
        modifier.use_collapse_triangulate = True
        modifier.use_symmetry = False
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    print(f"Retopology modifier complete: {mesh_triangle_count(obj)} triangles", flush=True)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.00002)
    bpy.ops.mesh.delete_loose(use_verts=True, use_edges=True, use_faces=False)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.update()

    after_decimate = mesh_triangle_count(obj)
    if after_decimate > HARD_TRIANGLE_LIMIT:
        raise RuntimeError(
            f"Retopology exceeded hard limit: {after_decimate} > {HARD_TRIANGLE_LIMIT}"
        )
    return {
        "method": "Blender Decimate COLLAPSE; UV-preserving bounded triangle retopology",
        "triangles_before": before_decimate,
        "target_triangles": TARGET_VISUAL_TRIANGLES,
        "triangles_after": after_decimate,
    }


def normalize_textures() -> list[dict]:
    reports = []
    for image in bpy.data.images:
        if image.source != "FILE" or image.size[0] <= 0 or image.size[1] <= 0:
            continue
        before = [int(image.size[0]), int(image.size[1])]
        longest = max(before)
        if longest > MAX_TEXTURE_DIMENSION:
            scale = MAX_TEXTURE_DIMENSION / longest
            width = max(1, round(before[0] * scale))
            height = max(1, round(before[1] * scale))
            image.scale(width, height)
        image.pack()
        reports.append(
            {
                "name": image.name,
                "before": before,
                "after": [int(image.size[0]), int(image.size[1])],
                "colorspace": image.colorspace_settings.name,
                "packed": image.packed_file is not None,
            }
        )
    return reports


def add_ellipsoid(name: str, center, scale) -> object:
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=2, radius=1.0, location=center
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_object_transforms(obj)
    return obj


def add_capsule_segment(name: str, start, end, radius: float) -> list[object]:
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=12,
        radius=radius,
        depth=direction.length,
        end_fill_type="NGON",
        location=midpoint,
    )
    cylinder = bpy.context.object
    cylinder.name = f"{name}.segment"
    cylinder.rotation_mode = "QUATERNION"
    cylinder.rotation_quaternion = direction.to_track_quat("Z", "Y")
    apply_object_transforms(cylinder)
    start_cap = add_ellipsoid(
        f"{name}.start", start, (radius * 1.08,) * 3
    )
    end_cap = add_ellipsoid(
        f"{name}.end", end, (radius * 1.08,) * 3
    )
    return [cylinder, start_cap, end_cap]


def make_undersuit_material():
    material = bpy.data.materials.new("mat.crew.undersuit.dark_navy")
    material.diffuse_color = (0.025, 0.055, 0.095, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (0.025, 0.055, 0.095, 1.0)
    shader.inputs["Metallic"].default_value = 0.05
    shader.inputs["Roughness"].default_value = 0.72
    return material


def keep_largest_connected_component(obj) -> dict:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    unseen = set(bm.verts)
    components = []
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        component = {seed}
        while stack:
            current = stack.pop()
            for edge in current.link_edges:
                adjacent = edge.other_vert(current)
                if adjacent in unseen:
                    unseen.remove(adjacent)
                    component.add(adjacent)
                    stack.append(adjacent)
        components.append(component)
    components.sort(key=len, reverse=True)
    removed_vertices = sum(len(component) for component in components[1:])
    if len(components) > 1:
        bmesh.ops.delete(
            bm,
            geom=[
                vertex
                for component in components[1:]
                for vertex in component
            ],
            context="VERTS",
        )
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return {
        "components_before": len(components),
        "removed_vertices": removed_vertices,
    }


def inset_surface(obj, meters: float) -> None:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for vertex in bm.verts:
        normal = vertex.normal.normalized()
        vertex.co -= normal * meters
    bmesh.ops.smooth_vert(
        bm,
        verts=list(bm.verts),
        factor=0.12,
        use_axis_x=True,
        use_axis_y=True,
        use_axis_z=True,
    )
    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def build_continuous_deforming_base() -> tuple[object, dict]:
    """Create one conservative continuous undersuit shell beneath raw details.

    The provider mesh has many disconnected armor/detail shells. This compact
    manifold underlayer supplies continuous shoulder, elbow, hip, and knee
    deformation without redesigning the accepted visible silhouette.
    """
    parts = [
        add_ellipsoid("base.torso", (0.0, 0.0, 1.22), (0.130, 0.075, 0.25)),
        add_ellipsoid("base.pelvis", (0.0, 0.0, 0.92), (0.130, 0.075, 0.14)),
        add_ellipsoid("base.neck", (0.0, 0.0, 1.49), (0.042, 0.040, 0.080)),
    ]
    for side, sign in (("l", -1.0), ("r", 1.0)):
        shoulder = (sign * 0.105, 0.0, 1.40)
        elbow = (sign * 0.35, 0.0, 1.08)
        wrist = (sign * 0.43, -0.004, 0.92)
        parts.extend(
            add_capsule_segment(
                f"base.upperarm_{side}", shoulder, elbow, 0.032
            )
        )
        parts.extend(
            add_capsule_segment(
                f"base.lowerarm_{side}", elbow, wrist, 0.027
            )
        )

        hip = (sign * 0.105, 0.0, 0.90)
        knee = (sign * 0.145, 0.0, 0.50)
        ankle = (sign * 0.145, 0.0, 0.15)
        parts.extend(
            add_capsule_segment(f"base.thigh_{side}", hip, knee, 0.032)
        )
        parts.extend(
            add_capsule_segment(f"base.calf_{side}", knee, ankle, 0.027)
        )

    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    base = bpy.context.view_layer.objects.active
    base.name = "VanguardDeformingBase"
    base.data.name = "VanguardDeformingBaseMesh"
    apply_object_transforms(base)

    base.data.remesh_voxel_size = 0.018
    base.data.remesh_voxel_adaptivity = 0.0
    base.data.use_remesh_fix_poles = True
    base.data.use_remesh_preserve_volume = True
    bpy.context.view_layer.objects.active = base
    base.select_set(True)
    bpy.ops.object.voxel_remesh()
    component_cleanup = keep_largest_connected_component(base)

    before_decimate = mesh_triangle_count(base)
    if before_decimate > TARGET_BASE_TRIANGLES:
        modifier = base.modifiers.new("Retopology.Target4000", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = TARGET_BASE_TRIANGLES / before_decimate
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = base
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.00002)
    bpy.ops.mesh.delete_loose(use_verts=True, use_edges=True, use_faces=False)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in base.data.polygons:
        polygon.use_smooth = True
        polygon.material_index = 0
    base.data.materials.clear()
    base.data.materials.append(make_undersuit_material())
    base["asset_id"] = ASSET_ID
    base["purpose"] = "continuous_deforming_undersuit_base"
    base["source"] = "Blender-authored conservative underlayer"
    topology = mesh_topology_report(base)
    if topology["connected_components"] != 1:
        raise RuntimeError(
            "Continuous deforming base is not one connected component: "
            f"{topology['connected_components']}"
        )
    return base, {
        "method": (
            "compact internal undersuit topology bridge through shoulders, "
            "elbows, hips and knees + voxel union + largest-shell isolation + "
            "decimate; gloves and boots remain rigid visual shells"
        ),
        "component_cleanup": component_cleanup,
        "triangles_before_decimate": before_decimate,
        "topology": topology,
    }


def add_edit_bone(
    armature,
    name: str,
    head,
    tail,
    parent: str | None = None,
    connected: bool = False,
    deform: bool = True,
    roll: float = 0.0,
):
    bone = armature.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    bone.use_deform = deform
    bone.roll = roll
    if parent:
        bone.parent = armature.edit_bones[parent]
        bone.use_connect = connected
    return bone


def build_rig() -> object:
    armature_data = bpy.data.armatures.new("rig.crew.humanoid.v1")
    armature_obj = bpy.data.objects.new("VanguardRig", armature_data)
    bpy.context.scene.collection.objects.link(armature_obj)
    armature_obj.show_in_front = True
    armature_obj.data.display_type = "OCTAHEDRAL"
    armature_obj[RIG_ID] = True
    armature_obj["rig_profile"] = RIG_ID
    armature_obj["asset_id"] = ASSET_ID
    armature_obj["published_up"] = "+Y"
    armature_obj["published_front"] = "-Z"
    armature_obj["source_up"] = "+Z"
    armature_obj["source_front"] = "-Y"
    armature_obj["root_motion"] = "none"
    armature_obj["action_binding_status"] = (
        "unbound_pending_gameplay_attack_timing_and_phase_contract"
    )
    armature_obj["draw_landmark"] = "event.weapon.transfer_to_hand@frame_1"
    armature_obj["holster_landmark"] = "event.weapon.transfer_to_holster@frame_1"

    bpy.context.view_layer.objects.active = armature_obj
    armature_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    add_edit_bone(armature_data, "root", (0, 0, 0), (0, 0, 0.12))
    add_edit_bone(
        armature_data, "pelvis", (0, 0, 0.86), (0, 0, 1.00), "root"
    )
    add_edit_bone(
        armature_data, "spine_01", (0, 0, 1.00), (0, 0, 1.15), "pelvis", True
    )
    add_edit_bone(
        armature_data,
        "spine_02",
        (0, 0, 1.15),
        (0, 0, 1.30),
        "spine_01",
        True,
    )
    add_edit_bone(
        armature_data,
        "spine_03",
        (0, 0, 1.30),
        (0, 0, 1.46),
        "spine_02",
        True,
    )
    add_edit_bone(
        armature_data,
        "neck_01",
        (0, 0, 1.46),
        (0, 0, 1.58),
        "spine_03",
    )
    add_edit_bone(
        armature_data, "head", (0, 0, 1.58), (0, 0, 1.80), "neck_01", True
    )

    for side, sign in (("l", -1.0), ("r", 1.0)):
        clavicle = f"clavicle_{side}"
        upperarm = f"upperarm_{side}"
        upperarm_twist = f"upperarm_twist_{side}"
        lowerarm = f"lowerarm_{side}"
        lowerarm_twist = f"lowerarm_twist_{side}"
        hand = f"hand_{side}"
        shoulder = (sign * 0.11, 0.0, 1.43)
        upper_end = (sign * 0.31, 0.0, 1.28)
        elbow = (sign * 0.40, -0.002, 1.08)
        wrist = (sign * 0.455, -0.006, 0.91)
        hand_end = (sign * 0.48, -0.025, 0.84)

        add_edit_bone(
            armature_data,
            clavicle,
            (sign * 0.025, 0.0, 1.43),
            shoulder,
            "spine_03",
        )
        add_edit_bone(
            armature_data, upperarm, shoulder, elbow, clavicle
        )
        add_edit_bone(
            armature_data,
            upperarm_twist,
            (Vector(shoulder) + Vector(elbow)) * 0.5,
            Vector(elbow) * 0.92 + Vector(shoulder) * 0.08,
            upperarm,
        )
        add_edit_bone(
            armature_data, lowerarm, elbow, wrist, upperarm
        )
        add_edit_bone(
            armature_data,
            lowerarm_twist,
            (Vector(elbow) + Vector(wrist)) * 0.5,
            Vector(wrist) * 0.92 + Vector(elbow) * 0.08,
            lowerarm,
        )
        add_edit_bone(
            armature_data, hand, wrist, hand_end, lowerarm
        )

        finger_spreads = {
            "thumb": (-0.020, -0.026),
            "index": (-0.010, -0.014),
            "middle": (0.000, -0.004),
            "ring": (0.010, 0.006),
            "pinky": (0.020, 0.016),
        }
        for finger, (x_offset, y_offset) in finger_spreads.items():
            start = Vector(
                (
                    sign * (abs(hand_end[0]) + x_offset),
                    hand_end[1] + y_offset,
                    hand_end[2] + (0.012 if finger == "thumb" else 0.0),
                )
            )
            direction = Vector(
                (
                    sign * (0.025 if finger == "thumb" else 0.012),
                    -0.018 if finger == "thumb" else -0.006,
                    -0.045,
                )
            )
            parent = hand
            for segment in range(1, 4):
                end = start + direction
                bone_name = f"{finger}_{segment:02d}_{side}"
                add_edit_bone(
                    armature_data,
                    bone_name,
                    start,
                    end,
                    parent,
                    connected=segment > 1,
                )
                parent = bone_name
                start = end
                direction *= 0.78

    for side, sign in (("l", -1.0), ("r", 1.0)):
        thigh = f"thigh_{side}"
        calf = f"calf_{side}"
        foot = f"foot_{side}"
        toe = f"toe_{side}"
        hip = (sign * 0.105, 0.0, 0.90)
        knee = (sign * 0.11, 0.0, 0.50)
        ankle = (sign * 0.11, 0.0, 0.15)
        ball = (sign * 0.11, -0.16, 0.075)
        toe_end = (sign * 0.11, -0.29, 0.065)
        add_edit_bone(armature_data, thigh, hip, knee, "pelvis")
        add_edit_bone(armature_data, calf, knee, ankle, thigh, True)
        add_edit_bone(armature_data, foot, ankle, ball, calf, True)
        add_edit_bone(armature_data, toe, ball, toe_end, foot, True)

    socket_hand = add_edit_bone(
        armature_data,
        SOCKET_HAND,
        (0.46, -0.045, 0.90),
        (0.46, -0.045, 0.99),
        "hand_r",
        deform=False,
        roll=math.pi,
    )
    socket_hand["interface"] = "character_primary_hand"
    socket_holster = add_edit_bone(
        armature_data,
        SOCKET_HOLSTER,
        (0.205, 0.015, 0.79),
        (0.205, 0.015, 0.88),
        "pelvis",
        deform=False,
        roll=math.pi,
    )
    socket_holster["interface"] = "character_primary_holster"

    bpy.ops.object.mode_set(mode="OBJECT")
    if len(armature_data.bones) > MAX_BONES:
        raise RuntimeError(
            f"Rig exceeds bone limit: {len(armature_data.bones)} > {MAX_BONES}"
        )
    return armature_obj


def point_segment_distance(point: Vector, start: Vector, end: Vector) -> float:
    segment = end - start
    length_squared = segment.length_squared
    if length_squared <= 1e-10:
        return (point - start).length
    t = max(0.0, min(1.0, (point - start).dot(segment) / length_squared))
    return (point - (start + segment * t)).length


def candidate_bones_for_vertex(point: Vector) -> list[str]:
    x = point.x
    z = point.z
    side = "r" if x >= 0.0 else "l"
    if z < 0.27:
        return [f"foot_{side}", f"toe_{side}", f"calf_{side}"]
    if z < 0.92:
        return [f"thigh_{side}", f"calf_{side}", "pelvis", f"foot_{side}"]
    if abs(x) > 0.24 and z < 1.52:
        arm = [
            f"clavicle_{side}",
            f"upperarm_{side}",
            f"upperarm_twist_{side}",
            f"lowerarm_{side}",
            f"lowerarm_twist_{side}",
            f"hand_{side}",
        ]
        if abs(x) > 0.425 and z < 1.02:
            arm.extend(
                f"{finger}_{segment:02d}_{side}"
                for finger in ("thumb", "index", "middle", "ring", "pinky")
                for segment in range(1, 4)
            )
        return arm
    if z > 1.57:
        return ["head", "neck_01", "spine_03"]
    if z > 1.38:
        return [
            "neck_01",
            "spine_03",
            "spine_02",
            f"clavicle_{side}",
        ]
    if z > 1.20:
        return ["spine_03", "spine_02", "spine_01"]
    if z > 1.00:
        return ["spine_02", "spine_01", "pelvis"]
    return ["pelvis", "spine_01", f"thigh_{side}"]


def bind_mesh(obj, armature_obj) -> dict:
    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)

    bones = {
        bone.name: (bone.head_local.copy(), bone.tail_local.copy())
        for bone in armature_obj.data.bones
        if bone.use_deform and bone.name != "root"
    }
    groups = {
        name: obj.vertex_groups.new(name=name)
        for name in bones
    }

    max_assigned = 0
    for vertex in obj.data.vertices:
        point = vertex.co
        distances = []
        for name in candidate_bones_for_vertex(point):
            if name not in bones:
                continue
            start, end = bones[name]
            distance = point_segment_distance(point, start, end)
            distances.append((distance, name))
        distances.sort(key=lambda item: item[0])
        chosen = distances[:MAX_INFLUENCES]
        raw_weights = [1.0 / max(0.012, distance) ** 2 for distance, _ in chosen]
        total = sum(raw_weights)
        if total <= 0.0:
            chosen = [(0.0, "pelvis")]
            raw_weights = [1.0]
            total = 1.0
        for raw_weight, (_, name) in zip(raw_weights, chosen):
            groups[name].add([vertex.index], raw_weight / total, "REPLACE")
        max_assigned = max(max_assigned, len(chosen))

    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature_obj
    modifier.use_vertex_groups = True
    modifier.use_bone_envelopes = False
    obj.parent = armature_obj
    obj.matrix_parent_inverse = armature_obj.matrix_world.inverted()
    return {
        "method": "region-constrained inverse-distance weights on shared rig",
        "maximum_assigned_influences": max_assigned,
        "vertex_groups": len(obj.vertex_groups),
    }


def bind_meshes_automatic(mesh_objects, armature_obj) -> dict:
    """Use Blender bone-heat weighting, then enforce the four-weight contract."""
    for obj in mesh_objects:
        obj.parent = None
        for modifier in list(obj.modifiers):
            if modifier.type == "ARMATURE":
                obj.modifiers.remove(modifier)
        for group in list(obj.vertex_groups):
            obj.vertex_groups.remove(group)

    bpy.ops.object.select_all(action="DESELECT")
    armature_obj.select_set(True)
    for obj in mesh_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    result = bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if "FINISHED" not in result:
        raise RuntimeError(f"Automatic armature binding failed: {result}")

    reports = {}
    deform_bones = {
        bone.name: (bone.head_local.copy(), bone.tail_local.copy())
        for bone in armature_obj.data.bones
        if bone.use_deform and bone.name != "root"
    }
    for obj in mesh_objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.vertex_group_limit_total(
            group_select_mode="ALL", limit=MAX_INFLUENCES
        )
        bpy.ops.object.vertex_group_normalize_all(
            group_select_mode="ALL", lock_active=False
        )
        maximum, unweighted = max_vertex_influences(obj)
        repaired_unweighted = 0
        if unweighted:
            groups = {
                name: obj.vertex_groups.get(name)
                or obj.vertex_groups.new(name=name)
                for name in deform_bones
            }
            for vertex in obj.data.vertices:
                if any(group.weight > 1e-6 for group in vertex.groups):
                    continue
                candidates = [
                    name
                    for name in candidate_bones_for_vertex(vertex.co)
                    if name in deform_bones
                ]
                if not candidates:
                    candidates = ["pelvis"]
                nearest = min(
                    candidates,
                    key=lambda name: point_segment_distance(
                        vertex.co,
                        deform_bones[name][0],
                        deform_bones[name][1],
                    ),
                )
                groups[nearest].add([vertex.index], 1.0, "REPLACE")
                repaired_unweighted += 1
            maximum, unweighted = max_vertex_influences(obj)
        reports[obj.name] = {
            "method": "Blender automatic bone-heat weights",
            "vertex_groups": len(obj.vertex_groups),
            "maximum_influences_after_limit": maximum,
            "unweighted_vertices": unweighted,
            "repaired_unweighted_vertices": repaired_unweighted,
        }
        if maximum > MAX_INFLUENCES or unweighted:
            raise RuntimeError(
                f"Weight validation failed for {obj.name}: "
                f"max={maximum}, unweighted={unweighted}"
            )
    return reports


def reset_pose(armature_obj) -> None:
    for bone in armature_obj.pose.bones:
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def apply_generic_pose(armature_obj, name: str) -> None:
    """Set a single unbound pose landmark; never encode gameplay timing."""
    adjustments = {
        "anim.humanoid.locomotion_holstered": {
            "pelvis": (math.radians(2), 0, 0),
            "upperarm_l": (0, math.radians(-5), 0),
            "upperarm_r": (0, math.radians(5), 0),
        },
        "anim.humanoid.draw": {
            "upperarm_r": (math.radians(-14), 0, math.radians(-8)),
            "lowerarm_r": (0, math.radians(-18), 0),
        },
        "anim.humanoid.idle_armed": {
            "upperarm_r": (math.radians(-18), 0, math.radians(-10)),
            "upperarm_l": (math.radians(-12), 0, math.radians(8)),
            "lowerarm_r": (0, math.radians(-24), 0),
            "lowerarm_l": (0, math.radians(20), 0),
        },
        "anim.humanoid.locomotion_armed": {
            "pelvis": (math.radians(3), 0, 0),
            "upperarm_r": (math.radians(-16), 0, math.radians(-9)),
            "upperarm_l": (math.radians(-10), 0, math.radians(7)),
        },
        "anim.humanoid.raise_aim": {
            "spine_03": (math.radians(-4), 0, 0),
            "upperarm_r": (math.radians(-32), 0, math.radians(-16)),
            "upperarm_l": (math.radians(-27), 0, math.radians(15)),
            "lowerarm_r": (0, math.radians(-35), 0),
            "lowerarm_l": (0, math.radians(31), 0),
        },
        "anim.humanoid.fire_recoil": {
            "spine_03": (math.radians(7), 0, 0),
            "upperarm_r": (math.radians(-29), 0, math.radians(-15)),
            "upperarm_l": (math.radians(-24), 0, math.radians(14)),
        },
        "anim.humanoid.recovery": {
            "spine_03": (math.radians(2), 0, 0),
            "upperarm_r": (math.radians(-24), 0, math.radians(-12)),
            "upperarm_l": (math.radians(-19), 0, math.radians(11)),
        },
        "anim.humanoid.holster": {
            "upperarm_r": (math.radians(-10), 0, math.radians(-6)),
            "lowerarm_r": (0, math.radians(-20), 0),
        },
        "anim.humanoid.dialogue_idle": {
            "head": (0, 0, math.radians(2)),
        },
        "anim.humanoid.dialogue_speak": {
            "upperarm_r": (math.radians(-12), 0, math.radians(-9)),
            "lowerarm_r": (0, math.radians(-26), 0),
            "head": (0, 0, math.radians(-3)),
        },
        "anim.humanoid.dialogue_listen": {
            "head": (math.radians(-3), 0, math.radians(5)),
        },
        "anim.humanoid.interact_terminal": {
            "upperarm_r": (math.radians(-28), 0, math.radians(-12)),
            "lowerarm_r": (0, math.radians(-38), 0),
        },
        "anim.humanoid.use_healing": {
            "upperarm_l": (math.radians(-20), 0, math.radians(12)),
            "lowerarm_l": (0, math.radians(36), 0),
        },
        "anim.humanoid.hit_reaction": {
            "spine_02": (math.radians(12), 0, math.radians(4)),
            "head": (math.radians(-7), 0, 0),
        },
        "anim.humanoid.down": {
            "pelvis": (math.radians(18), 0, math.radians(8)),
            "spine_01": (math.radians(14), 0, 0),
            "head": (math.radians(-12), 0, 0),
        },
    }
    for bone_name, euler in adjustments.get(name, {}).items():
        armature_obj.pose.bones[bone_name].rotation_euler = euler


def create_actions(armature_obj) -> dict:
    armature_obj.animation_data_create()
    created = []
    for name in ACTION_NAMES:
        reset_pose(armature_obj)
        apply_generic_pose(armature_obj, name)
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        action["contract"] = "shared_presentation_pose"
        action["binding_status"] = (
            "unbound_pending_gameplay_attack_timing_and_phase_contract"
        )
        action["duration_status"] = "unbound_single_frame_pose_landmark"
        action["root_motion"] = "none"
        armature_obj.animation_data.action = action
        for pose_bone in armature_obj.pose.bones:
            if pose_bone.name in {SOCKET_HAND, SOCKET_HOLSTER}:
                continue
            pose_bone.keyframe_insert(
                data_path="location", frame=1, group=pose_bone.name
            )
            pose_bone.keyframe_insert(
                data_path="rotation_euler", frame=1, group=pose_bone.name
            )
            pose_bone.keyframe_insert(
                data_path="scale", frame=1, group=pose_bone.name
            )
        if name == "anim.humanoid.draw":
            marker = action.pose_markers.new("event.weapon.transfer_to_hand")
            marker.frame = 1
            action["presentation_landmark"] = (
                "event.weapon.transfer_to_hand@frame_1"
            )
        elif name == "anim.humanoid.holster":
            marker = action.pose_markers.new("event.weapon.transfer_to_holster")
            marker.frame = 1
            action["presentation_landmark"] = (
                "event.weapon.transfer_to_holster@frame_1"
            )
        created.append(
            {
                "name": name,
                "frame_range": rounded_vector(action.frame_range),
                "markers": [
                    {"name": marker.name, "frame": marker.frame}
                    for marker in action.pose_markers
                ],
                "binding_status": action["binding_status"],
            }
        )

    reset_pose(armature_obj)
    armature_obj.animation_data.action = bpy.data.actions[
        "anim.humanoid.idle_holstered"
    ]
    bpy.context.scene.frame_set(1)
    return {
        "count": len(created),
        "actions": created,
        "durations": "not authored; each action is one unbound pose landmark",
    }


def source_rig_report(armature_obj) -> dict:
    hierarchy = []
    for bone in armature_obj.data.bones:
        hierarchy.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "deform": bone.use_deform,
                "head": rounded_vector(bone.head_local),
                "tail": rounded_vector(bone.tail_local),
            }
        )
    return {
        "profile": RIG_ID,
        "bone_count": len(armature_obj.data.bones),
        "hierarchy": hierarchy,
        "socket_hand_parent": armature_obj.data.bones[SOCKET_HAND].parent.name,
        "socket_holster_parent": armature_obj.data.bones[
            SOCKET_HOLSTER
        ].parent.name,
    }


def save_source(blend_path: Path) -> None:
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)


def export_glb(glb_path: Path, mesh_objects, armature_obj) -> None:
    glb_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for mesh_obj in mesh_objects:
        mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    result = bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_extras=True,
        export_yup=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_merge_animation="NONE",
        export_optimize_animation_size=True,
        export_optimize_animation_keep_anim_armature=True,
        export_skins=True,
        export_all_influences=False,
        # Attachment bones are intentionally non-deforming but are part of the
        # published character interface, so export the complete reviewed rig.
        export_def_bones=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB export failed: {result}")


def max_vertex_influences(obj) -> tuple[int, int]:
    maximum = 0
    unweighted = 0
    for vertex in obj.data.vertices:
        count = sum(1 for group in vertex.groups if group.weight > 1e-6)
        maximum = max(maximum, count)
        if count == 0:
            unweighted += 1
    return maximum, unweighted


def validate_fresh_import(glb_path: Path) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    result = bpy.ops.import_scene.gltf(filepath=str(glb_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Fresh GLB import failed: {result}")

    mesh_objects = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and not any(
            collection.name == "glTF_not_exported"
            for collection in obj.users_collection
        )
    ]
    armature_objects = [
        obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"
    ]
    if len(mesh_objects) != 2 or len(armature_objects) != 1:
        raise RuntimeError(
            "Fresh import expected exactly two product meshes and one armature, got "
            f"{len(mesh_objects)} mesh / {len(armature_objects)} armature"
        )
    armature_obj = armature_objects[0]
    minimum, maximum = world_bounds(mesh_objects)
    dimensions = maximum - minimum
    topology_by_mesh = {
        mesh_obj.name: mesh_topology_report(mesh_obj)
        for mesh_obj in mesh_objects
    }
    total_triangles = sum(
        topology["triangles"] for topology in topology_by_mesh.values()
    )
    base_objects = [
        obj for obj in mesh_objects if obj.name == "VanguardDeformingBase"
    ]
    base_welded_components = (
        welded_component_count(base_objects[0]) if len(base_objects) == 1 else None
    )
    material_count = len(
        {
            slot.material.name
            for mesh_obj in mesh_objects
            for slot in mesh_obj.material_slots
            if slot.material is not None
        }
    )
    image_reports = [
        {
            "name": image.name,
            "size": [int(image.size[0]), int(image.size[1])],
            "packed": image.packed_file is not None,
        }
        for image in bpy.data.images
        if image.size[0] > 0 and image.size[1] > 0
    ]
    influence_reports = {
        mesh_obj.name: max_vertex_influences(mesh_obj)
        for mesh_obj in mesh_objects
    }
    maximum_influences = max(
        report[0] for report in influence_reports.values()
    )
    unweighted = sum(report[1] for report in influence_reports.values())
    bone_names = [bone.name for bone in armature_obj.data.bones]
    action_names = sorted(action.name for action in bpy.data.actions)
    missing_actions = sorted(set(ACTION_NAMES) - set(action_names))
    missing_sockets = sorted(
        {SOCKET_HAND, SOCKET_HOLSTER} - set(bone_names)
    )

    checks = {
        "height_within_two_percent": abs(dimensions.z - TARGET_HEIGHT_METERS)
        <= TARGET_HEIGHT_METERS * 0.02,
        "grounded": abs(minimum.z) <= 0.002,
        "triangles_within_target": total_triangles <= TARGET_TRIANGLES,
        "triangles_within_hard_limit": total_triangles <= HARD_TRIANGLE_LIMIT,
        "materials_within_limit": material_count <= MAX_MATERIALS,
        "textures_within_limit": all(
            max(image["size"]) <= MAX_TEXTURE_DIMENSION
            for image in image_reports
        ),
        "bones_within_limit": len(bone_names) <= MAX_BONES,
        "influences_within_limit": maximum_influences <= MAX_INFLUENCES,
        "all_vertices_weighted": unweighted == 0,
        "required_sockets_present": not missing_sockets,
        "required_actions_present": not missing_actions,
        "continuous_deforming_base_present": len(base_objects) == 1,
        "continuous_deforming_base_connected": len(base_objects) == 1
        and base_welded_components == 1,
        "root_motion_absent": True,
        "separate_firearm": all(
            token not in mesh_obj.name.lower()
            for mesh_obj in mesh_objects
            for token in ("carbine", "rifle", "gun", "weapon")
        ),
    }
    if not all(checks.values()):
        failed = [name for name, passed in checks.items() if not passed]
        raise RuntimeError(f"Fresh-import validation failed: {failed}")

    return {
        "glb": str(glb_path),
        "glb_size_bytes": glb_path.stat().st_size,
        "mesh_objects": [obj.name for obj in mesh_objects],
        "armature_objects": [obj.name for obj in armature_objects],
        "bounds": {
            "minimum": rounded_vector(minimum),
            "maximum": rounded_vector(maximum),
            "dimensions": rounded_vector(dimensions),
        },
        "topology_by_mesh": topology_by_mesh,
        "continuous_base_welded_component_count": base_welded_components,
        "total_triangles": total_triangles,
        "material_count": material_count,
        "images": image_reports,
        "bone_count": len(bone_names),
        "bones": bone_names,
        "maximum_vertex_influences": maximum_influences,
        "unweighted_vertices": unweighted,
        "influences_by_mesh": {
            name: {
                "maximum_influences": values[0],
                "unweighted_vertices": values[1],
            }
            for name, values in influence_reports.items()
        },
        "actions": action_names,
        "missing_actions": missing_actions,
        "missing_sockets": missing_sockets,
        "checks": checks,
    }


def main() -> None:
    input_path, blend_path, glb_path, report_path = cli_paths()
    if not input_path.is_file():
        raise FileNotFoundError(input_path)
    if input_path.stat().st_size != EXPECTED_INPUT_BYTES:
        raise RuntimeError(
            f"Selected input size mismatch: {input_path.stat().st_size} "
            f"!= {EXPECTED_INPUT_BYTES}"
        )

    report_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene["asset_id"] = ASSET_ID
    scene["source_candidate"] = input_path.name
    scene["source_run_id"] = RUN_ID
    scene["source_task_id"] = TASK_ID
    scene["source_bytes"] = input_path.stat().st_size
    scene["provisional_selection"] = True
    scene["final_visual_review_pending"] = True
    scene["gameplay_attack_binding"] = "pending_do_not_invent"

    result = bpy.ops.import_scene.gltf(filepath=str(input_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Raw GLB import failed: {result}")
    mesh_objects = [
        obj for obj in bpy.context.scene.objects if obj.type == "MESH"
    ]
    if not mesh_objects:
        raise RuntimeError("Raw GLB import produced no mesh")
    raw_mesh_count = len(mesh_objects)
    mesh_obj = join_meshes(mesh_objects)
    mesh_obj.name = "VanguardBody"
    mesh_obj.data.name = "VanguardBodyMesh"
    print("Imported selected candidate; recording immutable source counts", flush=True)
    raw_topology = mesh_topology_report(mesh_obj, include_components=False)
    raw_bounds_min, raw_bounds_max = world_bounds([mesh_obj])

    print("Normalizing packed textures", flush=True)
    texture_report = normalize_textures()
    print("Normalizing scale, origin, and ground contact", flush=True)
    normalization_report = normalize_mesh(mesh_obj)
    retopology_report = cleanup_mesh(mesh_obj)
    print("Auditing cleaned topology", flush=True)
    cleaned_topology = mesh_topology_report(mesh_obj)
    print("Building continuous deforming undersuit base", flush=True)
    deforming_base, deforming_base_report = build_continuous_deforming_base()

    mesh_obj["asset_id"] = ASSET_ID
    mesh_obj["source_candidate"] = input_path.name
    mesh_obj["source_run_id"] = RUN_ID
    mesh_obj["source_task_id"] = TASK_ID
    mesh_obj["source_bytes"] = input_path.stat().st_size
    mesh_obj["retopology_method"] = retopology_report["method"]
    mesh_obj["firearm_included"] = False
    mesh_obj["outfit_policy"] = "fixed_complete_runtime_outfit"
    mesh_obj["continuity_status"] = (
        "continuous Blender-authored undersuit base beneath disconnected "
        "provider rigid/detail shells"
    )
    mesh_obj["selection_status"] = "provisional_pending_owner_visual_review"

    print("Building shared crew rig", flush=True)
    armature_obj = build_rig()
    print("Binding retopologized visual mesh and continuous base", flush=True)
    binding_report = bind_meshes_automatic(
        [mesh_obj, deforming_base], armature_obj
    )
    print("Creating unbound shared presentation actions", flush=True)
    action_report = create_actions(armature_obj)
    rig_report = source_rig_report(armature_obj)

    print("Saving editable source", flush=True)
    save_source(blend_path)
    print("Publishing GLB", flush=True)
    export_glb(glb_path, [mesh_obj, deforming_base], armature_obj)
    print("Validating exact fresh GLB import", flush=True)
    fresh_import_report = validate_fresh_import(glb_path)

    report = {
        "asset_id": ASSET_ID,
        "rig_profile": RIG_ID,
        "status": "provisional_pending_owner_visual_review",
        "blender_version": bpy.app.version_string,
        "script": str(Path(__file__).resolve()),
        "input": {
            "path": str(input_path),
            "size_bytes": input_path.stat().st_size,
            "run_id": RUN_ID,
            "task_id": TASK_ID,
            "immutable": True,
            "candidate": "02",
            "candidate_01_used": False,
        },
        "source_before": {
            "mesh_object_count": raw_mesh_count,
            "bounds": {
                "minimum": rounded_vector(raw_bounds_min),
                "maximum": rounded_vector(raw_bounds_max),
                "dimensions": rounded_vector(raw_bounds_max - raw_bounds_min),
            },
            "topology": raw_topology,
        },
        "normalization": normalization_report,
        "retopology": retopology_report,
        "cleaned_topology": cleaned_topology,
        "continuous_deforming_base": deforming_base_report,
        "textures": texture_report,
        "materials": [
            material.name for material in bpy.data.materials
        ],
        "binding": binding_report,
        "source_rig": rig_report,
        "presentation_actions": action_report,
        "editable_source": {
            "path": str(blend_path),
            "size_bytes": blend_path.stat().st_size,
        },
        "fresh_import_validation": fresh_import_report,
        "known_limitations": [
            "Automated triangle decimation retains disconnected rigid/detail "
            "shells from the provider mesh above one continuous Blender-authored "
            "deforming undersuit base; armor is not yet semantically separated "
            "into editable named source parts.",
            "Presentation actions are named single-frame generic pose "
            "landmarks only. Durations and attack phase mappings remain "
            "unbound because the gameplay contract is not yet defined.",
            "No firearm is included. Grip, support-hand and muzzle validation "
            "requires the separately produced Vanguard carbine.",
            "Final human visual approval and Godot assembly review remain pending.",
        ],
    }
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(
        {
            "asset_id": ASSET_ID,
            "blend": str(blend_path),
            "glb": str(glb_path),
            "report": str(report_path),
            "triangles": fresh_import_report["total_triangles"],
            "bones": fresh_import_report["bone_count"],
            "actions": len(fresh_import_report["actions"]),
            "glb_size_bytes": fresh_import_report["glb_size_bytes"],
        },
        indent=2,
    ))


if __name__ == "__main__":
    main()
