"""Bake grounded crew falls from the retained Rifle Death donor; preserve rigs and skin."""
from pathlib import Path
import json
import math
import sys
import argparse
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
from repair_solo_presentation import (sample_pose, apply_pose, pose_mix, smooth, write_action,
                                      world_bone, rotate_bone_toward)

DONOR = ROOT / 'art/generated/character.crew.vanguard.v1/prod-tripo-v31bq-20260803-02/raw/mixamo/rifle-death-no-skin.fbx'


def settle_legs(rig, weight):
    """Let the boots settle onto the deck after the torso makes contact."""
    if not weight:
        return
    for side in ('Left', 'Right'):
        upper = world_bone(rig, side+'UpLeg').translation
        knee = world_bone(rig, side+'Leg').translation
        foot = world_bone(rig, side+'Foot')
        target = foot.translation.copy()
        target.z += (.13 - target.z) * weight
        a, b = (knee-upper).length, (foot.translation-knee).length
        direction = (target-upper).normalized()
        distance = min((target-upper).length, a+b-.00001)
        along = (a*a-b*b+distance*distance) / (2*distance)
        height = math.sqrt(max(0, a*a-along*along))
        bend = knee-upper-direction*(knee-upper).dot(direction)
        joint = upper + direction*along + bend.normalized()*height
        rotate_bone_toward(rig, side+'UpLeg', side+'Leg', joint)
        rotate_bone_toward(rig, side+'Leg', side+'Foot', target)
        matrix = world_bone(rig, side+'Foot')
        rig.pose.bones['mixamorig:'+side+'Foot'].matrix = rig.matrix_world.inverted() @ Matrix.LocRotScale(
            matrix.translation, foot.to_quaternion(), matrix.to_scale())
        bpy.context.view_layer.update()


