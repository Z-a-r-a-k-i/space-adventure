"""Author the four-area station extension using the accepted dimensional kit.

Run with Blender --background --factory-startup --python this_file -- [--replace].
The Godot scene owns navigation and encounter placement; this is visible art only.
"""
from pathlib import Path
import json
import math
import sys
import bpy
from mathutils import Matrix, Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_station_environment_v2 as kit
import station_tactical_geometry as tactical


def pipe(name, start, end, radius, assigned_material):
    a, b = (Vector((p[0], -p[2], p[1])) for p in (start, end))
    direction = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=direction.length,
        location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(assigned_material)
    return kit.tint_mesh(obj, (1, 1, 1, 1))


def room_landmarks(objects, layout, dark, armor, deck, cyan):
    """Visible functions of each room; wall relief never enters the clear floor."""
    def wall_detail(room, pieces, authored_north):
        # Author the relief facing inward on the north wall, then mirror its
        # placement onto the manifest's camera-far (south) wall. Rotate
        # each symmetric closed part, rather than applying negative scale, so
        # normals and winding remain valid and the controls face the room.
        bpy.context.view_layer.update()
        south = tactical.room(layout, room.lower())["bounds"][2]
        turn = Matrix.Rotation(math.pi, 4, 'Z')
        for obj in pieces:
            authored = obj.matrix_world.copy()
            transformed = turn @ authored
            transformed.translation = Vector((authored.translation.x,
                -(authored_north + south) - authored.translation.y, authored.translation.z))
            obj.matrix_world = transformed
        bpy.context.view_layer.update()
        assert max(-(obj.matrix_world @ vertex.co).y for obj in pieces
                   for vertex in obj.data.vertices) < south + layout["clearance"]
        owner = next(obj for obj in objects if obj.name == f'Wall_Escape_{room}_south')
        kit.join(owner.name, [owner, *pieces])

    def floor_paint(room, pieces):
        assert max((obj.matrix_world @ vertex.co).z for obj in pieces
                   for vertex in obj.data.vertices) <= .04
        owner = next(obj for obj in objects if obj.name == f'Floor_{room}')
        kit.join(owner.name, [owner, *pieces])

    # Service: exposed three-line coolant/utility manifold, with restrained
    # insulated runs, armored unions and a clearly different round silhouette.
    service = [kit.add_box('Service_UtilityMount', (18, 1.28, 12.96), (7.6, 2.05, .13), dark, bevel=.04)]
    for line, height in enumerate((.62, 1.22, 1.82)):
        service.append(pipe(f'Service_Conduit_{line}', (14.7, height, 12.83), (21.3, height, 12.83), .095, armor))
        for x in (15.1, 17.05, 19.05, 20.9):
            service.append(pipe(f'Service_Union_{line}_{x}', (x-.095, height, 12.83), (x+.095, height, 12.83), .145, dark))
        service.append(kit.add_box(f'Service_FlowLabel_{line}', (18, height, 12.715), (.65, .09, .035), cyan, bevel=.008))
    for x in (14.6, 21.4):
        service.append(kit.add_box(f'Service_Manifold_{x}', (x, 1.22, 12.85), (.30, 1.68, .22), armor, bevel=.04))
    wall_detail('Service', service, 13)

    # Security: tall paired scanner emitters frame an inset control wall.
    # All parts mount beyond the southern navigation boundary after placement.
    security = [kit.add_box('Security_ControlRecess', (32, 1.28, 13.96), (6.6, 2.0, .14), dark, bevel=.045)]
    for x in (29.15, 34.85):
        security.append(kit.add_box(f'Security_ScannerJamb_{x}', (x, 1.28, 13.82), (.34, 2.12, .23), armor, bevel=.04))
        security.append(kit.add_box(f'Security_ScannerEmitter_{x}', (x, 1.32, 13.69), (.09, 1.62, .02), cyan, bevel=.01))
    security.extend([
        kit.add_box('Security_ScannerHeader', (32, 2.32, 13.83), (6.02, .26, .21), armor, bevel=.04),
        kit.add_box('Security_ConsoleSurround', (32, 1.39, 13.86), (2.54, 1.12, .20), armor, bevel=.035),
        kit.add_box('Security_Display', (32, 1.48, 13.74), (2.20, .64, .025), deck, bevel=.018),
        kit.add_box('Security_ControlRail', (32, .89, 13.76), (2.62, .14, .07), dark, bevel=.02),
    ])
    for index, width in enumerate((1.28, .92, 1.52)):
        security.append(kit.add_box(f'Security_Telemetry_{index}', (31.82, 1.65-index*.17, 13.717), (width, .045, .012), cyan, bevel=.006))
    for x in (30.10, 33.90):
        security.append(kit.add_box(f'Security_AccessPlate_{x}', (x, 1.40, 13.83), (.44, .92, .17), armor, bevel=.025))
    wall_detail('Security', security, 14)

    # Dock: a recessed loading shutter, broad cargo jambs, horizontal slats and
    # loading-track paint. It reads as freight infrastructure, not a usable door.
    dock = [kit.add_box('Dock_CargoShutter', (46, 1.25, 13.96), (7.4, 2.24, .15), dark, bevel=.03)]
    for x in (42.25, 49.75):
        dock.append(kit.add_box(f'Dock_LoadingJamb_{x}', (x, 1.30, 13.84), (.34, 2.35, .23), armor, bevel=.045))
    dock.append(kit.add_box('Dock_LoadingLintel', (46, 2.36, 13.85), (7.84, .24, .21), armor, bevel=.035))
    for index, height in enumerate((.47, .91, 1.35, 1.79)):
        dock.append(kit.add_box(f'Dock_ShutterSlat_{index}', (46, height, 13.83), (7.04, .29, .11), armor, bevel=.02, tint=(.58,.60,.62,1)))
    for x in (44.40, 47.60):
        dock.append(kit.add_box(f'Dock_CargoLatch_{x}', (x, 1.24, 13.72), (.15, 1.66, .06), dark, bevel=.012))
    for x in (42.27, 49.73):
        dock.append(kit.add_box(f'Dock_LoadingBeacon_{x}', (x, 2.08, 13.704), (.13, .14, .025), cyan, bevel=.008))
    wall_detail('Dock', dock, 14)
    tracks=[]
    for x in (42.7, 49.3):
        tracks.append(kit.add_box(f'Dock_LoadingTrack_{x}', (x, .030, 11.7), (.13, .004, 3.0), armor, bevel=0, tint=(.78,.68,.46,1)))
        for z in (10.3, 11.2, 12.1, 13.0):
            tracks.append(kit.add_box(f'Dock_LoadingTick_{x}_{z}', (x, .031, z), (.68, .004, .12), armor, bevel=0, tint=(.78,.68,.46,1)))
    floor_paint('Dock', tracks)

    # Launch: a broad, non-emissive octagonal pad outline and four centre
    # alignment bars. These are paint-thin, leave target/VFX space quiet and
    # differ from the small glowing healing field used during the fight.
    pad=[]
    outline=[(58,7.8),(59.6,6.2),(67.6,6.2),(69.2,7.8),(69.2,16.2),(67.6,17.8),(59.6,17.8),(58,16.2)]
    for index,(a,b) in enumerate(zip(outline,outline[1:]+outline[:1])):
        dx,dz=b[0]-a[0],b[1]-a[1]
        pad.append(kit.add_box(f'Launch_PadEdge_{index}', ((a[0]+b[0])/2,.030,(a[1]+b[1])/2), (math.hypot(dx,dz),.004,.14), armor, bevel=0,
            rotation=(0,0,-math.atan2(dz,dx)),tint=(.88,.76,.50,1)))
    for x,z,dx,dz in [(63.5,10.5,.18,1.4),(63.5,13.8,1.4,.18),(67.2,10.5,.18,1.4)]:
        pad.append(kit.add_box(f'Launch_Alignment_{x}_{z}', (x,.031,z),(dx,.004,dz),armor,bevel=0,tint=(.88,.76,.50,1)))
    for x in (69.9,70.8):
        pad.append(kit.add_box(f'Launch_ExitGuide_{x}', (x,.032,8),(.10,.004,1.4),cyan,bevel=0))
    floor_paint('Launch', pad)


