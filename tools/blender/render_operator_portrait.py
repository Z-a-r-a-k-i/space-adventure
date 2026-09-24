"""Render the Medic HUD portrait from the published Operator anatomy."""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/source/character.crew.operator.v1/operator-v1.blend'))
scene=bpy.context.scene;rig=bpy.data.objects['OperatorRig']
rig.animation_data.action=bpy.data.actions['anim.humanoid.idle_holstered'];scene.frame_set(1)
for obj in list(scene.objects):
    if obj.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(obj,do_unlink=True)
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.world=scene.world or bpy.data.worlds.new('PortraitWorld');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.022,.037,.055,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
scene.render.resolution_x=256;scene.render.resolution_y=320;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.exposure=.35
target=Vector((0,-.01,1.51))
def aim(obj):obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.42,-2.8,1.63));camera=bpy.context.object;aim(camera)
camera.data.type='ORTHO';camera.data.ortho_scale=.66;scene.camera=camera
for label,position,power,color,size in [('Key',(-1.4,-2.1,3),270,(1,.91,.8),2),('Fill',(1.8,-.8,2.2),95,(.63,.84,1),1.8),('Rim',(-.7,.7,2.5),330,(.3,1,.7),1)]:
    bpy.ops.object.light_add(type='AREA',location=position);light=bpy.context.object;light.name=label
    light.data.energy=power;light.data.color=color;light.data.shape='DISK';light.data.size=size;aim(light)
destination=ROOT/'game/ui/portraits/operator.png';destination.parent.mkdir(parents=True,exist_ok=True)
scene.render.filepath=str(destination);bpy.ops.render.render(write_still=True)
print('PORTRAIT='+str(destination))
