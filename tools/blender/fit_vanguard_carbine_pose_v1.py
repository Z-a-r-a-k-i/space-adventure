"""Fit Vanguard's provisional one-frame carbine presentation poses.

Run with Blender 5.2 LTS:

    blender --background --factory-startup \
      --python tools/blender/fit_vanguard_carbine_pose_v1.py -- \
      <vanguard.blend> <carbine.blend> <report.json>

The Vanguard Blend is updated in place. Use an ignored copy for the first
review pass. This script does not author gameplay timing: it only replaces the
existing one-frame pose landmarks for the two-hand assembly and establishes a
reviewable rear-right holster transform.
"""

from __future__ import annotations

import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


CHARACTER_ID = "character.crew.vanguard.v1"
WEAPON_ID = "weapon.crew.vanguard_carbine.v1"
RIG_NAME = "VanguardRig"
HAND_SOCKET = "socket.weapon.hand_primary"
HOLSTER_SOCKET = "socket.weapon.holster_primary"

POSE_TARGETS = {
    # Right-hand-head targets keep the stock near the right shoulder. A small
    # leftward yaw pulls the support grip into the left arm's natural reach
    # while keeping the muzzle clear and the receiver in front of the torso.
    "anim.humanoid.idle_armed": {
        "right_hand": Vector((0.225, -0.141, 1.26)),
        "weapon_yaw_degrees": -15.0,
    },
    "anim.humanoid.raise_aim": {
        "right_hand": Vector((0.205, -0.201, 1.35)),
        "weapon_yaw_degrees": -15.0,
    },
    "anim.humanoid.fire_recoil": {
        "right_hand": Vector((0.205, -0.161, 1.37)),
        "weapon_yaw_degrees": -15.0,
    },
    "anim.humanoid.recovery": {
        "right_hand": Vector((0.205, -0.181, 1.33)),
        "weapon_yaw_degrees": -15.0,
    },
}


def parse_paths() -> tuple[Path, Path, Path]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError(
            "Expected -- <vanguard.blend> <carbine.blend> <report.json>"
        ) from exc
    values = [Path(value).resolve() for value in sys.argv[separator + 1 :]]
    if len(values) != 3:
        raise RuntimeError("Expected Vanguard source, carbine source, and report")
    for source in values[:2]:
        if not source.is_file():
            raise FileNotFoundError(source)
    values[2].parent.mkdir(parents=True, exist_ok=True)
    return values[0], values[1], values[2]


def rounded(vector: Vector) -> list[float]:
    return [round(float(component), 8) for component in vector]


def rest_palm_centers(
    armature: bpy.types.Object,
) -> dict[str, Vector]:
    """Measure each generated palm from hand-bone-weighted rest geometry."""

    previous_pose_position = armature.data.pose_position
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    result: dict[str, Vector] = {}
    try:
        for side in ("l", "r"):
            group_names = {f"hand_{side}"}
            points: list[Vector] = []
            for obj in bpy.context.scene.objects:
                if obj.type != "MESH" or obj.find_armature() != armature:
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
                        if weight >= 0.35:
                            world = evaluated.matrix_world @ evaluated_vertex.co
                            points.append(armature.matrix_world.inverted() @ world)
                finally:
                    evaluated.to_mesh_clear()
            if not points:
                raise RuntimeError(f"Could not measure the {side} palm geometry")
            result[side] = sum(points, Vector()) / len(points)
    finally:
        armature.data.pose_position = previous_pose_position
        bpy.context.view_layer.update()
    return result


def normalize_arm_chains(
    armature: bpy.types.Object,
) -> dict[str, object]:
    """Correct the provisional rig's misplaced elbow joints.

    The original source placed each elbow near the wrist, producing a roughly
    45 cm upper arm and 18 cm forearm. Put the elbow at the midpoint of the
    straight neutral A-pose chain so the shared skeleton can form stable,
    retargetable two-hand poses without changing the neutral mesh silhouette.
    """

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    report: dict[str, object] = {}
    for side in ("l", "r"):
        upper = armature.data.edit_bones[f"upperarm_{side}"]
        lower = armature.data.edit_bones[f"lowerarm_{side}"]
        original_elbow = upper.tail.copy()
        shoulder = upper.head.copy()
        wrist = lower.tail.copy()
        elbow = shoulder.lerp(wrist, 0.5)
        upper.tail = elbow
        lower.head = elbow
        report[side] = {
            "original_elbow_m": rounded(original_elbow),
            "normalized_elbow_m": rounded(elbow),
            "upperarm_length_m": round(float(upper.length), 8),
            "lowerarm_length_m": round(float(lower.length), 8),
        }
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    return report


