"""Bind retained Operator geometry to fitted accepted Mixamo anatomy and fit rifles.

Blender 5.2 --background --python tools/blender/build_station_crew_expansion.py
Retained input backups are copied under artifacts/art-expansion/raw.
Use --publish-existing to re-export the committed editable sources directly.
No provider generation, old one-frame rigs, or runtime animation authority.
"""
from pathlib import Path
import argparse, json, math, sys
import bpy, bmesh
from mathutils import Matrix, Vector, Quaternion

ROOT=Path(__file__).resolve().parents[2]
CACHE=ROOT/'artifacts/art-expansion'
sys.path.insert(0,str(Path(__file__).parent))
from repair_solo_presentation import sample_pose,apply_pose,write_action,world_bone,solve_arm,pose_mix,smooth

P='mixamorig:'
IDLE='anim.humanoid.idle_holstered'
WALK='anim.humanoid.locomotion_holstered'

def export(path,animated=False):
    path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        if obj.type not in ('LIGHT','CAMERA'): obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_animations=animated,export_animation_mode='ACTIONS',export_merge_animation='ACTION',
        export_skins=animated,export_def_bones=True,export_yup=True,export_apply=False,
        export_extras=True,export_materials='EXPORT',export_image_format='AUTO',
        export_optimize_animation_size=True,export_force_sampling=True,
        export_cameras=False,export_lights=False)

def normalize_publication_names(asset):
    used=set();visited=set()
    def collect(tree):
        if tree in visited:return
        visited.add(tree)
        for node in tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:used.add(node.image)
            elif node.type=='GROUP' and node.node_tree:collect(node.node_tree)
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        for material in obj.data.materials:
            if not material:continue
            if asset=='character.crew.operator.v1':
                material.name='mat.operator.undersuit.navy' if obj.name=='OperatorDeformingBase' else 'mat.operator.surface.pbr'
            if material.use_nodes:
                collect(material.node_tree)
    # Short embedded image identities keep Godot's extracted GLB-prefixed
    # texture filenames portable. Original provider IDs live in provenance.
    for index,image in enumerate(sorted(used,key=lambda i:i.name)):
        original=image.name.lower()
        role='normal' if 'normal' in original else 'base' if 'base' in original or 'color' in original else 'rough' if 'rough' in original else 'metal' if 'metal' in original else 'orm' if original.endswith('_rm') else f'map-{index+1}'
        image.name=role
        if not image.packed_file:image.pack()
        image.filepath_raw=role+'.png'

def save(asset,leaf,animated=False):
    path=ROOT/'art/source'/asset/leaf
    path.parent.mkdir(parents=True,exist_ok=True)
    normalize_publication_names(asset)
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    export(ROOT/'game/Assets/Published'/f'{asset}.glb',animated)

