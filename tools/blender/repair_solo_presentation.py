"""Repair the approved solo presentation sources without regenerating models.

Blender 5.2: blender --background --python tools/blender/repair_solo_presentation.py
Original provider inputs stay untouched. Published skeleton normalization is
preserved; this script repairs the carbine axis, fitted arm layer and handling path.
"""
from pathlib import Path
import json
import math
import bpy
from mathutils import Matrix, Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]


def smooth(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3 - 2 * value)


def pose_mix(a, b, weight):
    al, ar, asc = a.decompose()
    bl, br, bsc = b.decompose()
    return Matrix.LocRotScale(al.lerp(bl, weight), ar.slerp(br, weight), asc.lerp(bsc, weight))


def world_bone(rig, name):
    return rig.matrix_world @ rig.pose.bones['mixamorig:' + name].matrix


def rotate_bone_toward(rig, name, child_name, target):
    bone = rig.pose.bones['mixamorig:' + name]
    matrix = world_bone(rig, name)
    old_direction = world_bone(rig, child_name).translation - matrix.translation
    desired_direction = target - matrix.translation
    rotation = old_direction.normalized().rotation_difference(desired_direction.normalized())
    bone.matrix = rig.matrix_world.inverted() @ Matrix.LocRotScale(
        matrix.translation, rotation @ matrix.to_quaternion(), matrix.to_scale())
    bpy.context.view_layer.update()


def solve_arm(rig, side, wrist, hand_rotation, pole):
    upper = world_bone(rig, side + 'Arm').translation
    middle = world_bone(rig, side + 'ForeArm').translation
    end = world_bone(rig, side + 'Hand').translation
    a, b = (middle-upper).length, (end-middle).length
    direction = (wrist-upper).normalized()
    distance = min((wrist-upper).length, a+b-0.00001)
    along = (a*a-b*b+distance*distance) / (2*distance)
    height = math.sqrt(max(0, a*a-along*along))
    bend = pole-upper-direction*(pole-upper).dot(direction)
    elbow = upper + direction*along + bend.normalized()*height
    rotate_bone_toward(rig, side+'Arm', side+'ForeArm', elbow)
    rotate_bone_toward(rig, side+'ForeArm', side+'Hand', wrist)
    matrix = world_bone(rig, side+'Hand')
    rig.pose.bones['mixamorig:'+side+'Hand'].matrix = rig.matrix_world.inverted() @ Matrix.LocRotScale(
        matrix.translation, hand_rotation, matrix.to_scale())
    bpy.context.view_layer.update()


def sample_pose(rig, action, frame):
    rig.animation_data.action = action
    bpy.context.scene.frame_set(math.floor(frame), subframe=frame % 1)
    bpy.context.view_layer.update()
    return {bone.name:bone.matrix_basis.copy() for bone in rig.pose.bones}


def apply_pose(rig, pose):
    for name, matrix in pose.items():
        rig.pose.bones[name].matrix_basis = matrix
    bpy.context.view_layer.update()


def write_action(rig, name, frames, metadata):
    old = bpy.data.actions.get(name)
    rig.animation_data.action = None
    if old:
        bpy.data.actions.remove(old)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    for key, value in metadata.items():
        action[key] = value
    rig.animation_data.action = action
    for frame, pose in enumerate(frames):
        for bone in rig.pose.bones:
            bone.rotation_mode = 'QUATERNION'
            bone.matrix_basis = pose[bone.name]
            bone.keyframe_insert('location', frame=frame, group=bone.name)
            bone.keyframe_insert('rotation_quaternion', frame=frame, group=bone.name)
            bone.keyframe_insert('scale', frame=frame, group=bone.name)
    return action