def main():
    kit.reset_scene()
    dark = kit.material("mat.station.escape.dark", (.036, .046, .057, 1), metallic=.32, roughness=.76)
    armor = kit.material("mat.station.escape.armor", (.30, .29, .27, 1), metallic=.48, roughness=.62)
    deck = kit.material("mat.station.escape.deck", (.05, .057, .064, 1), metallic=.14, roughness=.86)
    cyan = kit.material("mat.station.escape.route_cyan", (.035, .26, .30, 1), metallic=.16, roughness=.5, emission=1.2)
    objects = []
    layout = tactical.read_layout(kit.REPOSITORY)
    rooms = [value for value in layout["rooms"] if value["id"] not in ("solo", "party")]
    for spec in rooms:
        name = spec["id"].capitalize()
        west, east, south, north = spec["bounds"]
        x, z = (west + east) / 2, (south + north) / 2
        objects.append(tactical.tactical_floor(kit, f"Floor_{name}", spec, dark, armor, deck, cyan))
        for side, wall_z in [("north", north + .15), ("south", south - .15)]:
            objects.append(kit.wall(f"Wall_Escape_{name}_{side}", f"presentation.wall.escape.{spec['id']}.{side}",
                (x, 1.3, wall_z), (east-west, 2.6, .3), dark, armor, deck, cyan))
        # Three-metre door openings and passage centres share the navigation
        # manifest. The apron stays open toward the departure trajectory.
        doors = dict(spec["doors"])
        for wall_x, side in [(west, "west"), (east, "east")]:
            if side not in doors:
                continue
            door_z = doors[side]
            for lo, hi, segment in [(south, door_z-1.5, "south"), (door_z+1.5, north, "north")]:
                if hi <= lo:
                    continue
                objects.append(kit.wall(f"Wall_Escape_{name}_{side}_{segment}",
                    f"presentation.wall.escape.{spec['id']}.{side}_{segment}",
                    (wall_x, 1.3, (lo+hi)/2), (.3, 2.6, hi-lo), dark, armor, deck, cyan))
            strip_x = west+.6 if side == "west" else east-.6
            objects.append(kit.add_box(f"RouteStrip_{name}_{side}", (strip_x, .032, door_z), (.08, .008, 2.6), cyan, bevel=.003))
    for x, centre_z in layout["passages"]:
        objects.append(kit.floor_panel(f"Floor_Passage_{x}", (x, -.1, centre_z), (2, .2, 3), dark, armor, deck))
        for side, z in [("south", centre_z-1.65), ("north", centre_z+1.65)]:
            objects.append(kit.wall(f"Wall_Passage_{x}_{side}", f"presentation.wall.escape.passage{x}.{side}",
                (x, 1.3, z), (2, 2.6, .3), dark, armor, deck, cyan))
    room_landmarks(objects, layout, dark, armor, deck, cyan)
    asset_id = "kit.station.escape.v1"
    for obj in objects: obj["asset_id"] = asset_id
    def verify_open_pits():
        # Runs on the fresh import before publication, so a failed pit keeps the accepted files.
        for spec in rooms:
            tactical.verify_floor_pits(bpy.data.objects[f"Floor_{spec['id'].capitalize()}"], spec)
        return {"layout_revision": layout["revision"], "fresh_reimport_open_pits": sum(len(spec["pits"]) for spec in rooms)}
    report = kit.save_export_and_validate(asset_id, "escape", [obj.name for obj in objects], 100000, 4, "--replace" in sys.argv,
        before_promotion=verify_open_pits)
    print("SPACEADVENTURE_STATION_ESCAPE " + json.dumps(report, sort_keys=True))


if __name__ == "__main__": main()
