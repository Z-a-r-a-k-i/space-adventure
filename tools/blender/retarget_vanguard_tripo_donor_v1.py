"""Retarget one Tripo humanoid donor clip onto the shared Vanguard rig.

Run with Blender 5.2 LTS:

    blender --background --factory-startup \
      --python tools/blender/retarget_vanguard_tripo_donor_v1.py -- \
      <vanguard-source.blend> <donor.glb> <clip-name> \
      <staging-source.blend> <staging.glb> <report.json>

The donor rig, weights, and animation remain diagnostic inputs. This script
copies sampled armature-space rotation deltas onto ``rig.crew.humanoid.v1``,
removes donor data, replaces the matching one-frame interface action, and
exports a reversible staging result. It never overwrites an input or output.
"""

from __future__ import annotations

import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion


ASSET_ID = "character.crew.vanguard.v1"
RIG_ID = "rig.crew.humanoid.v1"
DONOR_TASK_ID = "c889d05a-90fe-4186-85eb-12d4eceafb35"
DONOR_RIG_MODEL = "v1.0 - Good for Humanoid"
EXPECTED_ACTIONS = {
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
}
CLIP_TO_ACTION = {
    "idle": "anim.humanoid.idle_holstered",
    "walk": "anim.humanoid.locomotion_holstered",
    "run": "anim.humanoid.locomotion_armed",
    "shoot": "anim.humanoid.fire_recoil",
    "fall": "anim.humanoid.down",
    "turn": "anim.humanoid.recovery",
    "standing_relax": "anim.humanoid.dialogue_idle",
    "wait": "anim.humanoid.dialogue_listen",
}
LOOPING_CLIPS = {"idle", "walk", "run", "standing_relax", "wait"}
BONE_MAP = {
    "Pelvis": "pelvis",
    "Waist": "spine_01",
    "Spine01": "spine_02",
    "Spine02": "spine_03",
    "NeckTwist02": "neck_01",
    "Head": "head",
    "L_Clavicle": "clavicle_l",
    "L_Upperarm": "upperarm_l",
    "L_UpperarmTwist01": "upperarm_twist_l",
    "L_Forearm": "lowerarm_l",
    "L_ForearmTwist01": "lowerarm_twist_l",
    "L_Hand": "hand_l",
    "R_Clavicle": "clavicle_r",
    "R_Upperarm": "upperarm_r",
    "R_UpperarmTwist01": "upperarm_twist_r",
    "R_Forearm": "lowerarm_r",
    "R_ForearmTwist01": "lowerarm_twist_r",
    "R_Hand": "hand_r",
    "L_Thigh": "thigh_l",
    "L_Calf": "calf_l",
    "L_Foot": "foot_l",
    "L_ToeBase": "toe_l",
    "R_Thigh": "thigh_r",
    "R_Calf": "calf_r",
    "R_Foot": "foot_r",
    "R_ToeBase": "toe_r",
}


def parse_paths() -> tuple[Path, Path, str, Path, Path, Path]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError(
            "Expected -- <source.blend> <donor.glb> <clip> "
            "<output.blend> <output.glb> <report.json>"
        ) from exc
    values = sys.argv[separator + 1 :]
    if len(values) != 6:
        raise RuntimeError("Expected six arguments after --")
    source, donor, clip, output_blend, output_glb, report = values
    paths = tuple(Path(value).resolve() for value in (
        source,
        donor,
        output_blend,
        output_glb,
        report,
    ))
    source_path, donor_path, blend_path, glb_path, report_path = paths
    for path in (source_path, donor_path):
        if not path.is_file():
            raise FileNotFoundError(path)
    if clip not in CLIP_TO_ACTION:
        raise RuntimeError(
            f"Unsupported clip {clip!r}; expected one of {sorted(CLIP_TO_ACTION)}"
        )
    collisions = [
        path for path in (blend_path, glb_path, report_path) if path.exists()
    ]
    if collisions:
        raise FileExistsError(
            "Refusing to overwrite: " + ", ".join(str(path) for path in collisions)
        )
    for path in (blend_path, glb_path, report_path):
        path.parent.mkdir(parents=True, exist_ok=True)
    return (
        source_path,
        donor_path,
        clip,
        blend_path,
        glb_path,
        report_path,
    )


def reset_pose(armature: bpy.types.Object) -> None:
    for pose_bone in armature.pose.bones:
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        pose_bone.scale = (1.0, 1.0, 1.0)


def rotation_delta(
    rest_matrix: Matrix,
    pose_matrix: Matrix,
) -> Quaternion:
    rest = rest_matrix.to_quaternion().normalized()
    pose = pose_matrix.to_quaternion().normalized()
    return (pose @ rest.inverted()).normalized()


def assign_armature_rotation(
    pose_bone: bpy.types.PoseBone,
    rotation: Quaternion,
) -> None:
    current = pose_bone.matrix.copy()
    desired = Matrix.Translation(current.translation) @ rotation.to_matrix().to_4x4()
    pose_bone.matrix = desired