def repair_death(rig, duration=2.1):
    """Map local deltas onto the accepted rest rig and retain the falling root rotation.

    Reading PoseBone.matrix immediately after setting matrix_basis returned a
    stale evaluated rotation in the old bake. Transfer the root's world rotation
    delta from its first frame onto the fitted stance, then settle the torso.
    Mesh grounding is evaluated on every frame, including the final body contact.
    """
    initial = sample_pose(rig, bpy.data.actions['anim.humanoid.idle_armed'], 0)
    rig.animation_data.action = None
    apply_pose(rig, initial)
    root_name = 'mixamorig:Hips'
    root_origin = rig.pose.bones[root_name].matrix.translation.copy()
    initial_root = rig.matrix_world @ rig.pose.bones[root_name].matrix
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH'
              and any(m.type == 'ARMATURE' and m.object == rig for m in o.modifiers)]
    before = set(bpy.data.objects)
    bpy.ops.wm.fbx_import(filepath=str(DONOR), global_scale=rig.scale.x / .01)
    imported = list(set(bpy.data.objects) - before)
    donor = next(o for o in imported if o.type == 'ARMATURE')
    action = donor.animation_data.action
    start, end = action.frame_range
    names = sorted((b.name for b in rig.pose.bones), key=lambda n: len(rig.data.bones[n].parent_recursive))
    assert all(n in donor.pose.bones for n in names), 'Death donor lacks a required bone'
    bpy.context.scene.frame_set(int(start)); bpy.context.view_layer.update()
    donor_origin = donor.pose.bones[root_name].matrix.translation.copy()
    donor_rotation = (donor.matrix_world @ donor.pose.bones[root_name].matrix).to_quaternion()
    frames = []
    floor_offsets = []
    count = round(duration * 30)
    for index in range(count + 1):
        frame = start + (end - start) * index / count
        bpy.context.scene.frame_set(math.floor(frame), subframe=frame % 1)
        bpy.context.view_layer.update()
        for name in names:
            source = donor.pose.bones[name]
            delta = source.matrix_basis.copy()
            if name == root_name:
                position = rig.matrix_world @ (root_origin + source.matrix.translation - donor_origin)
                rotation = (donor.matrix_world @ source.matrix).to_quaternion() @ donor_rotation.inverted() @ initial_root.to_quaternion()
                matrix = rig.matrix_world.inverted() @ Matrix.LocRotScale(position, rotation, rig.matrix_world.to_scale())
                rig.pose.bones[name].matrix_basis = rig.data.bones[name].matrix_local.inverted() @ matrix
            else:
                rig.pose.bones[name].matrix_basis = delta
        # The authored armed stance eases into the donor; no snapping arms at death.
        weight = smooth(index / (30 * .16))
        for name in names:
            rig.pose.bones[name].matrix_basis = pose_mix(initial[name], rig.pose.bones[name].matrix_basis, weight)
        bpy.context.view_layer.update()
        # The different rest torsos need a small late roll to rest on the deck,
        # rather than retaining the donor's upright bind-pose offset.
        settle = smooth((index / count - .55) / .3)
        if settle:
            hips = rig.matrix_world @ rig.pose.bones[root_name].matrix
            head = rig.matrix_world @ rig.pose.bones['mixamorig:Head'].matrix
            along = head.translation - hips.translation
            horizontal = Vector((along.x, along.y, 0))
            roll = along.normalized().rotation_difference(horizontal.normalized())
            rotation = hips.to_quaternion().slerp(roll @ hips.to_quaternion(), settle)
            rig.pose.bones[root_name].matrix = rig.matrix_world.inverted() @ Matrix.LocRotScale(hips.translation, rotation, hips.to_scale())
            bpy.context.view_layer.update()
        minimum = mesh_minimum(meshes)
        correction = max(0, .012 - minimum)
        if correction:
            matrix = rig.matrix_world @ rig.pose.bones[root_name].matrix
            matrix.translation.z += correction
            rig.pose.bones[root_name].matrix = rig.matrix_world.inverted() @ matrix
            bpy.context.view_layer.update()
        settle_legs(rig, smooth((index / count - .72) / .28))
        foot_correction = max(0, .012 - mesh_minimum(meshes))
        if foot_correction:
            matrix = rig.matrix_world @ rig.pose.bones[root_name].matrix
            matrix.translation.z += foot_correction
            rig.pose.bones[root_name].matrix = rig.matrix_world.inverted() @ matrix
            bpy.context.view_layer.update()
        correction += foot_correction
        floor_offsets.append(correction)
        frames.append({bone.name:bone.matrix_basis.copy() for bone in rig.pose.bones})
    for obj in imported: bpy.data.objects.remove(obj, do_unlink=True)
    if action.users == 0: bpy.data.actions.remove(action)
    baked = write_action(rig, 'anim.humanoid.down', frames, {
        'source_provider':'Mixamo', 'source_clip':'Rifle Death',
        'retarget_method':'local deltas with explicit root rotation and evaluated mesh ground contact',
        'duration_seconds':duration, 'loop_candidate':False,
    })
    minimum = float('inf')
    for frame in range(math.floor(baked.frame_range[0]), math.ceil(baked.frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame); bpy.context.view_layer.update()
        minimum = min(minimum, mesh_minimum(meshes))
    hips = (rig.matrix_world @ rig.pose.bones[root_name].matrix).translation
    head = (rig.matrix_world @ rig.pose.bones['mixamorig:Head'].matrix).translation
    assert minimum >= -.005, f'Death penetrates floor: {minimum}'
    assert head.z < .5 and hips.z < .45, f'Death must finish lying down: {head}, {hips}'
    return {'duration_seconds':duration, 'minimum_mesh_height_m':minimum,
            'final_head_height_m':head.z, 'final_hips_height_m':hips.z,
            'maximum_ground_correction_m':max(floor_offsets)}


def mesh_minimum(meshes):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    minimum = float('inf')
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        matrix = evaluated.matrix_world
        minimum = min(minimum, min((matrix @ vertex.co).z for vertex in mesh.vertices))
        evaluated.to_mesh_clear()
    return minimum


def verify_export(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    bpy.ops.import_scene.gltf(filepath=str(path))
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
    action = bpy.data.actions['anim.humanoid.down']
    rig.animation_data.action = action
    # glTF import also creates excluded Icosphere bone-display helpers.
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH' and obj.visible_get()]
    minimum = float('inf')
    for frame in range(math.floor(action.frame_range[0]), math.ceil(action.frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame); bpy.context.view_layer.update()
        minimum = min(minimum, mesh_minimum(meshes))
    heights = {name:(rig.matrix_world @ rig.pose.bones['mixamorig:'+name].matrix).translation.z for name in ['Head','Hips','LeftFoot','RightFoot']}
    assert minimum >= -.005 and heights['Head'] < .55 and heights['Hips'] < .5, (path, minimum, heights)
    assert max(heights['LeftFoot'], heights['RightFoot']) < .3, f'Raised boots at rest: {heights}'
    return {'minimum_mesh_height_m':minimum, 'final_bone_heights_m':heights}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    report = []
    for actor, rig_name, duration in [('vanguard', 'VanguardRig', 2.1), ('protector', 'ProtectorRig', 2.3)]:
        source = ROOT / f'art/source/character.crew.{actor}.v1/{actor}-v1.blend'
        output = ROOT / f'game/Assets/Published/character.crew.{actor}.v1.glb'
        if args.verify_only:
            report.append({'actor':actor, 'exact_glb':verify_export(output)})
            continue
        bpy.ops.wm.open_mainfile(filepath=str(source))
        rig = bpy.data.objects[rig_name]
        result = repair_death(rig, duration)
        rig.animation_data.action = bpy.data.actions['anim.humanoid.idle_holstered']
        bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
        bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
        bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active = rig
        staged = ROOT / f'artifacts/party-work/{actor}-death-repair.glb'
        staged.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.export_scene.gltf(filepath=str(staged), export_format='GLB', use_selection=True,
            export_animations=True, export_animation_mode='ACTIONS', export_merge_animation='ACTION',
            export_skins=True, export_def_bones=True, export_yup=True, export_apply=False,
            export_extras=True, export_materials='EXPORT', export_image_format='AUTO',
            export_optimize_animation_size=True, export_force_sampling=True)
        exact = verify_export(staged)
        staged.replace(output)
        report.append({'actor':actor, **result, 'exact_glb':exact})
    destination = ROOT / ('artifacts/party-work/death-export-check.json' if args.verify_only else 'artifacts/party-work/death-repair.json')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, indent=2))
    print('DEATH_REPAIR '+json.dumps(report))


if __name__ == '__main__': main()
