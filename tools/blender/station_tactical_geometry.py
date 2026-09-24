"""Shared visible tactical decks; dimensions come from tools/station-layout.json."""
from pathlib import Path
import json
import math
import bpy
from mathutils import Vector


def read_layout(repository):
    return json.loads((Path(repository)/'tools/station-layout.json').read_text(encoding='utf-8-sig'))


def room(layout, room_id):
    return next(value for value in layout['rooms'] if value['id'] == room_id)


def subtract(rect, cut):
    x0,x1,z0,z1=rect
    a,b,c,d=max(x0,cut[0]),min(x1,cut[1]),max(z0,cut[2]),min(z1,cut[3])
    if b<=a or d<=c:
        return [rect]
    return [r for r in [(x0,a,z0,z1),(b,x1,z0,z1),(a,b,z0,c),(a,b,d,z1)]
            if r[1]-r[0]>.001 and r[3]-r[2]>.001]


def cut_rectangles(rect, pits):
    result=[rect]
    for pit in pits:
        result=[piece for current in result for piece in subtract(current,pit)]
    return result


def pipe(kit,name,start,end,radius,surface,tint=(1,1,1,1),vertices=12):
    a,b=(Vector((p[0],-p[2],p[1])) for p in (start,end))
    direction=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=direction.length,location=(a+b)/2)
    obj=bpy.context.object
    obj.name=name
    obj.rotation_mode='QUATERNION'
    obj.rotation_quaternion=Vector((0,0,1)).rotation_difference(direction.normalized())
    obj.data.materials.append(surface)
    return kit.tint_mesh(obj,tint)


def loop_pipe(kit,name,x,z,width,depth,height,radius,surface):
    # One continuous oval coolant loop, with broad facets visible from above.
    segments,sides=32,8
    vertices=[]
    for step in range(segments):
        angle=2*math.pi*step/segments
        outward=Vector((math.cos(angle),-math.sin(angle),0))
        center=Vector((x+width/2*math.cos(angle),-z-depth/2*math.sin(angle),height))
        for side in range(sides):
            a=2*math.pi*side/sides
            vertices.append(center+radius*(outward*math.cos(a)+Vector((0,0,math.sin(a)))))
    faces=[]
    for step in range(segments):
        for side in range(sides):
            a=step*sides+side
            b=((step+1)%segments)*sides+side
            c=((step+1)%segments)*sides+(side+1)%sides
            d=step*sides+(side+1)%sides
            faces.append((a,d,c,b))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(surface)
    return kit.tint_mesh(obj,(.6,.81,.80,1))