def remove_zero_user_data() -> None:
    datablock_groups = (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.textures,
        bpy.data.cameras,
        bpy.data.lights,
    )
    for group in datablock_groups:
        for datablock in list(group):
            if datablock.users == 0:
                group.remove(datablock)


def select_donor_action(
    actions: list[bpy.types.Action],
    clip_name: str,
) -> bpy.types.Action:
    if len(actions) == 1:
        return actions[0]

    normalized_clip = re.sub(r"[^a-z0-9]+", "", clip_name.lower())
    matches = [
        action
        for action in actions
        if normalized_clip
        in re.sub(r"[^a-z0-9]+", "", action.name.lower())
    ]
    if len(matches) != 1:
        raise RuntimeError(
            f"Could not resolve donor clip {clip_name!r} from actions "
            f"{sorted(action.name for action in actions)}"
        )
    return matches[0]


def export_target(
    target: bpy.types.Object,
    output: Path,
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [
        obj
        for obj in bpy.context.scene.objects
        if obj == target or (
            obj.type == "MESH"
            and any(
                modifier.type == "ARMATURE" and modifier.object == target
                for modifier in obj.modifiers
            )
        )
    ]
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = target
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
        check_existing=False,
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
        export_def_bones=False,
        export_cameras=False,
        export_lights=False,
    )
    if "FINISHED" not in result or not output.is_file():
        raise RuntimeError(f"Staging GLB export failed: {result}")


def fresh_import_report(output: Path, expected_action: str) -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(output))
    if "FINISHED" not in result:
        raise RuntimeError(f"Fresh import failed: {result}")
    armatures = [
        obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"
    ]
    actions = {
        action.name: [round(float(value), 6) for value in action.frame_range]
        for action in bpy.data.actions
    }
    checks = {
        "one_armature": len(armatures) == 1,
        "expected_action_present": expected_action in actions,
        "expected_action_multiframe": (
            expected_action in actions
            and actions[expected_action][1] > actions[expected_action][0]
        ),
        "action_contract_preserved": set(actions) == EXPECTED_ACTIONS,
    }
    if not all(checks.values()):
        raise RuntimeError(f"Fresh-import validation failed: {checks}")
    return {
        "armatures": [obj.name for obj in armatures],
        "bones": sum(len(obj.data.bones) for obj in armatures),
        "actions": actions,
        "checks": checks,
    }


(
    source_path,
    donor_path,
    clip_name,
    staging_blend,
    staging_glb,
    report_path,
) = parse_paths()

bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
scene = bpy.context.scene
if scene.get("asset_id") != ASSET_ID:
    raise RuntimeError("Vanguard source asset ID mismatch")
targets = [obj for obj in scene.objects if obj.type == "ARMATURE"]
if len(targets) != 1:
    raise RuntimeError(f"Expected one target armature, got {len(targets)}")
target = targets[0]
if target.get("rig_profile") != RIG_ID:
    raise RuntimeError("Vanguard target rig profile mismatch")

before_objects = set(bpy.data.objects)
before_actions = set(bpy.data.actions)
result = bpy.ops.import_scene.gltf(filepath=str(donor_path))
if "FINISHED" not in result:
    raise RuntimeError(f"Donor import failed: {result}")
imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
imported_object_names = {obj.name for obj in imported_objects}
donors = [obj for obj in imported_objects if obj.type == "ARMATURE"]
if len(donors) != 1:
    raise RuntimeError(f"Expected one donor armature, got {len(donors)}")
donor = donors[0]
donor_actions = [action for action in bpy.data.actions if action not in before_actions]
if not donor_actions:
    raise RuntimeError("Donor export contains no actions")
donor_action = select_donor_action(donor_actions, clip_name)
donor_action_names = sorted(action.name for action in donor_actions)
donor_action_name = donor_action.name

missing_donor = sorted(set(BONE_MAP) - set(donor.pose.bones.keys()))
missing_target = sorted(set(BONE_MAP.values()) - set(target.pose.bones.keys()))
if missing_donor or missing_target:
    raise RuntimeError(
        f"Retarget map mismatch: donor={missing_donor}, target={missing_target}"
    )

target_action_name = CLIP_TO_ACTION[clip_name]
old_action = bpy.data.actions.get(target_action_name)
if old_action is None:
    raise RuntimeError(f"Missing contract action {target_action_name}")
if tuple(round(value, 6) for value in old_action.frame_range) != (1.0, 1.0):
    raise RuntimeError(
        f"Refusing to replace non-landmark action {target_action_name}"
    )
if target.animation_data and target.animation_data.action == old_action:
    target.animation_data.action = None
bpy.data.actions.remove(old_action)

