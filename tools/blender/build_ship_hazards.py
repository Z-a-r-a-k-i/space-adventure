"""Original readable ship hazard sculptures; no gameplay or particle clock."""
import argparse
import json
import math
import os
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_station_environment_v2 import promote_staged_artifacts, publication_lock_path

ROOT = Path(__file__).resolve().parents[2]


def material(name, color, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = .65
    node.inputs['Emission Color'].default_value = (*color, 1)
    node.inputs['Emission Strength'].default_value = emission
    return mat


def mesh(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def flame(name, x, y, height, radius, mat):
    vertices = []
    for ring, (z, scale, bend) in enumerate(((.02, .7, 0), (.22, 1, .015), (.58, .55, -.035), (1, 0, .055))):
        for i in range(7):
            angle = i * math.tau / 7 + ring * .14
            vertices.append((x + math.cos(angle) * radius * scale + bend,
                             y + math.sin(angle) * radius * scale, z * height))
    faces = [tuple(reversed(range(7)))]
    for ring in range(3):
        for i in range(7):
            j = (i + 1) % 7
            faces.append((ring*7+i, ring*7+j, (ring+1)*7+j, (ring+1)*7+i))
    mesh(name, vertices, faces, mat)


def build_fire():
    outer = material('hazard.flame.copper', (1, .065, .007), .85)
    # Translucent outer tongues expose the gold centres from the overhead view.
    outer.node_tree.nodes['Principled BSDF'].inputs['Alpha'].default_value = .55
    outer.surface_render_method = 'DITHERED'
    inner = material('hazard.flame.gold', (1, .45, .025), 1.5)
    for i, (x, y, h, r) in enumerate(((0, 0, .88, .17), (-.19, .07, .58, .13), (.17, .12, .7, .12), (.02, -.17, .5, .12))):
        flame('flame_' + str(i), x, y, h, r, outer)
        flame('hot_core_' + str(i), x, y - .015, h*.62, r*.68, inner)


def build_breach():
    steel = material('hazard.torn.steel', (.11, .16, .19))
    edge = material('hazard.raw.edge', (.38, .46, .48))
    dark = material('hazard.vacuum', (.001, .003, .008))
    # A hole must remain dark under the game's bright overhead key light.
    shader = dark.node_tree.nodes.new('ShaderNodeEmission')
    shader.inputs['Color'].default_value = (.001, .003, .008, 1)
    shader.inputs['Strength'].default_value = 1
    dark.node_tree.links.new(shader.outputs[0], dark.node_tree.nodes['Material Output'].inputs['Surface'])
    vertices = []
    for i in range(12):
        angle = i * math.tau / 12
        radius = .32 if i % 2 else .35
        vertices.extend(((math.cos(angle)*radius, math.sin(angle)*radius, .014),
                         (math.cos(angle)*.20, math.sin(angle)*.20, .045 + (i%3)*.035)))
    mesh('torn_deck', vertices, [(i*2, ((i+1)%12)*2, ((i+1)%12)*2+1, i*2+1) for i in range(12)], steel)
    mesh('vacuum_aperture', [(math.cos(i*math.tau/12)*.215, math.sin(i*math.tau/12)*.215, .016) for i in range(12)], [tuple(range(12))], dark)
    for i in range(0, 12, 2):
        a = i * math.tau / 12
        mesh('fracture_' + str(i), [(math.cos(a)*.205, math.sin(a)*.205, .055),
                                  (math.cos(a+.09)*.28, math.sin(a+.09)*.28, .017),
                                  (math.cos(a+.2)*.21, math.sin(a+.2)*.21, .065)], [(0,1,2)], edge)


def validate():
    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    points = [o.matrix_world @ Vector(v) for o in objects for v in o.bound_box]
    count = sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
    bounds = [[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)]
    assert count < 1000 and all(bounds[i][1]-bounds[i][0] <= .71 for i in (0,1))
    assert bounds[2][0] >= -.001 and bounds[2][1] <= .9
    return {'triangles': count, 'meshes': len(objects), 'blender_bounds': bounds}


def render(asset):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 24
    scene.world = bpy.data.worlds.new('review'); scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.15,.19,.24,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .7
    camera = bpy.data.objects.new('review_camera', bpy.data.cameras.new('review_camera'))
    scene.collection.objects.link(camera); camera.location = (1,-1,2)
    camera.rotation_euler = (Vector((0,0,.3))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type = 'ORTHO'; camera.data.ortho_scale = 1.3; scene.camera = camera
    scene.render.resolution_x = 700; scene.render.resolution_y = 700; scene.render.resolution_percentage = 100
    scene.render.filepath = str(ROOT/'artifacts/reviews/ship-battle/v02'/(asset+'.png'))
    bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--replace', action='store_true'); parser.add_argument('--render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    staging_root = ROOT/'artifacts/ship-asset-build'; staging_root.mkdir(parents=True, exist_ok=True)
    artifacts = []
    with tempfile.TemporaryDirectory(prefix="pair-", dir=staging_root) as folder:
        for asset, builder in (('fx.ship_fire.v1', build_fire), ('fx.ship_breach.v1', build_breach)):
            bpy.ops.wm.read_factory_settings(use_empty=True); builder(); bpy.context.view_layer.update()
            metrics = validate()
            source = ROOT/'art/source'/asset/'model.blend'; target = ROOT/'game/Assets/Published'/(asset+'.glb')
            source.parent.mkdir(parents=True, exist_ok=True)
            stage = Path(folder)/asset
            stage.mkdir()
            blend = stage/'model.blend'; glb = stage/'model.glb'; manifest = stage/'manifest.json'
            bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
            bpy.ops.export_scene.gltf(filepath=str(glb), export_format='GLB', export_yup=True, export_animations=False)
            bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=str(glb)); bpy.context.view_layer.update()
            report = {'asset_id': asset, 'rights': 'Original project-owned geometry, no external samples or textures',
                      'reference': 'art/briefs/ship-battle-v1.md', 'status': 'Owner visual acceptance pending',
                      'origin': 'Floor contact; Godot +Y up; unit scale', 'authored': metrics, 'fresh_reimport': validate(),
                      'reproduce': 'tools/blender/build_ship_hazards.py -- --replace --render'}
            manifest.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
            artifacts.extend(((source, blend), (target, glb), (source.parent/'manifest.json', manifest)))
            if args.render: render(asset)
            print('HAZARD_VALIDATED ' + json.dumps(report))
        promote_staged_artifacts(tuple(artifacts), replace=args.replace,
            transaction_id=str(os.getpid()), lock_path=publication_lock_path(artifacts[0][0], artifacts[1][0]))


if __name__ == '__main__': main()