def rebalance_arm_weights(
    armature: bpy.types.Object,
) -> dict[str, object]:
    """Redistribute legacy arm weights around the corrected elbow joints."""

    report: dict[str, object] = {}
    armature_inverse = armature.matrix_world.inverted()
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj.find_armature() != armature:
            continue
        changed = 0
        side_counts = {"l": 0, "r": 0}
        for side in ("l", "r"):
            upper_group = obj.vertex_groups.get(f"upperarm_{side}")
            lower_group = obj.vertex_groups.get(f"lowerarm_{side}")
            if upper_group is None or lower_group is None:
                continue
            upper_index = upper_group.index
            lower_index = lower_group.index
            shoulder = armature.data.bones[f"upperarm_{side}"].head_local
            wrist = armature.data.bones[f"lowerarm_{side}"].tail_local
            chain = wrist - shoulder
            chain_length_squared = chain.length_squared
            for vertex in obj.data.vertices:
                weights = {
                    membership.group: membership.weight
                    for membership in vertex.groups
                }
                total = weights.get(upper_index, 0.0) + weights.get(
                    lower_index,
                    0.0,
                )
                if total <= 1.0e-6:
                    continue
                armature_position = (
                    armature_inverse @ obj.matrix_world @ vertex.co
                )
                along = (armature_position - shoulder).dot(chain)
                along /= chain_length_squared
                transition = max(0.0, min(1.0, (along - 0.42) / 0.16))
                lower_factor = transition * transition * (3.0 - 2.0 * transition)
                upper_weight = total * (1.0 - lower_factor)
                lower_weight = total * lower_factor
                if upper_weight <= 1.0e-5:
                    upper_group.remove([vertex.index])
                else:
                    upper_group.add([vertex.index], upper_weight, "REPLACE")
                if lower_weight <= 1.0e-5:
                    lower_group.remove([vertex.index])
                else:
                    lower_group.add([vertex.index], lower_weight, "REPLACE")
                changed += 1
                side_counts[side] += 1

        # Preserve the four-influence export contract after adding elbow blend.
        for vertex in obj.data.vertices:
            memberships = sorted(
                (
                    (membership.group, membership.weight)
                    for membership in vertex.groups
                    if membership.weight > 1.0e-6
                ),
                key=lambda item: item[1],
                reverse=True,
            )
            if len(memberships) <= 4:
                continue
            keep = memberships[:4]
            keep_indexes = {index for index, _weight in keep}
            keep_total = sum(weight for _index, weight in keep)
            for index, _weight in memberships[4:]:
                obj.vertex_groups[index].remove([vertex.index])
            for index, weight in keep:
                obj.vertex_groups[index].add(
                    [vertex.index],
                    weight / keep_total,
                    "REPLACE",
                )
        report[obj.name] = {
            "vertices_redistributed": changed,
            "by_side": side_counts,
            "method": "smoothstep split across normalized arm-chain 0.42-0.58",
        }
    bpy.context.view_layer.update()
    return report


def calibrate_hand_socket(
    armature: bpy.types.Object,
    palm_centers: dict[str, Vector],
) -> dict[str, object]:
    """Place the primary socket at the generated right palm, not its wrist."""

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    socket = armature.data.edit_bones[HAND_SOCKET]
    original_head = socket.head.copy()
    original_axis = socket.tail - socket.head
    socket.head = palm_centers["r"]
    socket.tail = palm_centers["r"] + original_axis
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    return {
        "original_head_m": rounded(original_head),
        "calibrated_head_m": rounded(palm_centers["r"]),
        "method": (
            "centroid of rest-pose vertices weighted to the hand bone; "
            "large binary hashes intentionally omitted"
        ),
    }


