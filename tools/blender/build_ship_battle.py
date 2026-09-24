"""Original roof-off cutter derivative and roof-off hostile interceptor, Blender 5.2.

The station cutter publication is never modified. All geometry is presentation.
v2 look: dark gunmetal decks cut into plates, emissive light strips (cyan for the
cutter, red for the interceptor) and an interceptor whose three system rooms are
visible and targetable from above. Room/work/door/hazard anchors are unchanged.
Run with --render for one overhead review of each fresh-imported publication.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import tempfile
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_escape_cutter as kit
from build_station_environment_v2 import promote_staged_artifacts, publication_lock_path

ROOT = Path(__file__).resolve().parents[2]
FLOOR = 1.12
ROOMS = {
    "weapons": (0, -2.80, 3.20, 2.50),
    "shields": (-1.22, -.10, 1.28, 2.40),
    "life_support": (1.22, -.10, 1.28, 2.40),
    "engines": (0, 3.15, 3.40, 1.70),
    "passage": (0, .40, 1.10, 3.70),
}
WORK = {"weapons": (0, -2.65), "shields": (-1.18, -.10),
        "life_support": (1.18, -.10), "engines": (0, 2.90)}
DOORS = {"weapons": (0, -1.48), "shields": (-.57, -.10),
         "life_support": (.57, -.10), "engines": (0, 2.23),
         "airlock": (1.90, 1.85)}
HAZARDS = {
    "weapons": ((.72, -2.2), (-.85, -2.2)),
    "shields": ((-1.08, .60), (-1.08, -.80)),
    "life_support": ((1.08, .60), (1.08, -.80)),
    "engines": ((1.05, 3.05), (-1.05, 2.95)),
    "passage": ((0, .90), (0, 1.50)),
}
# Interceptor rooms (Godot x, z, width, depth); ship-battle.json enemy systems must match.
ENEMY_ROOMS = {
    "weapons": (0, -2.55, 2.0, 1.9),
    "shields": (0, -0.20, 2.5, 2.2),
    "engines": (0, 2.25, 2.7, 2.0),
}
P = {}


def box(name, x, y, z, sx, sy, sz, mat, group="structure", bevel=.025):
    return kit.box(name, (x, -z, y), (sx, sz, sy), P[mat], group, bevel)


def anchor(name, x, y, z):
    return kit.empty(name, (x, -z, y), kit.ROOT)


def palette(hostile=False):
    P.clear()
    accent = (.95, .16, .05) if hostile else (.10, .70, .95)
    strip = (1.0, .30, .08) if hostile else (.30, .88, 1.0)
    for name, color, metallic, roughness, glow in (
        ("navy", (.010, .016, .024), .6, .45, 0),
        ("armor", (.024, .036, .052), .55, .38, 0),
        ("frame", (.060, .070, .078), .8, .32, 0),
        ("floor", (.022, .026, .030), .45, .62, 0),
        ("floor2", (.046, .052, .058), .55, .48, 0),
        ("dark", (.008, .012, .016), .4, .55, 0),
        ("signal", accent, .3, .35, 4),
        ("amber", (.85, .39, .06), .2, .5, 2.5),
        ("screen", (.20, .05, .03) if hostile else (.03, .15, .22), .3, .25, 2.2),
        ("strip", strip, 0, .3, 4.5),
    ):
        P[name] = kit.material("battle." + name, color, metallic, roughness, glow)


def panel(x, z, width, depth, group):
    box("deck", x, FLOOR-.10, z, width, .20, depth, "frame", group, .04)
    box("floor", x, FLOOR-.015, z, width-.12, .035, depth-.12, "floor", group, .02)
    # Deck plates with visible seams: broad, readable divisions rather than texture noise.
    columns = max(1, round((width-.2)/.62))
    rows = max(1, round((depth-.2)/.62))
    plate_w, plate_d = (width-.2)/columns, (depth-.2)/rows
    for i in range(columns):
        for j in range(rows):
            box("floor_plate", x-(width-.2)/2+(i+.5)*plate_w, FLOOR+.006, z-(depth-.2)/2+(j+.5)*plate_d,
                plate_w-.05, .016, plate_d-.05, "floor2", group, .01)


def light_strip(x, z, sx, sz, y=FLOOR+.45):
    box("light_strip", x, y, z, sx, .014, sz, "strip", "light_strips", .004)


def wall(x, z, sx, sz, strip=False, group="cutaway_walls"):
    box("wall_base", x, FLOOR+.20, z, sx, .40, sz, "navy", group, .035)
    box("wall_cap", x, FLOOR+.415, z, sx+.025, .055, sz+.025,
        "frame", group, .015)
    if strip:
        # Emissive light line along the cap reads as the hull edge from overhead.
        light_strip(x, z, max(.03, sx-.14) if sx > sz else .035, max(.03, sz-.14) if sz >= sx else .035)


def gate(key, x, z):
    horizontal = key in ("weapons", "engines")
    span = .91 if horizontal else .84
    for side in (-1, 1):
        dx, dz = ((side*(span/2+.055), 0) if horizontal else (0, side*(span/2+.055)))
        box("jamb", x+dx, FLOOR+.32, z+dz, .13, .64, .13,
            "frame", "door_frames", .025)
        box("door_signal", x+dx, FLOOR+.65, z+dz, .08, .025, .08,
            "amber" if key == "airlock" else "signal", "door_frames", .005)
    box("leaf", x, FLOOR+.23, z, span if horizontal else .085, .46,
        .085 if horizontal else span, "armor", "door_"+key, .012)
    anchor("gate_"+key, x, FLOOR, z)


def console(key, x, z, width, depth):
    box("console_base", x, FLOOR+.23, z, width, .46, depth,
        "navy", "system_"+key, .055)
    box("console_frame", x, FLOOR+.47, z, width-.05, .08, depth-.04,
        "frame", "system_"+key, .02)
    box("console_screen", x, FLOOR+.515, z, width-.14, .025, depth-.14,
        "screen", "system_"+key, .015)
    box("console_signal", x, FLOOR+.535, z+.12, max(.08,width-.21), .02, .025,
        "signal", "system_"+key, .005)


def cylinder_g(name, x, y, z, radius, depth, group, mat="frame"):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=depth,
                                       location=(x, -z, y))
    return kit.finish(bpy.context.object, P[mat], group, .015)


def cutter():
    # Retain original nacelle masses and exact native engine sockets only.
    kit.build()
    keep = {"EscapeCutter", "Engine_Housings", "Engine_Core_left", "Engine_Core_right",
            "socket.engine.left", "socket.engine.right"}
    for obj in list(bpy.data.objects):
        if obj.name not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    kit.ROOT = bpy.data.objects["EscapeCutter"]
    kit.ROOT.name = "CutterCombat"
    kit.ROOT["asset_id"] = "ship.escape_cutter.combat.v1"
    kit.ROOT["scope"] = "roof-off battle presentation; gameplay external"
    kit.GROUPS.clear()
    palette()
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            for index,mat in enumerate(obj.data.materials):
                obj.data.materials[index]=P["signal" if "engine.cyan" in mat.name else
                                           "frame" if "frame" in mat.name else "navy"]
    # Dimensional pressure-deck shell using the original hull's segmented profile.
    kit.ring_profile("cutaway_lower_hull", [(-4.55,1.68,.72,1.22),
        (-3.9,2.0,.72,1.25), (-1.4,2.05,.72,1.25),
        (2.55,1.85,.90,1.23), (4.65,1.08,1.08,1.25),
        (5.5,.70,1.24,1.43)], P["navy"], "hull_shell")
    # Cut top cap to allow floors below the shell lip to show.
    obj = kit.GROUPS["hull_shell"][0]
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z > .9], context="FACES")
    bm.to_mesh(obj.data); bm.free()
    for key, (x,z,w,d) in ROOMS.items():
        panel(x,z,w,d,"deck_"+key)
        anchor("room_"+key,x,FLOOR,z)
    for side in (-1,1):
        panel(side*1.20,1.80,1.28,.70,"deck_crosspassage")
    for side in (-1,1):
        wall(side*1.92,-.3,.12,6.5,strip=True)
        for z in (-2.8,-1.45,0,2.7,3.6):
            box("outer_plate",side*1.99,FLOOR+.14,z,.14,.28,.75,
                "armor","hull_armor",.03)
        for z in (3.45,2.3,1.1):
            box("pod_armor",side*2.53,2.42,z,.90,.10,1.02,
                "armor","nacelle_panels",.06)
            box("pod_strip",side*2.54,2.48,z,.45,.028,.04,
                "signal","nacelle_panels",.009)
    wall(0,4.1,3.70,.15,strip=True)
    # Forward cockpit rim slopes inward with the existing bow envelope.
    for x in (-1.60,1.60): wall(x,-2.83,.12,2.55,strip=True)
    wall(0,-4.10,3.25,.12,strip=True)
    # Low partitions preserve every door's clear opening.
    for side in (-1,1):
        wall(side*1.03,-1.48,1.08,.13)
        wall(side*.57,-.99,.12,.90)
        wall(side*.57,.85,.12,1.00)
        wall(side*1.21,1.35,1.20,.12)
        wall(side*1.12,2.23,1.26,.12)
    # Starboard aft airlock opening, no full wall blocking the slot.
    # The sidewall above ends at z2.95; remove its opening via split replacement.
    # Replace long starboard wall objects by explicit segment lengths below.
    for obj in list(kit.GROUPS["cutaway_walls"]):
        if obj.location.x > 1.90 and abs(obj.location.y-.3) < .01:
            kit.GROUPS["cutaway_walls"].remove(obj)
            bpy.data.objects.remove(obj, do_unlink=True)
    wall(1.92,-.825,.12,4.75,strip=True)
    wall(1.92,3.23,.12,1.65,strip=True)
    # Passage guide lights and doorway thresholds.
    for z in (-.95,-.35,.25,.85,1.45):
        for side in (-1,1):
            box("guide_light",side*.42,FLOOR+.03,z,.05,.02,.14,"strip","light_strips",.006)
    for key,(x,z) in DOORS.items(): gate(key,x,z)
    console("weapons",0,-3.65,1.70,.48)
    for side in (-1,1):
        console("weapons",side*1.12,-3.37,.46,.85)
    # Shield coil column along outboard bulkhead.
    for z in (-.75,.55):
        cylinder_g("shield_coil",-1.69,FLOOR+.35,z,.15,.70,"system_shields","navy")
        for y in (.18,.38,.58):
            cylinder_g("shield_ring",-1.69,FLOOR+y,z,.16,.045,"system_shields","signal")
    console("shields",-1.69,-.10,.28,.48)
    # Life support: paired recognizable filter canisters, not medical equipment.
    for z in (-.68,.49):
        cylinder_g("oxygen_filter",1.69,FLOOR+.30,z,.15,.60,"system_life_support")
        cylinder_g("filter_cap",1.69,FLOOR+.63,z,.14,.05,"system_life_support","signal")
    console("life_support",1.69,-.08,.28,.43)
    for x in (-.95,.95):
        box("engine_service_mass",x,FLOOR+.30,3.72,.90,.60,.43,
            "navy","system_engines",.06)
        for i in range(4):
            box("engine_fins",x-.30+i*.20,FLOOR+.63,3.72,.075,.07,.36,
                "frame","system_engines",.015)
        box("engine_status",x,FLOOR+.33,3.475,.65,.07,.025,
            "signal","system_engines",.01)
    console("engines",0,3.82,.65,.35)
    for key,(x,z) in WORK.items():
        anchor("work_"+key,x,FLOOR,z); anchor("effect_"+key,x,1.50,z)
    for key, positions in HAZARDS.items():
        for kind, (x, z) in zip(("fire", "breach"), positions):
            # Above the deck inserts, clear of machinery and the crew name tags.
            anchor("hazard_"+kind+"_"+key,x,FLOOR+.03,z)
    # One fixed forward pulse gun integrated with the existing sensor nose.
    box("weapon_mount",0,1.45,-4.80,.64,.30,.90,"armor","gun_mount",.075)
    box("weapon_barrel",0,1.49,-5.40,.28,.20,.50,"dark","gun_mount",.035)
    box("muzzle_glow",0,1.49,-5.66,.20,.13,.025,"signal","gun_mount",.009)
    anchor("muzzle",0,1.49,-5.69)
    kit.join_groups()


def interceptor():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit.GROUPS.clear(); kit.ROOT=kit.empty("Interceptor")
    kit.ROOT["asset_id"]="ship.interceptor.v1"
    kit.ROOT["presentation_only"]=True
    kit.ROOT["revision"]="v2 roof-off rooms"
    palette(True)
    # Low roof-off pressure hull (Blender +Y is the bow). Its top opening (0.73 of the half-width)
    # clears every room, so the rooms read from overhead like the cutter's.
    kit.ring_profile("interceptor_pressure_hull",[
        (-4.4,1.30,.60,1.25),(-3.2,1.95,.45,1.28),(-.6,1.95,.42,1.28),(.9,1.80,.45,1.28),
        (1.7,1.55,.50,1.28),(3.5,1.40,.60,1.26),(4.6,.75,.70,1.22),(5.4,.25,.85,1.15)],P["navy"],"hull_shell")
    import bmesh
    hull=kit.GROUPS["hull_shell"][0]
    bm=bmesh.new(); bm.from_mesh(hull.data)
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.normal.z>.9],context="FACES")
    bm.to_mesh(hull.data); bm.free()
    for key,(x,z,w,d) in ENEMY_ROOMS.items():
        panel(x,z,w,d,"deck_"+key)
        anchor("room_"+key,x,FLOOR,z)
        # Side walls carry the red hull light lines.
        for side in (-1,1):
            wall(x+side*(w/2+.06),z,.12,d+.1,strip=True)
    # Bow and aft end walls, and bulkheads between rooms with narrow service hatches (no crew aboard).
    wall(0,-3.56,2.1,.12,strip=True)
    wall(0,3.31,2.8,.12,strip=True)
    for z,half in ((-1.55,1.25),(.95,1.35)):
        for side in (-1,1):
            wall(side*(half+.33)/2,z,(half-.33),.12)
        box("hatch_frame",0,FLOOR+.30,z,.62,.60,.16,"frame","bulkheads",.03)
        box("hatch_light",0,FLOOR+.62,z,.30,.03,.18,"strip","bulkheads",.006)
    for side in (-1,1):
        for z,half in ((-3.0,1.43),(-2.0,1.52),(-.9,1.80),(.5,1.95),(1.8,1.95),(3.0,1.93)):
            box("armor_rib",side*half,FLOOR+.18,z,.20,.36,.34,"armor","hull_armor",.05)
        box("engine_pod",side*2.30,1.08,2.45,.62,.78,3.3,"armor","nacelles",.14)
        for z in (1.2,2.2,3.2):
            box("pod_frame",side*2.30,1.49,z,.66,.06,.12,"frame","nacelles",.02)
        box("pod_strip",side*2.30,1.52,2.45,.05,.02,2.2,"signal","nacelles",.006)
        box("engine_core",side*2.30,1.10,4.12,.46,.30,.05,"signal","nacelles",.02)
        box("pod_pylon",side*2.06,1.10,2.45,.36,.18,1.1,"frame","nacelles",.03)
    # Weapons room: pulse cannon breech feeding the bow barrel, missile tubes to port.
    wx,wz,_,_=ENEMY_ROOMS["weapons"]
    box("cannon_breech",wx+.25,FLOOR+.30,wz-.15,.62,.60,.95,"frame","system_weapons",.06)
    box("cannon_core",wx+.25,FLOOR+.62,wz-.15,.30,.05,.62,"signal","system_weapons",.01)
    box("cannon_barrel",wx+.25,FLOOR+.36,wz-1.25,.24,.22,1.3,"dark","system_weapons",.04)
    for dx in (-.64,-.42):
        for dz in (-.28,.08,.44):
            cylinder_g("missile_tube",wx+dx,FLOOR+.28,wz+dz,.085,.56,"system_weapons","armor")
            cylinder_g("missile_tip",wx+dx,FLOOR+.58,wz+dz,.055,.04,"system_weapons","amber")
    box("weapons_console",wx+.62,FLOOR+.22,wz+.66,.42,.44,.28,"navy","system_weapons",.04)
    box("weapons_screen",wx+.62,FLOOR+.45,wz+.66,.32,.02,.18,"screen","system_weapons",.01)
    box("pulse_muzzle",wx+.25,FLOOR+.36,-4.45,.20,.14,.04,"signal","system_weapons",.01)
    anchor("effect_weapons",wx,1.50,wz)
    # Shields room: central emitter column ringed by glowing coils, capacitor banks either side.
    sx,sz,_,_=ENEMY_ROOMS["shields"]
    cylinder_g("shield_emitter",sx,FLOOR+.40,sz,.30,.80,"system_shields","frame")
    for y in (.20,.42,.64):
        cylinder_g("shield_coil",sx,FLOOR+y,sz,.34,.05,"system_shields","signal")
    cylinder_g("shield_cap",sx,FLOOR+.82,sz,.18,.05,"system_shields","strip")
    for side in (-1,1):
        box("capacitor",sx+side*.82,FLOOR+.22,sz,.34,.44,1.2,"armor","system_shields",.05)
        for dz in (-.36,0,.36):
            box("capacitor_light",sx+side*.82,FLOOR+.45,sz+dz,.12,.02,.12,"signal","system_shields",.005)
    anchor("effect_shields",sx,1.50,sz)
    # Engines room: reactor block with a hot core and heat fins, conduits out to the pods.
    ex,ez,_,_=ENEMY_ROOMS["engines"]
    box("reactor_block",ex,FLOOR+.30,ez+.10,1.1,.60,1.0,"frame","system_engines",.07)
    box("reactor_core",ex,FLOOR+.62,ez+.10,.52,.04,.48,"signal","system_engines",.02)
    for i in range(5):
        box("heat_fin",ex-.44+i*.22,FLOOR+.64,ez+.70,.06,.12,.30,"frame","system_engines",.012)
    for side in (-1,1):
        box("conduit",ex+side*.98,FLOOR+.18,ez+.10,.72,.16,.18,"armor","system_engines",.04)
    anchor("effect_engines",ex,1.50,ez)
    anchor("muzzle",wx+.25,FLOOR+.36,-4.55)
    kit.join_groups()


def validate(asset):
    bpy.context.view_layer.update()
    meshes=[o for o in bpy.data.objects if o.type=="MESH"]
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
    mats={m.name for o in meshes for m in o.data.materials}
    if tris>60000 or len(meshes)>100 or len(mats)>12:
        raise ValueError(f"Budget exceeded: {tris} triangles/{len(meshes)} meshes/{len(mats)} mats")
    if any(abs(v-1)>1e-4 for o in meshes for v in o.scale): raise ValueError("Non-unit scale")
    names={o.name for o in bpy.data.objects}
    required={"muzzle","system_weapons","system_shields","system_engines"}
    if "cutter" in asset:
        required|={"room_"+k for k in ROOMS}|{"work_"+k for k in WORK}|{"door_"+k for k in DOORS}|{"system_life_support"}
        required|={"hazard_"+kind+"_"+k for kind in ("fire","breach") for k in ROOMS}
    else:
        required|={"room_"+k for k in ENEMY_ROOMS}|{"effect_"+k for k in ENEMY_ROOMS}
    if required-names: raise ValueError(f"Missing anchors: {required-names}")
    points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    clearance = {}
    if "cutter" in asset:
        from mathutils.bvhtree import BVHTree
        # Test a 0.6m-wide crew envelope against actual exported triangles, not
        # presentation marker spacing alone. Floor rays include the footprint.
        vertices=[]; faces=[]
        for obj in meshes:
            offset=len(vertices)
            vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
            faces.extend(tuple(offset+i for i in p.vertices) for p in obj.data.polygons)
        tree=BVHTree.FromPolygons(vertices,faces)
        for key,(x,z) in WORK.items():
            distances=[]
            for height in (.40,.90,1.50):
                nearest=tree.find_nearest(Vector((x,-z,FLOOR+height)))
                if nearest[0] is None or nearest[3]<.30:
                    raise ValueError(f"Crew envelope intersects geometry at {key}: {nearest[3]}")
                distances.append(nearest[3])
            for i in range(9):
                r=0 if i==0 else .28
                angle=i*math.tau/8
                start=Vector((x+r*math.cos(angle),-z+r*math.sin(angle),FLOOR+.10))
                hit=tree.ray_cast(start,Vector((0,0,-1)),.25)
                if hit[0] is None or abs(hit[0].z-FLOOR)>.06:
                    raise ValueError(f"Missing supporting floor at {key}: {hit[0]}")
            clearance[key]={"radius_m":.30,"minimum_clearance_m":round(min(distances),4),"support_samples":9}
    return {"triangles":tris,"meshes":len(meshes),"materials":len(mats),
            "crew_clearance":clearance,
            "blender_bounds":{"min":lo,"max":hi},
            "anchors_godot":{o.name:[round(o.matrix_world.translation.x,4),
                round(o.matrix_world.translation.z,4),round(-o.matrix_world.translation.y,4)]
                for o in bpy.data.objects if o.type=="EMPTY" and o.name.startswith(("room_","work_","effect_","hazard_","gate_","muzzle","system_"))}}


def render(asset):
    scene=bpy.context.scene
    scene.render.engine="CYCLES"; scene.cycles.samples=32
    scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new("review_world"); scene.world.use_nodes=True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value=(.14,.18,.24,1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value=.5
    for position,energy,size in (((-5,4,10),1600,8),((6,-3,8),1200,6)):
        data=bpy.data.lights.new("review_light","AREA"); data.energy=energy; data.shape="DISK"; data.size=size
        obj=bpy.data.objects.new("review_light",data); scene.collection.objects.link(obj)
        obj.location=position; obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat("-Z","Y").to_euler()
    camera=bpy.data.objects.new("review_camera",bpy.data.cameras.new("review_camera"))
    scene.collection.objects.link(camera); camera.location=(0,0,20); camera.rotation_euler=(0,0,0)
    camera.data.type="ORTHO"; camera.data.ortho_scale=13
    scene.camera=camera; scene.render.resolution_x=1000; scene.render.resolution_y=1250
    scene.render.resolution_percentage=100; scene.view_settings.view_transform="AgX"
    path=ROOT/"artifacts/reviews/ship-battle/v02"/(asset+".png")
    path.parent.mkdir(parents=True,exist_ok=True); scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser(); parser.add_argument("--render",action="store_true")
    parser.add_argument("--replace",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    staging_root=ROOT/"artifacts/ship-asset-build"
    staging_root.mkdir(parents=True,exist_ok=True)
    artifacts = []
    with tempfile.TemporaryDirectory(prefix="pair-", dir=staging_root) as folder:
        for asset,builder in (("ship.escape_cutter.combat.v1",cutter),("ship.interceptor.v1",interceptor)):
            source=ROOT/"art/source"/asset/"model.blend"
            target=ROOT/"game/Assets/Published"/(asset+".glb")
            if not args.replace and (source.exists() or target.exists()): raise FileExistsError(asset)
            builder(); metrics=validate(asset)
            source.parent.mkdir(parents=True,exist_ok=True); target.parent.mkdir(parents=True,exist_ok=True)
            staging=Path(folder)/asset
            staging.mkdir()
            staged_source,staged_glb=staging/"model.blend",staging/"model.glb"
            bpy.ops.wm.save_as_mainfile(filepath=str(staged_source),compress=True)
            bpy.ops.export_scene.gltf(filepath=str(staged_glb),export_format="GLB",export_yup=True,
                                      export_extras=True,export_apply=True,export_animations=False)
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.gltf(filepath=str(staged_glb)); imported=validate(asset)
            report={"asset_id":asset,"status":"v2 presentation (dark plated decks, light strips, roof-off interceptor); owner acceptance pending",
                "rights":"original project-owned dimensional geometry; cutter nacelles from existing builder",
                "reference":"art/briefs/ship-battle-v1.md","tool":bpy.app.version_string,
                "authored":metrics,"fresh_reimport":imported,
                "floor_godot_y":FLOOR,"rooms":ROOMS if "cutter" in asset else ENEMY_ROOMS,
                "reproduce":"tools/blender/build_ship_battle.py -- --replace --render"}
            staged_manifest=staging/"manifest.json"
            staged_manifest.write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8",newline="\n")
            artifacts.extend(((source,staged_source),(target,staged_glb),
                (source.parent/"manifest.json",staged_manifest)))
            if args.render: render(asset)
            print("SHIP_ASSET_VALIDATED "+json.dumps({"asset":asset,**metrics}))
        promote_staged_artifacts(tuple(artifacts), replace=args.replace,
            transaction_id=str(os.getpid()), lock_path=publication_lock_path(artifacts[0][0], artifacts[1][0]))


if __name__=="__main__": main()