def fit_handling(rig,one_handed=False):
    idle=bpy.data.actions[IDLE];walk=bpy.data.actions[WALK]
    neutral=sample_pose(rig,idle,1)
    # Right palm faces the separate weapon's local grip; calibrate from source
    # neutral hand frame without assigning another character's bone transforms.
    offsets={}
    for side in ('Right','Left'):
        hand=world_bone(rig,side+'Hand');index=world_bone(rig,side+'HandIndex1')
        offsets[side]=hand.to_quaternion().inverted()@((index.translation-hand.translation)*.65)
    # Use the accepted Vanguard wrist orientation as a semantic aim direction.
    existing_actions=set(bpy.data.actions)
    with bpy.data.libraries.load(str(ROOT/'art/source/character.crew.vanguard.v1/vanguard-v1.blend'),link=False) as (src,dst):
        dst.objects=['VanguardRig'];dst.actions=['anim.humanoid.idle_armed']
    donor=dst.objects[0];bpy.context.collection.objects.link(donor)
    action=dst.actions[0];sample_pose(donor,action,1)
    aim_rotation=Quaternion((0,0,1),math.pi)
    orientations={side:aim_rotation.inverted()@world_bone(donor,side+'Hand').to_quaternion() for side in ('Right','Left')}
    bpy.data.objects.remove(donor,do_unlink=True)
    for imported_action in set(bpy.data.actions)-existing_actions:bpy.data.actions.remove(imported_action)
    neutral=sample_pose(rig,idle,1)
    height=world_bone(rig,'Head').translation.z
    aim_position=Vector((-.18,-.35,height-.28)) if one_handed else Vector((-.13,-.20,height-.29))
    holster_position=Vector((-.285,.055,.84)) if one_handed else Vector((-.25,.23,1.15))
    holster_rotation=Quaternion((1,0,0),-math.pi/2 if one_handed else math.pi/2)
    support=Vector((.000119213,.34444952,-.034864943))
    def snapshot(): return {b.name:b.matrix_basis.copy() for b in rig.pose.bones}
    def fit(position,rotation,rw=1,lw=1):
        initial=snapshot()
        for side,grip,weight,pole in [('Right',position,rw,Vector((-.72,.02,1.15))),('Left',position+rotation@support,0 if one_handed else lw,Vector((.65,-.3,1.15)))]:
            if weight<=0:continue
            orientation=rotation@orientations[side]
            solve_arm(rig,side,grip-orientation@offsets[side],orientation,pole)
            for suffix in ('Arm','ForeArm','Hand'):
                name=P+side+suffix
                rig.pose.bones[name].matrix_basis=pose_mix(initial[name],rig.pose.bones[name].matrix_basis,weight)
            bpy.context.view_layer.update()
    fitted={}
    for name,base in [('idle_armed',idle),('locomotion_armed',walk)]:
        frames=[]
        for frame in range(math.floor(base.frame_range[0]),math.ceil(base.frame_range[1])+1):
            pose=sample_pose(rig,base,frame);rig.animation_data.action=None;apply_pose(rig,pose)
            fit(aim_position,aim_rotation);frames.append(snapshot())
        fitted[name]=frames
    rig.animation_data.action=None;draw=[]
    keys=[(.25,holster_position,holster_rotation),(.5,Vector((-.43,-.04,1.16)),holster_rotation.slerp(aim_rotation,.45)),(.75,aim_position+Vector((-.07,.05,-.04)),aim_rotation),(1,aim_position,aim_rotation)]
    for frame in range(25):
        t=frame/24;stance=smooth((t-.55)/.45)
        apply_pose(rig,{n:pose_mix(m,fitted['idle_armed'][0][n],stance) for n,m in neutral.items()})
        position,rotation=holster_position,holster_rotation
        for a,b in zip(keys,keys[1:]):
            if t>a[0]:
                amount=smooth((t-a[0])/(b[0]-a[0]));position=a[1].lerp(b[1],amount);rotation=a[2].slerp(b[2],amount)
        fit(position,rotation,smooth(t/.25),smooth((t-.6)/.3));draw.append(snapshot())
    for socket,bone in [('socket.weapon.hand_primary','RightHand'),('socket.weapon.holster_primary','Hips')]:
        obj=bpy.data.objects.get(socket)
        if obj is None:
            obj=bpy.data.objects.new(socket,None);bpy.context.collection.objects.link(obj)
        obj.parent=rig;obj.parent_type='BONE';obj.parent_bone=P+bone
    apply_pose(rig,draw[-1]);bpy.data.objects['socket.weapon.hand_primary'].matrix_world=Matrix.LocRotScale(aim_position,aim_rotation,rig.matrix_world.to_scale())
    bpy.context.view_layer.update();apply_pose(rig,neutral)
    bpy.data.objects['socket.weapon.holster_primary'].matrix_world=Matrix.LocRotScale(holster_position,holster_rotation,rig.matrix_world.to_scale())
    for name,frames in fitted.items():write_action(rig,'anim.humanoid.'+name,frames,{'source_provider':'accepted Mixamo locomotion with fitted Blender weapon handling','loop_candidate':True})
    write_action(rig,'anim.humanoid.draw_primary',draw,{'duration_seconds':.8,'transfer_progress':.25})
    write_action(rig,'anim.humanoid.holster_primary',list(reversed(draw)),{'duration_seconds':.8,'transfer_progress':.75})
    write_action(rig,'anim.humanoid.attack_primary',[fitted['idle_armed'][0]]*2,{'recoil_owner':'Godot presentation clock'})
    rig['right_palm_offset_m']=list(offsets['Right']);rig['left_palm_offset_m']=list(offsets['Left'])
    rig['one_handed']=one_handed
    rig.animation_data.action=idle;bpy.context.scene.frame_set(1)