def pit_geometry(kit,name,bounds,theme,dark,armor,deck,cyan):
    x0,x1,z0,z1=bounds
    x,z=(x0+x1)/2,(z0+z1)/2
    width,depth=x1-x0,z1-z0
    shaft_depth=1.85 if theme=='freight' else 1.35
    pieces=[]
    machine=[]
    def box(suffix,center,size,surface=armor,bevel=.025,tint=(1,1,1,1),machinery=False):
        obj=kit.add_box(name+'_'+suffix,center,size,surface,bevel=bevel,tint=tint)
        (machine if machinery else pieces).append(obj)
        return obj
    box('RecessBase',(x,-shaft_depth,z),(width,.14,depth),dark)
    for side,edge in [('West',x0),('East',x1)]:
        box(side+'Lining',(edge,-shaft_depth/2,z),(.08,shaft_depth,depth),deck)
        box(side+'Coping',(edge+(-.095 if side=='West' else .095),.045,z),(.19,.09,depth+.38),armor)
    for side,edge in [('South',z0),('North',z1)]:
        box(side+'Lining',(x,-shaft_depth/2,edge),(width,shaft_depth,.08),deck)
        box(side+'Coping',(x,.045,edge+(-.095 if side=='South' else .095)),(width,.09,.19),armor)
    # Broad alternating hazard dashes live on the 9 cm coping, inside the
    # manifest's 40 cm navigation clearance; there is no tall railing or cover.
    for side,edge in [('S',z0-.095),('N',z1+.095)]:
        count=max(3,round(width/.48))
        for index in range(count):
            box(f'Caution{side}{index}',(x0+(index+.5)*width/count,.094,edge),(.24,.006,.17),armor,0,(1,.72,.30,1))
    for side,edge in [('W',x0-.095),('E',x1+.095)]:
        count=max(3,round(depth/.48))
        for index in range(count):
            box(f'Caution{side}{index}',(edge,.094,z0+(index+.5)*depth/count),(.17,.006,.24),armor,0,(1,.72,.30,1))
    def tube(suffix,a,b,radius,surface=armor,tint=(1,1,1,1)):
        machine.append(pipe(kit,name+'_'+suffix,a,b,radius,surface,tint))
    if theme=='breach':
        # A ruptured power trunk and two dropped, angled deck fragments remain
        # inside the recess, below the playable floor.
        for offset in (-.31,.31):
            tube(f'BrokenLeft{offset}',(x0+.15,-.50,z+offset),(x-.28,-.52,z+offset),.20)
            tube(f'BrokenRight{offset}',(x+.23,-.62,z+offset),(x1-.15,-.50,z+offset),.20)
            tube(f'ExposedCore{offset}',(x-.27,-.52,z+offset),(x-.13,-.59,z+offset),.115,deck)
        for index,(px,pz,tilt) in enumerate([(x-.22,z+.48,.12),(x+.35,z-.43,-.16)]):
            obj=box(f'DroppedPlate{index}',(px,-.65,pz),(.73,.055,.35),armor,.012,(.50,.52,.56,1),True)
            obj.rotation_euler.y=tilt
    elif theme=='coolant':
        machine.append(loop_pipe(kit,name+'_CoolantLoop',x,z,width-.90,depth-.90,-.53,.20,armor))
        tube('PumpBody',(x,-.93,z),(x,-.35,z),.38,deck)
        tube('PumpCap',(x,-.37,z),(x,-.31,z),.30,armor)
        box('PumpStatus',(x,-.27,z),(.42,.025,.10),cyan,.008,machinery=True)
        for offset in (-depth*.28,depth*.28):
            box(f'LoopBrace{offset}',(x,-.81,z+offset),(width-.50,.20,.24),dark,machinery=True)
    elif theme=='scanner':
        tube('ScannerBase',(x,-.98,z),(x,-.67,z),min(width,depth)*.35,dark)
        tube('ScannerDish',(x,-.63,z),(x,-.50,z),min(width,depth)*.32,armor)
        tube('ScannerFace',(x,-.48,z),(x,-.44,z),min(width,depth)*.245,deck)
        for offset in (-.25,.25):
            box(f'ScanEmitter{offset}',(x,-.405,z+offset),(.58,.022,.11),cyan,.009,machinery=True)
        for offset in (-width*.35,width*.35):
            box(f'ScanRail{offset}',(x+offset,-.67,z),(.13,.25,depth-.38),armor,machinery=True)
    elif theme=='freight':
        # The lift is visibly lowered; the large uninterrupted dark reveal
        # around its platform makes the freight void legible from tactical range.
        box('LoweredLift',(x,-1.46,z),(width-.64,.18,depth-.64),armor,.04,(.57,.60,.62,1),True)
        box('LiftInset',(x,-1.355,z),(width-.85,.025,depth-.85),deck,.014,machinery=True)
        for px in (x0+.24,x1-.24):
            for pz in (z0+.29,z1-.29):
                tube(f'LiftGuide{px}{pz}',(px,-1.60,pz),(px,-.20,pz),.10,armor)
        for offset in (-.63,.63):
            box(f'FreightFootprint{offset}',(x+offset,-1.33,z),(.12,.014,depth-.98),armor,0,(1,.72,.30,1),True)
    else:
        # Transit power trench and launch service sockets use broad conduit
        # bundles, with distinct power collars and below-deck end manifolds.
        along_z=depth>=width
        for index,offset in enumerate((-.31,.31)):
            a=(x+offset,-.55,z0+.23) if along_z else (x0+.23,-.55,z+offset)
            b=(x+offset,-.55,z1-.23) if along_z else (x1-.23,-.55,z+offset)
            tube(f'PowerRun{index}',a,b,.18)
            for fraction in (.23,.50,.77):
                if along_z:
                    center=(x+offset,-.55,z0+depth*fraction)
                    size=(.43,.44,.16)
                else:
                    center=(x0+width*fraction,-.55,z+offset)
                    size=(.16,.44,.43)
                box(f'PowerCollar{index}{fraction}',center,size,deck,machinery=True)
        box('StatusPanel',(x,-.28,z),(.42,.025,.13),cyan,.008,machinery=True)
    bpy.context.view_layer.update()
    assert max((obj.matrix_world @ v.co).z for obj in machine for v in obj.data.vertices) < -.12
    return pieces+machine