def repair_vanguard():
    source = ROOT / 'art/source/character.crew.vanguard.v1/vanguard-v1.blend'
    output = ROOT / 'game/Assets/Published/character.crew.vanguard.v1.glb'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig = bpy.data.objects['VanguardRig']
    if rig.get('solo_handling_revision', 0) < 2:
        idle_action = bpy.data.actions['anim.humanoid.idle_holstered']
        armed_action = bpy.data.actions['anim.humanoid.idle_armed']
        neutral = sample_pose(rig, idle_action, 1)
        aim = sample_pose(rig, armed_action, 1)
        aim_rotation = Quaternion((0,0,1), math.pi)
        holster_rotation = Quaternion((1,0,0), math.pi/2)
        aim_position = Vector((-.12,-.20,1.32))
        holster_position = Vector((-.22,.20,1.10))
        # Palm centres are measured from the accepted hand/index anatomy.
        offsets, rotations = {}, {}
        for side in ['Right', 'Left']:
            hand = world_bone(rig, side+'Hand')
            index = world_bone(rig, side+'HandIndex1')
            offsets[side] = hand.to_quaternion().inverted() @ ((index.translation-hand.translation)*.65)
            rotations[side] = aim_rotation.inverted() @ hand.to_quaternion()
        support = Vector((.000119213, .34444952, -.034864943))

        def fit_hands(position, rotation, right_weight=1.0, left_weight=1.0):
            initial = {bone.name:bone.matrix_basis.copy() for bone in rig.pose.bones}
            for side, grip, weight, pole in [
                ('Right', position, right_weight, Vector((-.65,.0,1.15))),
                ('Left', position+rotation@support, left_weight, Vector((.60,-.22,1.12))),
            ]:
                if weight <= 0:
                    continue
                hand_rotation = rotation @ rotations[side]
                solve_arm(rig, side, grip-hand_rotation@offsets[side], hand_rotation, pole)
                for suffix in ['Arm','ForeArm','Hand']:
                    name = 'mixamorig:'+side+suffix
                    rig.pose.bones[name].matrix_basis = pose_mix(initial[name], rig.pose.bones[name].matrix_basis, weight)
                bpy.context.view_layer.update()

        # Keep authored locomotion and breathing below the arm layer. Fit both
        # hands to the exact 0.82 m carbine on every frame, including loop ends.
        fitted_actions = {}
        for name in ['anim.humanoid.idle_armed','anim.humanoid.locomotion_armed']:
            action = bpy.data.actions[name]
            frames = []
            for frame in range(math.floor(action.frame_range[0]), math.ceil(action.frame_range[1])+1):
                base = sample_pose(rig, action, frame)
                rig.animation_data.action = None
                apply_pose(rig, base)
                fit_hands(aim_position, aim_rotation)
                frames.append({bone.name:bone.matrix_basis.copy() for bone in rig.pose.bones})
            fitted_actions[name] = frames

        draw_frames = []
        keys = [(0.25, holster_position, holster_rotation),
                (0.50, Vector((-.45,-.02,1.38)), holster_rotation.slerp(aim_rotation,.45)),
                (0.75, Vector((-.27,-.16,1.36)), aim_rotation),
                (1.00, aim_position, aim_rotation)]
        rig.animation_data.action = None
        for frame in range(55):
            t = frame/54
            # Finish in the actual armed torso/shoulder stance, so the support
            # wrist stays reachable across the draw-to-idle boundary and retry.
            stance = smooth((t-.55)/.45)
            armed_base = fitted_actions['anim.humanoid.idle_armed'][0]
            apply_pose(rig, {name:pose_mix(matrix, armed_base[name], stance) for name,matrix in neutral.items()})
            position, rotation = holster_position, holster_rotation
            for previous, current in zip(keys, keys[1:]):
                if t > previous[0]:
                    amount = smooth((t-previous[0])/(current[0]-previous[0]))
                    position = previous[1].lerp(current[1], amount)
                    rotation = previous[2].slerp(current[2], amount)
            fit_hands(position, rotation, smooth(t/.25), smooth((t-.60)/.30))
            draw_frames.append({bone.name:bone.matrix_basis.copy() for bone in rig.pose.bones})
        apply_pose(rig, draw_frames[-1])
        rig.animation_data.action = None
        socket = bpy.data.objects['socket.weapon.hand_primary']
        socket.matrix_world = Matrix.LocRotScale(aim_position, aim_rotation, rig.matrix_world.to_scale())
        bpy.context.view_layer.update()
        apply_pose(rig, neutral)
        holster = bpy.data.objects['socket.weapon.holster_primary']
        holster.matrix_world = Matrix.LocRotScale(holster_position, holster_rotation, rig.matrix_world.to_scale())
        bpy.context.view_layer.update()
        for name, frames in fitted_actions.items():
            write_action(rig, name, frames, {'source_provider':'Mixamo + Blender fitted arm layer','loop_candidate':True})
        write_action(rig, 'anim.humanoid.draw_primary', draw_frames,
            {'source_provider':'Blender authored','transfer_progress':.25,'duration_seconds':1.8,'loop_candidate':False})
        write_action(rig, 'anim.humanoid.holster_primary', list(reversed(draw_frames)),
            {'source_provider':'Blender authored','transfer_progress':.75,'duration_seconds':1.8,'loop_candidate':False})
        rig['solo_handling_revision'] = 2
        rig['draw_transfer_progress'] = .25
        rig['holster_transfer_progress'] = .75
        rig['right_palm_offset_m'] = list(offsets['Right'])
        rig['left_palm_offset_m'] = list(offsets['Left'])
    rig.animation_data.action = bpy.data.actions['anim.humanoid.idle_holstered']
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB', use_selection=True,
        export_animations=True, export_animation_mode='ACTIONS', export_merge_animation='ACTION',
        export_skins=True, export_def_bones=True, export_yup=True, export_apply=False,
        export_extras=True, export_materials='EXPORT', export_image_format='AUTO',
        export_optimize_animation_size=True, export_force_sampling=True)
    print('VANGUARD_HANDLING_REPAIR=2')