def ground_actions(rig):
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
    root=rig.pose.bones[P+'Hips'];corrections={}
    for name in (IDLE,WALK,'anim.humanoid.walk_holstered','anim.humanoid.down'):
        action=bpy.data.actions[name];frames=[];offsets=[]
        for frame in range(math.floor(action.frame_range[0]),math.ceil(action.frame_range[1])+1):
            pose=sample_pose(rig,action,frame);rig.animation_data.action=None;apply_pose(rig,pose)
            dg=bpy.context.evaluated_depsgraph_get();minimum=float('inf')
            for obj in meshes:
                evaluated=obj.evaluated_get(dg);mesh=evaluated.to_mesh()
                minimum=min(minimum,min((evaluated.matrix_world@v.co).z for v in mesh.vertices));evaluated.to_mesh_clear()
            correction=.006-minimum
            matrix=rig.matrix_world@root.matrix;matrix.translation.z+=correction;root.matrix=rig.matrix_world.inverted()@matrix
            bpy.context.view_layer.update();frames.append({b.name:b.matrix_basis.copy() for b in rig.pose.bones});offsets.append(correction)
        write_action(rig,name,frames,{'source_provider':'accepted Mixamo clip with evaluated per-frame floor contact','loop_candidate':name!='anim.humanoid.down'})
        corrections[name]=[min(offsets),max(offsets)]
    rig['grounding_corrections_json']=json.dumps(corrections)