action = bpy.data.actions.new(target_action_name)
action.use_fake_user = True
action["contract"] = "shared_humanoid_retarget_proof"
action["binding_status"] = "provisional_retarget_proof"
action["duration_status"] = "provider_donor_sampled_at_30_fps"
action["root_motion"] = "none"
action["loop"] = clip_name in LOOPING_CLIPS
action["donor_provider"] = "Tripo Studio"
action["donor_task_id"] = DONOR_TASK_ID
action["donor_rig_model"] = DONOR_RIG_MODEL
action["donor_clip"] = clip_name
target.animation_data_create()
target.animation_data.action = action

scene.render.fps = 30
scene.render.fps_base = 1.0
start = int(math.floor(donor_action.frame_range[0]))
end = int(math.ceil(donor_action.frame_range[1]))
scene.frame_start = start
scene.frame_end = end
motion_degrees = {target_name: 0.0 for target_name in BONE_MAP.values()}

for frame in range(start, end + 1):
    scene.frame_set(frame)
    reset_pose(target)
    bpy.context.view_layer.update()
    for donor_name, target_name in BONE_MAP.items():
        donor_pose = donor.pose.bones[donor_name]
        donor_rest = donor.data.bones[donor_name]
        target_pose = target.pose.bones[target_name]
        target_rest = target.data.bones[target_name]
        delta = rotation_delta(donor_rest.matrix_local, donor_pose.matrix)
        desired = (
            delta @ target_rest.matrix_local.to_quaternion().normalized()
        ).normalized()
        assign_armature_rotation(target_pose, desired)
        bpy.context.view_layer.update()
        delta_angle = min(delta.angle, (2.0 * math.pi) - delta.angle)
        motion_degrees[target_name] = max(
            motion_degrees[target_name],
            math.degrees(delta_angle),
        )
        target_pose.rotation_mode = "QUATERNION"
        target_pose.keyframe_insert(
            data_path="rotation_quaternion",
            frame=frame,
            group=target_name,
        )

target["retarget_proof"] = True
target["retarget_proof_clip"] = clip_name
target["retarget_proof_action"] = target_action_name
target["retarget_proof_source"] = "Tripo Studio donor"
target["retarget_proof_task_id"] = DONOR_TASK_ID
target["root_motion"] = "none"
scene["retarget_proof_status"] = "provisional"
scene["retarget_proof_clip"] = clip_name
scene["retarget_proof_action"] = target_action_name

for obj in imported_objects:
    if obj.name in bpy.data.objects:
        bpy.data.objects.remove(obj, do_unlink=True)
for imported_action in donor_actions:
    if imported_action.name in bpy.data.actions and imported_action != action:
        bpy.data.actions.remove(imported_action)
remove_zero_user_data()

target.animation_data.action = None
reset_pose(target)
scene.frame_set(start)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(staging_blend), check_existing=False)
export_target(target, staging_glb)

action_names = set(bpy.data.actions.keys())
source_checks = {
    "action_contract_preserved": action_names == EXPECTED_ACTIONS,
    "target_action_multiframe": action.frame_range[1] > action.frame_range[0],
    "root_motion_absent": target.pose.bones["root"].location.length <= 1.0e-8,
    "mapped_motion_present": max(motion_degrees.values()) > 0.1,
    "donor_objects_removed": not any(
        name in bpy.data.objects for name in imported_object_names
    ),
}
if not all(source_checks.values()):
    raise RuntimeError(f"Staging source validation failed: {source_checks}")

source_report = {
    "source": str(source_path),
    "donor": str(donor_path),
    "donor_bytes": donor_path.stat().st_size,
    "donor_action": donor_action_name,
    "donor_actions_available": donor_action_names,
    "donor_frame_range": [start, end],
    "donor_rig_model": DONOR_RIG_MODEL,
    "donor_task_id": DONOR_TASK_ID,
    "clip": clip_name,
    "target_action": target_action_name,
    "target_frame_range": [
        round(float(action.frame_range[0]), 6),
        round(float(action.frame_range[1]), 6),
    ],
    "mapped_bones": BONE_MAP,
    "maximum_rotation_delta_degrees": {
        name: round(value, 6)
        for name, value in sorted(motion_degrees.items())
    },
    "checks": source_checks,
}
fresh_report = fresh_import_report(staging_glb, target_action_name)
report = {
    "generated_utc": datetime.now(timezone.utc).isoformat(),
    "blender_version": bpy.app.version_string,
    "status": "provisional donor retarget staging proof passed",
    "asset_id": ASSET_ID,
    "rig_profile": RIG_ID,
    "staging_blend": str(staging_blend),
    "staging_blend_bytes": staging_blend.stat().st_size,
    "staging_glb": str(staging_glb),
    "staging_glb_bytes": staging_glb.stat().st_size,
    "source_validation": source_report,
    "fresh_import_validation": fresh_report,
}
report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("SPACE_ADVENTURE_RESULT=" + json.dumps(report, sort_keys=True))