def normalize_carbine_axis(root, mesh, markers):
    if root.get("solo_axis_revision", 0) >= 1:
        return
    rotation = Matrix.Rotation(math.pi, 4, "Z")
    mesh.data.transform(rotation)
    for marker in markers:
        marker.location = rotation @ marker.location
        # Blender +Y exports to Godot -Z. Marker rotation stays identity so
        # its Godot -Z axis points out through the physical muzzle.
        marker["authoring_local_forward"] = "+Y"
    root["authoring_forward"] = "+Y"
    root["solo_axis_revision"] = 1


def main():
    source = ROOT / "art/source/weapon.crew.vanguard_carbine.v1/vanguard-carbine-v1.blend"
    output = ROOT / "game/Assets/Published/weapon.crew.vanguard_carbine.v1.glb"
    bpy.ops.wm.open_mainfile(filepath=str(source))
    root = bpy.data.objects["weapon.crew.vanguard_carbine.v1"]
    mesh = bpy.data.objects["weapon.vanguard_carbine.body"]
    markers = [bpy.data.objects[name] for name in (
        "socket.grip.primary", "socket.grip.support", "socket.attack.muzzle.primary")]
    assert mesh.type == "MESH" and mesh.data.users == 1
    assert all(marker.parent == root for marker in markers)
    normalize_carbine_axis(root, mesh, markers)
    bpy.context.view_layer.update()
    assert markers[-1].location.y > 0.5, "Muzzle must export along Godot -Z"
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, mesh, *markers]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=True, export_materials="EXPORT", export_vertex_color="MATERIAL",
        export_normals=True, export_cameras=False, export_lights=False,
        export_animations=False, export_skins=False, export_extras=True)
    print("SOLO_ASSET_REPAIR=" + json.dumps({"asset":"weapon.crew.vanguard_carbine.v1",
        "source":str(source),"output":str(output),"axis_revision":1,"length_m":round(mesh.dimensions.y,6)}))
    repair_vanguard()


if __name__ == "__main__":
    main()