def read_weapon_offsets(path: Path) -> dict[str, Vector]:
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    root = bpy.data.objects.get(WEAPON_ID)
    if root is None:
        raise RuntimeError("Carbine source root is missing")
    inverse = root.matrix_world.inverted()
    result = {}
    for name in (
        "socket.grip.primary",
        "socket.grip.support",
        "socket.attack.muzzle.primary",
    ):
        marker = bpy.data.objects.get(name)
        if marker is None:
            raise RuntimeError(f"Carbine marker is missing: {name}")
        result[name] = inverse @ marker.matrix_world.translation
    if result["socket.grip.primary"].length > 1.0e-6:
        raise RuntimeError("Carbine root no longer coincides with the primary grip")
    return result


def solve_arm(
    armature: bpy.types.Object,
    side: str,
    target_location: Vector,
    orientation_delta,
) -> dict[str, object]:
    upper_name = f"upperarm_{side}"
    lower_name = f"lowerarm_{side}"
    hand_name = f"hand_{side}"
    lower = armature.pose.bones[lower_name]
    hand = armature.pose.bones[hand_name]

    sign = -1.0 if side == "l" else 1.0
    upper = armature.pose.bones[upper_name]
    shoulder = upper.head.copy()
    direction = target_location - shoulder
    distance = direction.length
    upper_length = armature.data.bones[upper_name].length
    lower_length = armature.data.bones[lower_name].length
    maximum_reach = upper_length + lower_length
    minimum_reach = abs(upper_length - lower_length)
    if not minimum_reach < distance < maximum_reach:
        raise RuntimeError(
            f"{side} wrist target at {distance:.4f} m is outside arm reach "
            f"({minimum_reach:.4f}, {maximum_reach:.4f})"
        )

    direction.normalize()
    along = (
        upper_length * upper_length
        - lower_length * lower_length
        + distance * distance
    ) / (2.0 * distance)
    height = math.sqrt(max(upper_length * upper_length - along * along, 0.0))
    circle_center = shoulder + direction * along
    pole_direction = (
        Vector((0.60 * sign, 0.08, 1.00)) - circle_center
    )
    pole_direction -= direction * pole_direction.dot(direction)
    if pole_direction.length < 1.0e-6:
        raise RuntimeError(f"{side} arm pole direction is degenerate")
    elbow = circle_center + pole_direction.normalized() * height

    def align_pose_bone(
        pose_bone: bpy.types.PoseBone,
        head: Vector,
        tail: Vector,
    ) -> None:
        rest_bone = armature.data.bones[pose_bone.name]
        rest_direction = (rest_bone.tail_local - rest_bone.head_local).normalized()
        desired_direction = (tail - head).normalized()
        delta = rest_direction.rotation_difference(desired_direction).to_matrix()
        rotation = delta @ rest_bone.matrix_local.to_3x3()
        pose_bone.matrix = Matrix.Translation(head) @ rotation.to_4x4()

    align_pose_bone(upper, shoulder, elbow)
    bpy.context.view_layer.update()
    align_pose_bone(lower, elbow, target_location)
    bpy.context.view_layer.update()
    solved_hand_head = hand.head.copy()

    # Keep each palm's reviewed rest orientation while preserving the solved
    # wrist position. The vertical carbine grips then remain legible and the
    # hand socket does not roll the complete weapon unpredictably.
    rest_rotation = armature.data.bones[hand_name].matrix_local.to_3x3()
    hand.matrix = (
        Matrix.Translation(solved_hand_head)
        @ (orientation_delta @ rest_rotation).to_4x4()
    )
    bpy.context.view_layer.update()

    return {
        "side": side,
        "target": rounded(target_location),
        "elbow": rounded(elbow),
        "upperarm_length_m": round(float(upper_length), 8),
        "lowerarm_length_m": round(float(lower_length), 8),
        "hand_head": rounded(hand.head),
        "target_error_m": round(float((hand.head - target_location).length), 8),
    }


def key_current_pose(armature: bpy.types.Object, action: bpy.types.Action) -> None:
    armature.animation_data.action = action
    for pose_bone in armature.pose.bones:
        if pose_bone.name in {HAND_SOCKET, HOLSTER_SOCKET}:
            continue
        pose_bone.rotation_mode = "XYZ"
        pose_bone.keyframe_insert(
            data_path="location",
            frame=1,
            group=pose_bone.name,
        )
        pose_bone.keyframe_insert(
            data_path="rotation_euler",
            frame=1,
            group=pose_bone.name,
        )
        pose_bone.keyframe_insert(
            data_path="scale",
            frame=1,
            group=pose_bone.name,
        )
    action["contract"] = "shared_presentation_pose"
    action["binding_status"] = (
        "vanguard_carbine_pose_bound_provisional_hand_grip_visual_revision"
    )
    action["duration_status"] = "single_frame_pose_no_gameplay_timing"
    action["assembly_fit"] = "vanguard_carbine_fit_v1"
    action["root_motion"] = "none"