def tactical_floor(kit,name,spec,dark,armor,deck,cyan):
    bounds,pits=spec['bounds'],spec['pits']
    x0,x1,z0,z1=bounds
    width,depth=x1-x0,z1-z0
    pieces=[]
    def rectangle(suffix,rect,height,thickness,surface,bevel=.025,inset=0,tint=(1,1,1,1)):
        a,b,c,d=rect
        if min(b-a,d-c)<=inset*2+.02:
            return
        pieces.append(kit.add_box(name+'_'+suffix,((a+b)/2,height,(c+d)/2),
            (b-a-inset*2,thickness,d-c-inset*2),surface,bevel=bevel,tint=tint))
    for index,rect in enumerate(cut_rectangles(bounds,pits)):
        rectangle(f'Slab{index}',rect,-.10,.20,dark,.02)
    columns,rows=max(1,round(width/3)),max(1,round(depth/3))
    for column in range(columns):
        for row in range(rows):
            tile=(x0+column*width/columns,x0+(column+1)*width/columns,
                  z0+row*depth/rows,z0+(row+1)*depth/rows)
            for index,rect in enumerate(cut_rectangles(tile,pits)):
                rectangle(f'Plate{column}_{row}_{index}',rect,.011,.024,armor,.018,.035)
                rectangle(f'Inset{column}_{row}_{index}',rect,.024,.006,deck,0,.09)
    rectangle('EdgeS',(x0,x1,z0+.03,z0+.19),.025,.020,armor,.01)
    rectangle('EdgeN',(x0,x1,z1-.19,z1-.03),.025,.020,armor,.01)
    rectangle('EdgeW',(x0+.03,x0+.19,z0+.19,z1-.19),.025,.020,armor,.01)
    rectangle('EdgeE',(x1-.19,x1-.03,z0+.19,z1-.19),.025,.020,armor,.01)
    for index,pit in enumerate(pits):
        pieces.extend(pit_geometry(kit,f'{name}_Pit{index}',pit,spec['theme'],dark,armor,deck,cyan))
        # Quiet corner dashes suggest both flanking lanes without implying
        # the recess blocks a firearm shot or carries a new hazard rule.
        a,b,c,d=pit
        for corner,(px,pz) in enumerate(((a-.65,c-.65),(b+.65,c-.65),(a-.65,d+.65),(b+.65,d+.65))):
            rectangle(f'Lane{index}_{corner}',(px-.23,px+.23,pz-.06,pz+.06),.034,.004,armor,0,tint=(.72,.85,.86,1))
    obj=kit.join(name,pieces)
    obj['layout_room_id']=spec['id']
    obj['pit_count']=len(pits)
    return obj


def verify_floor_pits(floor,spec):
    # Inspect the actual combined mesh, including floor plates, rim, markings
    # and machinery. Every sampled interior must be open below walking height.
    bpy.context.view_layer.update()
    inverse=floor.matrix_world.inverted()
    for pit in spec['pits']:
        x0,x1,z0,z1=pit
        for i in range(1,6):
            for j in range(1,6):
                origin=Vector((x0+(x1-x0)*i/6,-(z0+(z1-z0)*j/6),2))
                found,hit,normal,index=floor.ray_cast(inverse@origin,inverse.to_3x3()@Vector((0,0,-1)))
                assert found, f'{floor.name}: pit bottom is missing'
                assert (floor.matrix_world@hit).z < -.10, f'{floor.name}: walking-height surface bridges pit at {origin}'