def make_operator():
    # Retain the accepted source hierarchy and actual sampled animation channels.
    donor_source=ROOT/'art/source/character.crew.vanguard.v1/vanguard-v1.blend'
    bpy.ops.wm.open_mainfile(filepath=str(donor_source));donor=bpy.data.objects['VanguardRig']
    sample_pose(donor,bpy.data.actions[IDLE],1)
    donor_world=donor.matrix_world.copy();donor_inverse=donor_world.inverted()
    hierarchy=[(b.name,b.parent.name if b.parent else None,b.matrix_local.copy(),b.length) for b in donor.data.bones]
    basis={b.name:b.matrix_basis.copy() for b in donor.pose.bones}
    poses={b.name:b.matrix.copy() for b in donor.pose.bones}
    animations={}
    for name in (IDLE,WALK,'anim.humanoid.walk_holstered','anim.humanoid.down'):
        action=bpy.data.actions[name]
        animations[name]=[sample_pose(donor,action,frame) for frame in range(math.floor(action.frame_range[0]),math.ceil(action.frame_range[1])+1)]
    bpy.ops.wm.open_mainfile(filepath=str(CACHE/'raw/operator.blend'))
    old=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');old.animation_data_clear();old.data.pose_position='REST'
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for mesh in meshes:
        bm=bmesh.new();bm.from_mesh(mesh.data)
        if mesh.name=='OperatorDeformingBase':
            # The old hidden toe scaffold projected beyond the intact boots.
            # Retain the joint underlayer, remove that named visible defect.
            bmesh.ops.delete(bm,geom=[v for v in bm.verts if (mesh.matrix_world@v.co).z<.32],context='VERTS')
        bm.to_mesh(mesh.data);bm.free();mesh.data.update()
    old_world={o.name:o.matrix_world.copy() for o in meshes}
    for o in meshes:
        mw=o.matrix_world.copy();o.parent=None;o.matrix_world=mw
        for m in list(o.modifiers):
            if m.type=='ARMATURE':o.modifiers.remove(m)
    bpy.data.objects.remove(old,do_unlink=True)
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    # New Mixamo hierarchy, anatomically fitted rest translations. Existing
    # source rest rotations and local pose deltas remain intact; old rig removed.
    arm=bpy.data.armatures.new('OperatorMixamoSkeleton');rig=bpy.data.objects.new('OperatorRig',arm);bpy.context.collection.objects.link(rig)
    rig.matrix_world=donor_world;rig.animation_data_create()
    world_scale=donor_world.to_scale()
    targets={
        'Hips':(0,0,.91),'Spine':(0,0,1.035),'Spine1':(0,0,1.16),'Spine2':(0,0,1.285),
        'Neck':(0,0,1.455),'Head':(0,0,1.535),'HeadTop_End':(0,0,1.72)}
    for side,sign in [('Left',1),('Right',-1)]:
        targets.update({side+'Shoulder':(sign*.03,0,1.393),side+'Arm':(sign*.155,0,1.367),side+'ForeArm':(sign*.265,-.002,1.033),side+'Hand':(sign*.325,-.006,.87),side+'HandIndex1':(sign*.347,-.025,.805),side+'HandIndex2':(sign*.352,-.027,.784),side+'HandIndex3':(sign*.357,-.029,.763),side+'HandIndex4':(sign*.362,-.030,.746),side+'UpLeg':(sign*.10,0,.89),side+'Leg':(sign*.145,0,.478),side+'Foot':(sign*.17,.035,.1434),side+'ToeBase':(sign*.17,-.10,.062),side+'Toe_End':(sign*.17,-.165,.04)})
    desired={name:donor_inverse@Matrix.LocRotScale(Vector(targets[name.removeprefix(P)]),(donor_world@poses[name]).to_quaternion(),world_scale) for name,_,_,_ in hierarchy}
    rests={}
    for name,parent,rest,length in hierarchy:
        rests[name]=(rests[parent]@desired[parent].inverted() if parent else Matrix.Identity(4))@desired[name]@basis[name].inverted()
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for name,parent,rest,length in hierarchy:
        b=arm.edit_bones.new(name);b.length=length*.9;b.matrix=rests[name]
        if parent:b.parent=arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    apply_pose(rig,basis)
    assert max((rig.pose.bones[name].matrix.translation-desired[name].translation).length*world_scale.x for name,_,_,_ in hierarchy)<.0001
    # Blender bone heat cannot solve this retained segmented armor mesh. Paint
    # fresh, smooth anatomical envelopes explicitly; no old weights survive.
    # Landmarks are measured on the retained source and share this fitted rig.
    def chain(value,points):
        if value<=points[0][0]:return {P+points[0][1]:1}
        for a,b in zip(points,points[1:]):
            if value<=b[0]:
                t=smooth((value-a[0])/(b[0]-a[0]));out={P+a[1]:1-t};out[P+b[1]]=out.get(P+b[1],0)+t;return out
        return {P+points[-1][1]:1}
    def envelope(co,force_body=False):
        x,y,z=co;side='Left' if x>0 else 'Right';ax=abs(x)
        if z>=.90:
            body=chain(z,[(.90,'Hips'),(1.035,'Hips'),(1.16,'Spine'),(1.285,'Spine1'),(1.40,'Spine2'),(1.475,'Neck'),(1.515,'Head')])
        else:
            body=chain(z,[(.10,side+'Foot'),(.22,side+'Leg'),(.42,side+'Leg'),(.55,side+'UpLeg'),(.80,side+'UpLeg'),(.90,'Hips')])
        boundary=.12+max(0,1.30-z)*.21
        arm_blend=smooth((ax-boundary)/.025) if .65<z<1.415 and not force_body else 0
        if arm_blend:
            arm_weights=chain(z,[(.83,side+'Hand'),(.905,side+'ForeArm'),(.995,side+'ForeArm'),(1.075,side+'Arm'),(1.33,side+'Arm'),(1.405,side+'Shoulder')])
            body={n:w*(1-arm_blend) for n,w in body.items()}
            for n,w in arm_weights.items():body[n]=body.get(n,0)+w*arm_blend
        return {n:w for n,w in body.items() if w>.00001}
    fresh_weights={}
    for obj in meshes:
        adj=[set() for v in obj.data.vertices]
        for e in obj.data.edges:
            a,b=e.vertices;adj[a].add(b);adj[b].add(a)
        unseen=set(range(len(adj)));body_vertices=set();sensor_vertices=set()
        while unseen:
            stack=[unseen.pop()];ids=[]
            while stack:
                n=stack.pop();ids.append(n)
                for other in adj[n]:
                    if other in unseen:unseen.remove(other);stack.append(other)
            coords=[old_world[obj.name]@obj.data.vertices[i].co for i in ids]
            center=sum(coords,Vector())/len(coords)
            # Keep complete belt/thigh pouches on their owning body region;
            # a projecting corner must not inherit a nearby forearm weight.
            if abs(center.x)<.255 and max(v.z for v in coords)<1.12 and min(v.z for v in coords)>.64:
                body_vertices.update(ids)
            if center.x>.13 and min(v.z for v in coords)>1.30 and max(v.z for v in coords)>1.415:
                sensor_vertices.update(ids)
        fresh_weights[obj.name]=[{P+'Spine2':1} if v.index in sensor_vertices else envelope(old_world[obj.name]@v.co,v.index in body_vertices) for v in obj.data.vertices]
    skin={b.name:rig.matrix_world@b.matrix@b.bone.matrix_local.inverted()@rig.matrix_world.inverted() for b in rig.pose.bones}
    max_error=0
    for obj in meshes:
        coords=[old_world[obj.name]@v.co for v in obj.data.vertices]
        obj.vertex_groups.clear();groups={n:obj.vertex_groups.new(name=n) for n,_,_,_ in hierarchy}
        obj.parent=rig;obj.matrix_world=Matrix.Identity(4)
        for v,co,seeds in zip(obj.data.vertices,coords,fresh_weights[obj.name]):
            weights={}
            for name,w in seeds.items():
                weights[name]=weights.get(name,0)+w
            weights=dict(sorted(weights.items(),key=lambda item:-item[1])[:4]);total=sum(weights.values())
            if total<=0:weights={P+'Hips':1};total=1
            weights={n:w/total for n,w in weights.items()}
            blend=Matrix([[sum(skin[n][r][c]*w for n,w in weights.items()) for c in range(4)] for r in range(4)])
            v.co=blend.inverted()@co
            max_error=max(max_error,((blend@v.co)-co).length)
            for n,w in weights.items():groups[n].add([v.index],w,'REPLACE')
        mod=obj.modifiers.new('MixamoSkin','ARMATURE');mod.object=rig
    for name,frames in animations.items():write_action(rig,name,frames,{'source_provider':'accepted Vanguard Mixamo source; fitted rest translations and inverse neutral skin binding','loop_candidate':name!='anim.humanoid.down'})
    rig['binding_source']='accepted Vanguard Mixamo rig; anatomy fitted; original OperatorRig removed'
    rig['neutral_reconstruction_error_m']=max_error
    ground_actions(rig)
    fit_handling(rig,True)
    rig['asset_id']='character.crew.operator.v1'
    save('character.crew.operator.v1','operator-v1.blend',True)
    print('OPERATOR_BIND_ERROR='+str(max_error))