def apply_grip_pose(armature: bpy.types.Object) -> None:
    """Curl both hands around the rigid vertical grips.

    These are presentation-only finger rotations on the shared hierarchy. They
    do not imply reload, trigger, attack timing, or weapon simulation.
    """

    finger_curl = {
        1: math.radians(35.0),
        2: math.radians(55.0),
        3: math.radians(42.0),
    }
    for side in ("l", "r"):
        for finger in ("index", "middle", "ring", "pinky"):
            for segment, rotation in finger_curl.items():
                bone = armature.pose.bones[f"{finger}_0{segment}_{side}"]
                bone.rotation_mode = "XYZ"
                bone.rotation_euler.x = rotation
        for segment, rotation in {
            1: math.radians(28.0),
            2: math.radians(40.0),
            3: math.radians(32.0),
        }.items():
            thumb = armature.pose.bones[f"thumb_0{segment}_{side}"]
            thumb.rotation_mode = "XYZ"
            thumb.rotation_euler.x = rotation
        armature.pose.bones[f"thumb_01_{side}"].rotation_euler.z = (
            math.radians(12.0) if side == "l" else math.radians(-12.0)
        )
    bpy.context.view_layer.update()


def fit_action(
    armature: bpy.types.Object,
    action_name: str,
    right_target: Vector,
    support_offset: Vector,
    weapon_yaw_degrees: float,
    palm_offsets: dict[str, Vector],
) -> dict[str, object]:
    action = bpy.data.actions.get(action_name)
    if action is None:
        raise RuntimeError(f"Missing action: {action_name}")
    armature.animation_data.action = action
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    weapon_rotation = Matrix.Rotation(
        math.radians(weapon_yaw_degrees),
        3,
        "Z",
    )
    right_result = solve_arm(
        armature,
        "r",
        right_target,
        weapon_rotation,
    )
    hand_socket = armature.pose.bones[HAND_SOCKET]
    hand_rest_rotation = (
        armature.matrix_world
        @ armature.data.bones[HAND_SOCKET].matrix_local
    ).to_3x3()
    socket_world = armature.matrix_world @ hand_socket.matrix
    evaluated_weapon_rotation = (
        socket_world.to_3x3() @ hand_rest_rotation.inverted()
    )
    primary_position = socket_world.translation
    support_target = (
        primary_position + evaluated_weapon_rotation @ support_offset
    )
    left_wrist_target = (
        support_target
        - evaluated_weapon_rotation @ palm_offsets["l"]
    )
    left_result = solve_arm(
        armature,
        "l",
        left_wrist_target,
        evaluated_weapon_rotation,
    )
    apply_grip_pose(armature)
    key_current_pose(armature, action)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    left_position = armature.matrix_world @ armature.pose.bones["hand_l"].head
    left_palm_position = (
        left_position + evaluated_weapon_rotation @ palm_offsets["l"]
    )
    socket_position = armature.matrix_world @ hand_socket.head
    socket_world = armature.matrix_world @ hand_socket.matrix
    evaluated_weapon_rotation = (
        socket_world.to_3x3() @ hand_rest_rotation.inverted()
    )
    support_position = (
        socket_position + evaluated_weapon_rotation @ support_offset
    )
    return {
        "action": action_name,
        "weapon_yaw_degrees": weapon_yaw_degrees,
        "right_arm": right_result,
        "left_arm": left_result,
        "primary_socket_position_m": rounded(socket_position),
        "support_grip_position_m": rounded(support_position),
        "support_wrist_position_m": rounded(left_position),
        "support_palm_position_m": rounded(left_palm_position),
        "support_hand_gap_m": round(
            float((left_palm_position - support_position).length),
            8,
        ),
        "frame_range": [float(value) for value in action.frame_range],
        "timing_authority": "none; single reviewed pose at frame 1",
    }


