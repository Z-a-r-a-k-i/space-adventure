"""Rebuild the authored station's spatial wrappers from station-layout.json.

The manifest is an offline authoring input shared with Blender, not runtime
content. Gameplay placements and the resulting navigation remain in the scene.
"""
from pathlib import Path
import json
import math
import re

ROOT = Path(__file__).resolve().parents[1]
LAYOUT = json.loads((ROOT / 'tools/station-layout.json').read_text())


def cells(bounds, holes):
    """Conforming quads: every neighbouring edge has the same endpoints."""
    west, east, south, north = bounds
    xs = sorted({west, east, *(x for h in holes for x in h[:2])})
    zs = sorted({south, north, *(z for h in holes for z in h[2:])})
    return [(a, b, c, d) for a, b in zip(xs, xs[1:])
            for c, d in zip(zs, zs[1:])
            if not any(h[0] < (a+b)/2 < h[1] and h[2] < (c+d)/2 < h[3] for h in holes)]


def vector(x, y, z):
    return f'Vector3({x:g}, {y:g}, {z:g})'


def main():
    path = ROOT / 'game/scenes/station_route.tscn'
    scene = path.read_text(encoding='utf-8')
    blocks = re.split(r'(?=^\[node )', scene, flags=re.M)
    resources, nodes = blocks[0], blocks[1:]

    def update(parent, name, **properties):
        prefix = f'[node name="{name}" '
        index = next(i for i, n in enumerate(nodes) if n.startswith(prefix) and f'parent="{parent}"' in n.splitlines()[0])
        n = nodes[index]
        for key, value in properties.items():
            if re.search(rf'^{key} = .*$', n, re.M):
                n = re.sub(rf'^{key} = .*$', f'{key} = {value}', n, flags=re.M)
            else:
                n = n.rstrip() + f'\n{key} = {value}\n\n'
        nodes[index] = n

    floor_names = {'SoloCombatArena', 'MainPartyArena', *(f'Escape_{r["id"]}' for r in LAYOUT['rooms'][2:])}
    # Idempotently replace only generated or superseded spatial floor wrappers.
    nodes = [n for n in nodes if not any(
        f'parent="Environment/Floors/{name}"' in n.splitlines()[0]
        or (f'name="{name}"' in n.splitlines()[0] and 'parent="Environment/Floors"' in n.splitlines()[0])
        for name in floor_names)
        and not re.search(r'(?:name="Layout_|parent="Environment/Floors/Layout_)', n.splitlines()[0])]
    resources = re.sub(r'\[sub_resource type="BoxShape3D" id="layout_floor_\d+"\]\n.*?(?=\[|\Z)', '', resources, flags=re.S)
    vertices, polygons = [], []
    vertex_ids = {}

    def nav_quad(rect):
        a, b, c, d = rect
        polygon = []
        for x, z in [(a,c), (b,c), (b,d), (a,d)]:
            key = (round(x, 5), round(z, 5))
            if key not in vertex_ids:
                vertex_ids[key] = len(vertices)
                vertices.append((x, 0, z))
            polygon.append(vertex_ids[key])
        polygons.append(polygon)

    for rect in [(-12.7,-7.3,4.3,9.7), (-4.7,1.7,-2.7,3.3), (5.7,11.7,5.3,10.7)]:
        nav_quad(rect)
    shapes = []
    for room in LAYOUT['rooms']:
        a,b,c,d = room['bounds']
        inset = LAYOUT['clearance'] if room['id'] not in ('solo','party') else .3
        nav_bounds = (a+inset, b-inset, c+inset, d-inset)
        if room['id'] == 'apron':
            nav_bounds = (74.4,77.2,2.4,13.6)
        holes = [(h[0]-.4,h[1]+.4,h[2]-.4,h[3]+.4) for h in room['pits']]
        for rect in cells(nav_bounds, holes):
            nav_quad(rect)
        for rect in cells(room['bounds'], room['pits']):
            x1,x2,z1,z2 = rect
            index = len(shapes)
            shape_id = f'layout_floor_{index}'
            shapes.append(f'[sub_resource type="BoxShape3D" id="{shape_id}"]\nsize = {vector(x2-x1,.2,z2-z1)}\n\n')
            name = f'Layout_{room["id"]}_{index}'
            nodes.append(f'[node name="{name}" type="StaticBody3D" parent="Environment/Floors"]\n'
                         f'position = {vector((x1+x2)/2,-.1,(z1+z2)/2)}\ncollision_layer = 1\ncollision_mask = 0\n'
                         f'[node name="CollisionShape3D" type="CollisionShape3D" parent="Environment/Floors/{name}"]\nshape = SubResource("{shape_id}")\n\n')
        if room['id'] in ('solo','party'):
            continue
        update('.', f'EscapeLight_{room["id"]}', position=vector((a+b)/2,4,(c+d)/2), omni_range=str(max(b-a,d-c)))
        update('Environment/Wayfinding', f'EscapeSign_{room["id"]}', position=vector((a+b)/2,.043,c+1.3))

    nav = 'vertices = PackedVector3Array(' + ', '.join(f'{v:g}' for p in vertices for v in p) + ')\n'
    nav += 'polygons = [' + ', '.join('PackedInt32Array(' + ', '.join(map(str,p)) + ')' for p in polygons) + ']'
    resources = re.sub(r'vertices = PackedVector3Array\([^\n]*\)\npolygons = \[[^\n]*\]', nav, resources)
    resources += ''.join(shapes)
    for (x,z), area in zip(LAYOUT['passages'], ['security','dock','launch','airlock']):
        update('Environment/Floors', f'Passage{x}', position=vector(x,-.1,z))
        update('NavigationLinks', f'Escape_{area}', start_position=vector(x-1.7,0,z), end_position=vector(x+1.7,0,z))
        if area != 'airlock':
            update('Interactions', f'Escape_{area}', position=vector(x,0,z))
            update('Markers', f'EscapeDoor_{area}', position=vector(x-1.45,0,z))
    for area, encounter in LAYOUT['encounters'].items():
        tx,tz = encounter['trigger']
        update('Markers', f'EscapeTrigger_{area}', position=vector(tx,0,tz))
        for index,(x,z) in enumerate(encounter['crew']):
            update('Markers', f'{area}_crew{index}', position=vector(x,0,z))
        for index,(x,z) in enumerate(encounter['enemies']):
            yaw = math.degrees(math.atan2(x-tx,z-tz))
            update('Markers', f'Spawn_Escape_{area}_{index}', position=vector(x,0,z), rotation_degrees=vector(0,yaw,0))
            update('Hostiles', f'Escape_{area}_{index}', position=vector(x,0,z), rotation_degrees=vector(0,yaw,0))
    update('Markers', 'SecurityEnforcerSpawn', position=vector(-13.3,0,.6))
    update('Hostiles', 'SecurityEnforcer', position=vector(-13.3,0,.6))
    # Keep the opening's sign on intact deck, readable from the entry foothold.
    update('Environment/Wayfinding', 'Security', position=vector(-8.2,.042,-2.5), font_size='64', pixel_size='0.006')
    scene = resources + ''.join(nodes)
    for match in list(re.finditer(r'\[sub_resource type="(?:BoxMesh|BoxShape3D)" id="([^"]+)"\]\n.*?(?=\[)', scene, re.S)):
        if f'SubResource("{match[1]}")' not in scene:
            scene = scene.replace(match[0], '')
    count = len(re.findall(r'^\[(?:ext_resource|sub_resource) ', scene, re.M)) + 1
    scene = re.sub(r'load_steps=\d+', f'load_steps={count}', scene, count=1)
    path.write_bytes((scene.rstrip() + '\n').encode('utf-8'))
    print(f'Station spatial layout: {len(vertices)} vertices, {len(polygons)} polygons, {len(shapes)} picking floors.')


if __name__ == '__main__':
    main()