def make_ranged():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/source/character.enemy.security_enforcer.v1/security-enforcer-v1.blend'))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.name='RangedEnforcerRig'
    # Keep the accepted complete skin, rest hierarchy and original locomotion.
    contact=bpy.data.objects.get('socket.attack.contact.primary')
    if contact:bpy.data.objects.remove(contact,do_unlink=True)
    strike=bpy.data.actions.get('anim.humanoid.melee_strike')
    if strike:bpy.data.actions.remove(strike)
    ground_actions(rig)
    fit_handling(rig,False)
    rig['asset_id']='character.enemy.ranged_enforcer.v1'
    save('character.enemy.ranged_enforcer.v1','ranged-enforcer-v1.blend',True)

def make_pistol():
    bpy.ops.wm.open_mainfile(filepath=str(CACHE/'raw/pistol.blend'))
    root=bpy.data.objects['weapon.crew.operator_pistol.v1']
    # Retained reconstructed source is already open-muzzled. Flip its -Y barrel
    # into the accepted weapon-local +Y authoring / Godot -Z socket contract.
    turn=Matrix.Rotation(math.pi,4,'Z')
    for obj in root.children:
        if obj.type=='MESH':
            for v in obj.data.vertices:v.co=turn@v.co
        elif obj.type=='EMPTY':
            obj.location=turn@obj.location
            obj.rotation_mode='QUATERNION';obj.rotation_quaternion=Quaternion()
            obj['authoring_local_forward']='+Y'
    bpy.data.objects['socket.grip.primary'].matrix_local=Matrix.Identity(4)
    root['asset_id']='weapon.crew.operator_pistol.v1';root['one_handed']=True;root['authoring_forward']='+Y'
    root['gameplay_attack_reference']='attack.crew.operator.pistol';root['selection_status']='2026-09-12 retained design production; owner review of live handling pending'
    save('weapon.crew.operator_pistol.v1','operator-pistol-v1.blend')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--asset',choices=['operator','ranged','pistol','all'],default='all')
    parser.add_argument('--publish-existing',action='store_true',help='Export committed editable sources without the retained backup cache.')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for name,func in [('pistol',make_pistol),('operator',make_operator),('ranged',make_ranged)]:
        if args.asset not in (name,'all'):continue
        if args.publish_existing:
            asset,leaf={'pistol':('weapon.crew.operator_pistol.v1','operator-pistol-v1.blend'),'operator':('character.crew.operator.v1','operator-v1.blend'),'ranged':('character.enemy.ranged_enforcer.v1','ranged-enforcer-v1.blend')}[name]
            bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/source'/asset/leaf))
            normalize_publication_names(asset)
            export(ROOT/'game/Assets/Published'/f'{asset}.glb',name!='pistol')
        else:func()