def fit_holster(armature: bpy.types.Object) -> dict[str, object]:
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    hand = armature.data.edit_bones[HAND_SOCKET]
    holster = armature.data.edit_bones[HOLSTER_SOCKET]

    hand_rotation = hand.matrix.to_3x3()
    weapon_rotation = Matrix.Rotation(math.radians(90.0), 3, "X")
    socket_rotation = weapon_rotation @ hand_rotation
    position = Vector((0.20, 0.42, 1.35))
    holster.matrix = Matrix.Translation(position) @ socket_rotation.to_4x4()
    holster.length = 0.09

    bpy.ops.object.mode_set(mode="POSE")
    bpy.context.view_layer.update()
    result = {
        "location_m": rounded(
            armature.data.bones[HOLSTER_SOCKET].head_local
        ),
        "carry": "rear-right/back, muzzle down",
        "selection_status": "provisional pending owner visual review",
    }
    bpy.ops.object.mode_set(mode="OBJECT")
    return result


character_path, weapon_path, report_path = parse_paths()
offsets = read_weapon_offsets(weapon_path)
bpy.ops.wm.open_mainfile(filepath=str(character_path), load_ui=False)
armature = bpy.data.objects.get(RIG_NAME)
if armature is None or armature.get("asset_id") != CHARACTER_ID:
    raise RuntimeError("Vanguard rig is missing or has the wrong asset ID")

arm_chain_normalization = normalize_arm_chains(armature)
arm_weight_rebalance = rebalance_arm_weights(armature)
palm_centers = rest_palm_centers(armature)
palm_offsets = {
    side: palm_centers[side] - armature.data.bones[f"hand_{side}"].head_local
    for side in ("l", "r")
}
socket_calibration = calibrate_hand_socket(armature, palm_centers)

bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="POSE")
action_reports = [
    fit_action(
        armature,
        action_name,
        pose_spec["right_hand"],
        offsets["socket.grip.support"],
        pose_spec["weapon_yaw_degrees"],
        palm_offsets,
    )
    for action_name, pose_spec in POSE_TARGETS.items()
]
bpy.ops.object.mode_set(mode="OBJECT")
holster_report = fit_holster(armature)

armature["carbine_pose_fit"] = "vanguard_carbine_fit_v1"
armature["carbine_pose_timing"] = "not_authored"
armature["carbine_pose_visual_status"] = (
    "provisional_revise_generated_glove_grip_closure"
)
armature["holster_selection"] = "rear_right_back_provisional"
armature["arm_chain_normalization"] = "balanced_neutral_midpoint_v1"
armature["arm_weight_rebalance"] = "smoothstep_elbow_split_v1"
armature.animation_data.action = bpy.data.actions[
    "anim.humanoid.idle_holstered"
]
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
bpy.context.preferences.filepaths.save_version = 0
save_result = bpy.ops.wm.save_as_mainfile(
    filepath=str(character_path),
    check_existing=False,
    compress=True,
    relative_remap=True,
)
if "FINISHED" not in save_result:
    raise RuntimeError(f"Could not save fitted Vanguard source: {save_result}")

report = {
    "generated_utc": datetime.now(timezone.utc).isoformat(),
    "blender_version": bpy.app.version_string,
    "asset_id": CHARACTER_ID,
    "source": str(character_path),
    "source_bytes_after_save": character_path.stat().st_size,
    "weapon_source": str(weapon_path),
    "weapon_offsets": {
        name: rounded(value) for name, value in offsets.items()
    },
    "palm_centers_rest_m": {
        side: rounded(value) for side, value in palm_centers.items()
    },
    "palm_offsets_from_wrist_rest_m": {
        side: rounded(value) for side, value in palm_offsets.items()
    },
    "arm_chain_normalization": arm_chain_normalization,
    "arm_weight_rebalance": arm_weight_rebalance,
    "primary_hand_socket_calibration": socket_calibration,
    "actions": action_reports,
    "holster": holster_report,
    "scope": (
        "One-frame presentation pose and carry-transform fit only. No gameplay "
        "attack identity, timing, damage, range, ability, VFX, or audio authored."
    ),
    "decision": "provisional_revise_visible_hand_grip",
    "known_defect": (
        "The generated glove topology remains visibly open below the primary "
        "and support grips in close review. Static assets and socket mechanics "
        "pass; finished weapon handling animation does not."
    ),
}
report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("SPACE_ADVENTURE_RESULT=" + json.dumps(report, sort_keys=True))
