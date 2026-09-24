"""Fresh-import every new crew GLB and measure its complete published actions."""
from pathlib import Path
import bpy,json,math,struct
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/art-expansion'
P='mixamorig:'

def mesh_positions():
    points=[];dg=bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or not any(m.type=='ARMATURE' for m in obj.modifiers):continue
        evaluated=obj.evaluated_get(dg);mesh=evaluated.to_mesh()
        points.extend(evaluated.matrix_world@v.co for v in mesh.vertices);evaluated.to_mesh_clear()
    return points

# Publication limits from each asset brief (art/briefs/): triangles, meshes, material slots.
# None means the brief sets no limit for that measure.
BUDGETS={'character.crew.operator.v1':{'triangles':35000,'meshes':18,'materials':8},
    'character.enemy.ranged_enforcer.v1':{'triangles':30000,'meshes':None,'materials':6},
    'weapon.crew.operator_pistol.v1':{'triangles':9000,'meshes':10,'materials':4}}

report={}
for asset,budget in BUDGETS.items():
    path=ROOT/'game/Assets/Published'/f'{asset}.glb';data=path.read_bytes();length,kind=struct.unpack_from('<II',data,12)
    gltf=json.loads(data[20:20+length]);bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path))
    assert all(len(asset+'.'+image.get('name','texture')+'.png')<=64 for image in gltf.get('images',[])), 'Embedded texture name exceeds portable filename budget'
    entry={'bytes':len(data),'animation_names':[a['name'] for a in gltf.get('animations',[])],
        'triangles':sum(gltf['accessors'][p['indices']]['count']//3 for m in gltf['meshes'] for p in m['primitives']),
        'materials':len(gltf.get('materials',[])),'meshes':len(gltf['meshes'])}
    for measure,limit in budget.items():
        assert limit is None or entry[measure]<=limit, f'{asset} has {entry[measure]} {measure}; its brief allows {limit}'
    entry['budget']=budget
    if asset.startswith('weapon'):
        assert not gltf.get('skins') and not gltf.get('animations')
        nodes={node['name']:node for node in gltf['nodes']};assert 'socket.grip.support' not in nodes
        assert 'socket.grip.primary' in nodes and 'socket.attack.muzzle.primary' in nodes
        muzzle=nodes['socket.attack.muzzle.primary']
        assert muzzle['translation'][2]<-.2 and 'rotation' not in muzzle
        entry['muzzle_forward_godot']=[0,0,-1]
    else:
        rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');entry['bones']=len(rig.data.bones)
        assert entry['bones']<=64
        required=['idle_holstered','walk_holstered','draw_primary','idle_armed','locomotion_armed','attack_primary','holster_primary','down']
        assert all('anim.humanoid.'+n in entry['animation_names'] for n in required)
        assert any(o.name=='socket.weapon.hand_primary' for o in bpy.context.scene.objects)
        assert any(o.name=='socket.weapon.holster_primary' for o in bpy.context.scene.objects)
        max_influences=0;unweighted=0;max_weight_error=0
        for obj in [o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]:
            for v in obj.data.vertices:
                w=[g.weight for g in v.groups if g.weight>.00001];max_influences=max(max_influences,len(w));unweighted+=not w;max_weight_error=max(max_weight_error,abs(sum(w)-1))
        print('SKIN',asset,max_influences,unweighted,max_weight_error)
        assert max_influences<=4 and not unweighted and max_weight_error<.0001
        entry['max_influences']=max_influences;entry['weight_error']=max_weight_error;entry['actions']={}
        for action in bpy.data.actions:
            if not action.name.startswith('anim.humanoid.'):continue
            rig.animation_data.action=action
            lo,hi=action.frame_range;floors=[];hips=[];feet={'Left':[],'Right':[]};maxheight=[]
            frames=list(range(math.floor(lo),math.ceil(hi)+1))
            for frame in frames:
                bpy.context.scene.frame_set(frame);bpy.context.view_layer.update();points=mesh_positions()
                floors.append(min(p.z for p in points));maxheight.append(max(p.z for p in points))
                hips.append(rig.matrix_world@rig.pose.bones[P+'Hips'].head)
                for side in feet:feet[side].append((rig.matrix_world@rig.pose.bones[P+side+'Foot'].head).z)
            stats={'frames':len(frames),'floor_min':min(floors),'floor_max':max(floors),'maximum_height':max(maxheight),
                'hip_vertical_range':max(p.z for p in hips)-min(p.z for p in hips),
                'hip_planar_range':max((a-b).xy.length for a in hips for b in hips),
                'hip_loop_delta':(hips[-1]-hips[0]).length,
                'foot_lift':{s:max(v)-min(v) for s,v in feet.items()},'final_height':maxheight[-1]}
            entry['actions'][action.name]=stats
        # Existing ranged fall source retains its accepted small floor envelope.
        idle=entry['actions']['anim.humanoid.idle_holstered'];walk=entry['actions']['anim.humanoid.locomotion_holstered']
        assert idle['maximum_height']>1.6 and idle['maximum_height']<2.05
        assert walk['hip_vertical_range']<=.15 and walk['hip_planar_range']<=.15
        assert min(walk['foot_lift'].values())>=.04
        assert walk['hip_loop_delta']<=.011
        if asset=='character.crew.operator.v1':
            assert min(a['floor_min'] for a in entry['actions'].values())>=-.025
            assert entry['actions']['anim.humanoid.down']['final_height']<.70
    report[asset]=entry
OUT.mkdir(parents=True,exist_ok=True);(OUT/'validation.json').write_text(json.dumps(report,indent=2))
print('STATION_CREW_PUBLICATIONS_VERIFIED='+str(OUT/'validation.json'))
