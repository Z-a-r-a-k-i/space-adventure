"""Fit the retained shotgun and Protector for local, assembled combat review.

Blender 5.2 --background --python tools/blender/build_party_presentation.py
Rebuilds stay in ignored LocalBaseline for review before publication.
The recovered cleanup source retains the original candidate's geometry.
"""
from pathlib import Path
import sys
import math
import bpy
from mathutils import Matrix, Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
from repair_solo_presentation import (smooth, pose_mix, world_bone,
    solve_arm, sample_pose, apply_pose, write_action, normalize_carbine_axis)

STAGING = ROOT / 'game/Assets/LocalBaseline/party'
STAGING.mkdir(parents=True, exist_ok=True)


def export(path, animated=False):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True,
        export_animations=animated, export_animation_mode='ACTIONS', export_merge_animation='ACTION',
        export_skins=animated, export_def_bones=True, export_yup=True, export_apply=False,
        export_extras=True, export_materials='EXPORT', export_image_format='AUTO',
        export_optimize_animation_size=True, export_force_sampling=True,
        export_cameras=False, export_lights=False)


def main():
    weapon_source = ROOT / 'art/source/weapon.crew.protector_shotgun.v1/protector-shotgun-v1.blend'
    recovered = weapon_source.with_suffix('.blend1')
    bpy.ops.wm.open_mainfile(filepath=str(weapon_source if weapon_source.exists() else recovered))
    weapon_root = bpy.data.objects['weapon.crew.protector_shotgun.v1']
    mesh = bpy.data.objects['weapon.protector_shotgun.body']
    markers = [bpy.data.objects[n] for n in ('socket.grip.primary', 'socket.grip.support', 'socket.attack.muzzle.primary')]
    normalize_carbine_axis(weapon_root, mesh, markers)
    support = markers[1].location.copy()
    assert 3500 <= sum(len(p.vertices)-2 for p in mesh.data.polygons) <= 12000
    assert len(mesh.data.materials) <= 4 and markers[2].location.y > .5
    weapon_root['review_status'] = 'See the asset production record for acceptance'
    bpy.ops.wm.save_as_mainfile(filepath=str(weapon_source), compress=True)
    export(STAGING / 'shotgun.glb')

    # The accepted Vanguard pose supplies wrist orientation only. Protector's
    # own anatomy, leg motion, skin weights and rest skeleton remain intact.
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'art/source/character.crew.vanguard.v1/vanguard-v1.blend'))
    donor = bpy.data.objects['VanguardRig']
    sample_pose(donor, bpy.data.actions['anim.humanoid.idle_armed'], 1)
    aim_rotation = Quaternion((0,0,1), math.pi)
    rotations = {side: aim_rotation.inverted() @ world_bone(donor, side+'Hand').to_quaternion()
                 for side in ('Right','Left')}

    source = ROOT / 'art/source/character.crew.protector.v1/protector-v1.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig = bpy.data.objects['ProtectorRig']
    idle = bpy.data.actions['anim.humanoid.idle_holstered']
    walk = bpy.data.actions['anim.humanoid.locomotion_holstered']
    neutral = sample_pose(rig, idle, 1)
    offsets = {}
    for side in ('Right','Left'):
        hand = world_bone(rig, side+'Hand')
        index = world_bone(rig, side+'HandIndex1')
        offsets[side] = hand.to_quaternion().inverted() @ ((index.translation-hand.translation)*.65)
    aim_position = Vector((-.10,-.13,1.39))
    holster_position = Vector((-.27,.25,1.16))
    holster_rotation = Quaternion((1,0,0), math.pi/2)

    def snapshot():
        return {b.name:b.matrix_basis.copy() for b in rig.pose.bones}

    def fit(position, rotation, right_weight=1, left_weight=1):
        initial = snapshot()
        for side, grip, weight, pole in (
            ('Right', position, right_weight, Vector((-.72,.02,1.22))),
            ('Left', position+rotation@support, left_weight, Vector((.65,-.25,1.18)))):
            if weight <= 0: continue
            orientation = rotation @ rotations[side]
            solve_arm(rig, side, grip-orientation@offsets[side], orientation, pole)
            for suffix in ('Arm','ForeArm','Hand'):
                name = 'mixamorig:'+side+suffix
                rig.pose.bones[name].matrix_basis = pose_mix(initial[name], rig.pose.bones[name].matrix_basis, weight)
            bpy.context.view_layer.update()

    fitted = {}
    for name, action in (('idle_armed',idle),('locomotion_armed',walk)):
        frames=[]
        for frame in range(math.floor(action.frame_range[0]),math.ceil(action.frame_range[1])+1):
            pose = sample_pose(rig,action,frame)
            rig.animation_data.action=None
            apply_pose(rig,pose)
            fit(aim_position,aim_rotation)
            frames.append(snapshot())
        fitted[name]=frames
    rig.animation_data.action=None
    draw=[]
    keys=[(.25,holster_position,holster_rotation),
          (.5,Vector((-.51,-.03,1.44)),holster_rotation.slerp(aim_rotation,.45)),
          (.75,Vector((-.29,-.18,1.43)),aim_rotation),(1,aim_position,aim_rotation)]
    for frame in range(25):
        t=frame/24
        stance=smooth((t-.55)/.45)
        apply_pose(rig,{n:pose_mix(m,fitted['idle_armed'][0][n],stance) for n,m in neutral.items()})
        position,rotation=holster_position,holster_rotation
        for a,b in zip(keys,keys[1:]):
            if t>a[0]:
                amount=smooth((t-a[0])/(b[0]-a[0]))
                position=a[1].lerp(b[1],amount)
                rotation=a[2].slerp(b[2],amount)
        fit(position,rotation,smooth(t/.25),smooth((t-.6)/.3))
        draw.append(snapshot())
    apply_pose(rig,draw[-1])
    bpy.data.objects['socket.weapon.hand_primary'].matrix_world=Matrix.LocRotScale(aim_position,aim_rotation,rig.matrix_world.to_scale())
    bpy.context.view_layer.update()
    apply_pose(rig,neutral)
    bpy.data.objects['socket.weapon.holster_primary'].matrix_world=Matrix.LocRotScale(holster_position,holster_rotation,rig.matrix_world.to_scale())
    bpy.context.view_layer.update()
    for name,frames in fitted.items():
        write_action(rig,'anim.humanoid.'+name,frames,{'source_provider':'Accepted Mixamo base + fitted Blender arm layer','loop_candidate':True})
    write_action(rig,'anim.humanoid.draw_primary',draw,{'duration_seconds':.8,'transfer_progress':.25})
    write_action(rig,'anim.humanoid.holster_primary',list(reversed(draw)),{'duration_seconds':1.8,'transfer_progress':.75})
    write_action(rig,'anim.humanoid.attack_primary',[fitted['idle_armed'][0]]*2,{'recoil_owner':'Godot presentation clock'})
    from repair_crew_death import repair_death
    repair_death(rig, duration=2.3)
    rig['right_palm_offset_m']=list(offsets['Right'])
    rig['left_palm_offset_m']=list(offsets['Left'])
    rig['party_handling_revision']=1
    rig.animation_data.action=idle
    bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    export(STAGING / 'protector.glb',True)
    print('PARTY_PRESENTATION_STAGED='+str(STAGING))

if __name__=='__main__': main()
