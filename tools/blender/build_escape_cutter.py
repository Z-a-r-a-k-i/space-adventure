"""Author the approved rigid escape-cutter exterior; Blender 5.2, no providers.

blender --background --factory-startup --python-exit-code 1 \
  --python tools/blender/build_escape_cutter.py -- --render

Use --replace only to intentionally rebuild this asset's source/publication.
Authoring is metres, +Z up, +Y bow; glTF/Godot is +Y up, -Z bow.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
import tempfile
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_station_environment_v2 import (
    promote_staged_artifacts,
    publication_lock_path,
)

REPOSITORY = Path(__file__).resolve().parents[2]
ASSET_ID = "ship.escape_cutter.v1"
SOURCE = REPOSITORY / "art/source" / ASSET_ID / "model.blend"
PUBLICATION = REPOSITORY / "game/Assets/Published" / f"{ASSET_ID}.glb"
MANIFEST = SOURCE.parent / "manifest.json"
REVIEW = REPOSITORY / "artifacts/reviews" / ASSET_ID / "v01/rear.png"
RAMP_DROP = 0.96
RAMP_RUN = 2.0
RAMP_CLOSE = -math.pi / 2.0 - math.atan2(RAMP_DROP, RAMP_RUN)
GROUPS: dict[str, list[bpy.types.Object]] = {}
ROOT: bpy.types.Object


def material(name: str, color: tuple[float, float, float], metallic: float = 0.5,
             roughness: float = 0.45, emission: float = 0.0) -> bpy.types.Material:
    result = bpy.data.materials.new("mat.escape_cutter." + name)
    result.diffuse_color = (*color, 1)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Emission Color"].default_value = (*color, 1)
    shader.inputs["Emission Strength"].default_value = emission
    return result


def empty(name: str, location=(0.0, 0.0, 0.0), parent=None) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.2
    return obj


def finish(obj, mat, group, bevel=0.035, parent=None):
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("machined_edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.parent = parent or ROOT
    for face in obj.data.polygons:
        face.use_smooth = False
    GROUPS.setdefault(group, []).append(obj)
    return obj


def box(name, location, dimensions, mat, group, bevel=0.035, rotation=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, mat, group, bevel, parent)


def mesh(name, vertices, faces, mat, group, bevel=0.025):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, group, bevel)


def ring_profile(name, sections, mat, group):
    vertices = []
    for y, half_width, low, high in sections:
        vertices.extend([
            (-half_width, y, low + 0.32), (-half_width * .73, y, low),
            (half_width * .73, y, low), (half_width, y, low + .32),
            (half_width, y, high - .38), (half_width * .73, y, high),
            (-half_width * .73, y, high), (-half_width, y, high - .38),
        ])
    faces = [tuple(range(7, -1, -1))]
    for i in range(len(sections) - 1):
        for j in range(8):
            faces.append((i * 8 + j, i * 8 + (j + 1) % 8,
                          (i + 1) * 8 + (j + 1) % 8, (i + 1) * 8 + j))
    faces.append(tuple(range((len(sections) - 1) * 8, len(sections) * 8)))
    return mesh(name, vertices, faces, mat, group)


def beam(name, start, end, width, depth, mat, group):
    start, end = Vector(start), Vector(end)
    delta = end - start
    obj = box(name, (start + end) / 2, (width, depth, delta.length), mat, group)
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return obj


def join_groups():
    # Keep rigid animation owners separate; collapse static panel/bolt geometry.
    for group, objects in GROUPS.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        if len(objects) > 1:
            bpy.ops.object.join()
        bpy.context.object.name = group
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def build():
    global ROOT
    bpy.ops.wm.read_factory_settings(use_empty=True)
    GROUPS.clear()
    ROOT = empty("EscapeCutter")
    ROOT["asset_id"] = ASSET_ID
    ROOT["presentation_only"] = True
    ROOT["bow_godot"] = "-Z"
    ROOT["scope"] = "rigid exterior; no collision, gameplay, interior, or authored animation"
    shell = material("shell.navy", (.065, .10, .145))
    panel = material("armor.blue", (.11, .16, .215), .55, .5)
    trim = material("frame.warm_gray", (.28, .30, .30), .65, .37)
    dark = material("recess.charcoal", (.013, .021, .028), .35, .55)
    glass = material("cockpit.black_blue", (.012, .037, .048), .75, .18)
    metal = material("gear.steel", (.20, .23, .25), .8, .35)
    cyan = material("signal.cyan", (.05, .62, .85), .3, .25, 3.0)
    engine = material("engine.cyan", (.12, .72, 1.0), .15, .22, 4.0)
    amber = material("warning.amber", (.95, .39, .07), .2, .35, 2.2)

    ring_profile("pressure_shell", [
        (-4.65, 1.65, .85, 3.12), (-4.05, 2.0, .72, 3.40),
        (-1.4, 2.05, .72, 3.58), (2.55, 1.85, .9, 3.36),
        (4.65, 1.08, 1.08, 2.28), (5.5, .70, 1.24, 1.85),
    ], shell, "Hull_Shell")
    # Segmented side armor, lower rub rails, recessed vents, and panel fasteners.
    for side in (-1, 1):
        for index, y in enumerate((-3.4, -2.0, -.6, .8, 2.05)):
            x = 2.02 if y < 1 else 1.94
            box(f"flank_panel_{side}_{index}", (side * x, y, 2.06),
                (.10, 1.22, 1.30), panel, "Armor_Panels", .04)
            for dy in (-.52, .52):
                for z in (1.52, 2.60):
                    box("panel_fastener", (side * (x + .066), y + dy, z),
                        (.025, .065, .065), metal, "Fasteners", .012)
        for z in (1.18, 2.85):
            beam("longitudinal_frame", (side * 1.86, -4.0, z),
                 (side * 1.79, 2.62, z + .09), .10, .11, trim, "Frame_Trim")
        for y in (-3.95, -2.68, -1.28, .12, 1.52):
            box("vertical_seam", (side * 2.072, y, 2.02),
                (.06, .045, 1.41), trim, "Frame_Trim", .008)
        box("upper_vent_recess", (side * 1.76, -2.45, 3.338),
            (.67, 1.50, .065), dark, "Recesses", .02,
            rotation=(0, side * .606, 0))
        for y in (-3.02, -2.79, -2.56, -2.33, -2.10, -1.87):
            z = 3.4 + (y + 4.05) * .18 / 2.65 - .19 + .04
            box("vent_louvre", (side * 1.76, y, z),
                (.65, .085, .07), metal, "Frame_Trim", .01,
                rotation=(0, side * .606, 0))
        box("flank_signal_recess", (side * 2.09, .9, 2.43),
            (.065, .85, .20), dark, "Recesses", .02)
        box("flank_signal", (side * 2.13, .9, 2.43),
            (.025, .60, .055), cyan, "Signal_Lights", .008)

    for index, y in enumerate((-3.35, -1.85, -.35, 1.15)):
        box(f"roof_plate_{index}", (0, y, 3.54 if y < 1 else 3.45),
            (2.62, 1.34, .08), panel, "Armor_Panels", .04)
        for x in (-1.23, 1.23):
            box("roof_frame", (x, y, 3.60 if y < 1 else 3.51),
                (.075, 1.30, .08), trim, "Frame_Trim", .01)

    # Sloped cockpit glazing follows the angular bow, with structural mullions.
    windshield = [(-1.27, 2.75, 3.32), (1.27, 2.75, 3.32),
                  (.72, 4.38, 2.49), (-.72, 4.38, 2.49)]
    mesh("forward_glazing", windshield, [(0, 1, 2, 3)], glass, "Cockpit_Glazing", 0)
    for start, end in zip(windshield, windshield[1:] + windshield[:1]):
        beam("cockpit_frame", start, end, .105, .11, trim, "Frame_Trim")
    beam("cockpit_mullion", (0, 2.75, 3.35), (0, 4.38, 2.52),
         .10, .10, trim, "Frame_Trim")
    for side in (-1, 1):
        points = [(side * 1.84, 2.70, 2.78), (side * 1.24, 4.25, 2.05),
                  (side * 1.04, 4.27, 2.48), (side * 1.47, 2.72, 3.23)]
        mesh("cockpit_side_glazing", points, [(0, 1, 2, 3)], glass, "Cockpit_Glazing", 0)
        for start, end in zip(points, points[1:] + points[:1]):
            beam("side_window_frame", start, end, .065, .07, trim, "Frame_Trim")
    box("bow_sensor_recess", (0, 5.52, 1.58), (.74, .10, .30), dark, "Recesses", .05)
    box("bow_sensor_glass", (0, 5.58, 1.58), (.50, .015, .12), glass, "Cockpit_Glazing", .015)

    # Short integrated engine nacelles preserve the reference's compact span.
    for side, suffix in ((-1, "left"), (1, "right")):
        box("engine_shoulder", (side * 2.17, -1.55, 1.95),
            (.85, 3.95, 1.12), trim, "Engine_Housings", .14)
        box("engine_pod", (side * 2.51, -2.02, 1.89),
            (1.06, 3.80, 1.0), shell, "Engine_Housings", .18)
        for y in (-3.48, -2.31, -1.13):
            box("pod_armor", (side * 2.55, y, 2.39),
                (.91, 1.04, .12), panel, "Armor_Panels", .08)
            box("pod_band", (side * 2.58, y - .48, 1.88),
                (1.04, .12, 1.03), trim, "Frame_Trim", .08)
        box("engine_nozzle_frame", (side * 2.51, -4.04, 1.89),
            (1.04, .42, .91), trim, "Engine_Housings", .15)
        box("engine_nozzle_recess", (side * 2.51, -4.275, 1.89),
            (.79, .035, .62), dark, "Recesses", .08)
        socket = empty("socket.engine." + suffix, (side * 2.51, -4.32, 1.89), ROOT)
        socket.rotation_euler.z = math.pi
        socket["exhaust_godot"] = "+Z"
        box("engine_core", (0, 0, 0), (.62, .045, .16), engine,
            "Engine_Core_" + suffix, .04, parent=socket)
        box("pod_status", (side * 2.54, -1.42, 2.48),
            (.50, .07, .035), cyan, "Signal_Lights", .008)

    # Landing feet are the only ground contact; underbody remains clear.
    for side in (-1, 1):
        for y in (-3.05, 2.83):
            x = side * (1.48 if y < 0 else 1.04)
            box("landing_foot", (x, y, .11), (.84, 1.03, .22), dark, "Landing_Gear", .075)
            box("landing_sole", (x, y, .07), (.89, 1.08, .14), metal, "Landing_Gear", .025)
            beam("landing_strut", (x, y, .22), (x * .9, y, .94),
                 .18, .18, metal, "Landing_Gear")
            box("gear_collar", (x, y, .61), (.34, .40, .25), trim, "Landing_Gear", .03)

    # Shallow black vestibule is an exterior recess, not a traversable interior.
    box("boarding_shadow", (0, -4.72, 2.04), (1.98, .055, 2.09), dark, "Recesses", .04)
    box("threshold", (0, -4.81, 1.02), (2.03, .45, .12), metal, "Boarding_Frame", .03)
    for side in (-1, 1):
        box("hatch_jamb", (side * 1.08, -4.83, 2.04),
            (.20, .26, 2.21), trim, "Boarding_Frame", .07)
        box("hatch_jamb_light", (side * 1.27, -4.85, 1.95),
            (.055, .035, .47), amber, "Signal_Lights", .008)
    box("hatch_lintel", (0, -4.83, 3.10), (2.24, .26, .20), trim, "Boarding_Frame", .06)
    # Separate hatch lid stows along the roof, then swings down around local X.
    door = empty("pivot.door", (0, -4.79, 3.10), ROOT)
    door["closed_rotation_x_radians"] = -math.pi / 2.0
    box("door_panel", (0, 1.0, .09), (1.93, 2.0, .15), panel,
        "Door_Panel", .045, parent=door)
    ramp = empty("pivot.ramp", (0, -4.82, 1.02), ROOT)
    ramp["closed_rotation_x_radians"] = RAMP_CLOSE
    ramp["open_rotation_x_radians"] = 0.0
    ramp["rotation_axis"] = "+X"
    slope = math.atan2(RAMP_DROP, RAMP_RUN)
    box("ramp_deck", (0, -RAMP_RUN / 2, -RAMP_DROP / 2),
        (1.88, math.hypot(RAMP_RUN, RAMP_DROP), .10), panel, "Ramp_Deck",
        .018, rotation=(slope, 0, 0), parent=ramp)
    for i in range(10):
        distance = (i + .5) / 10
        box("ramp_grip", (0, -distance * RAMP_RUN, -distance * RAMP_DROP + .06),
            (1.67, .065, .035), metal, "Ramp_Grips", .008,
            rotation=(slope, 0, 0), parent=ramp)
    for side in (-1, 1):
        box("ramp_edge", (side * .93, -RAMP_RUN / 2, -RAMP_DROP / 2 + .04),
            (.085, math.hypot(RAMP_RUN, RAMP_DROP), .14), trim, "Ramp_Frame",
            .015, rotation=(slope, 0, 0), parent=ramp)
    entry = empty("socket.boarding.entry", (0, -6.68, .06), ROOT)
    interior = empty("socket.boarding.interior", (0, -3.90, 1.08), ROOT)
    entry["role"] = "ground approach destination; presentation marker only"
    interior["role"] = "departure disappearance destination; no gameplay interior"
    box("beacon_base", (0, -2.1, 3.66), (.30, .32, .11), dark, "Roof_Fittings", .04)
    box("beacon", (0, -2.1, 3.77), (.18, .20, .13), amber, "Signal_Lights", .025)
    join_groups()
    bpy.context.view_layer.update()


def validate():
    objects = list(bpy.context.scene.objects)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = sum(len(face.vertices) - 2 for obj in meshes for face in obj.data.polygons)
    required = {"EscapeCutter", "pivot.ramp", "pivot.door", "socket.boarding.entry",
                "socket.boarding.interior", "socket.engine.left", "socket.engine.right"}
    by_name = {obj.name: obj for obj in objects}
    if required - by_name.keys():
        raise RuntimeError(f"Missing cutter contract: {sorted(required - by_name.keys())}")
    if triangles > 40000 or len(meshes) > 30:
        raise RuntimeError(f"Cutter exceeds mesh budget: {triangles} triangles, {len(meshes)} meshes")
    if bpy.data.actions or any(obj.type == "ARMATURE" for obj in objects):
        raise RuntimeError("Exterior must remain rigid without authored animations")
    corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    minimum = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
    maximum = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
    size = maximum - minimum
    if not (5.8 < size.x < 6.5 and 11.5 < size.y < 12.8 and 3.6 < size.z < 4.4):
        raise RuntimeError(f"Unexpected cutter bounds: {tuple(size)}")
    if abs(minimum.z) > .035:
        raise RuntimeError(f"Landing/ramp contact is not grounded: {minimum.z}")
    root = by_name["EscapeCutter"]
    for name in required - {"EscapeCutter"}:
        if by_name[name].parent != root:
            raise RuntimeError(f"Required marker is not directly under cutter root: {name}")
    if any(abs(value - 1) > 1e-5 for obj in objects for value in obj.scale):
        raise RuntimeError("Cutter publication must have unit object scales")
    ramp = by_name["pivot.ramp"]
    if abs(ramp.get("closed_rotation_x_radians", 0) - RAMP_CLOSE) > 1e-5:
        raise RuntimeError("Ramp closure metadata lost during export")
    return {
        "triangles": triangles, "meshes": len(meshes),
        "materials": len({mat.name for obj in meshes for mat in obj.data.materials}),
        "bounds_blender_metres": {
            "min": [round(v, 5) for v in minimum], "max": [round(v, 5) for v in maximum]},
        "sockets_godot_metres": {
            name: [round(by_name[name].matrix_world.translation.x, 5),
                   round(by_name[name].matrix_world.translation.z, 5),
                   round(-by_name[name].matrix_world.translation.y, 5)]
            for name in sorted(required - {"EscapeCutter"})},
        "ramp_closed_rotation_x_radians": round(RAMP_CLOSE, 7),
    }


def render_review():
    global ROOT
    ROOT = bpy.data.objects["EscapeCutter"]
    floor = material("review.floor", (.15, .175, .20), .05, .73)
    box("review_floor", (0, 0, -.10), (200, 200, .15), floor, "review_floor", 0)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.world = bpy.data.worlds.new("review_world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.32, .37, .43, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .6
    for name, position, energy, size in (
        ("key", (6, -7, 12), 2400, 9), ("fill", (-8, -1, 7), 1800, 8),
        ("rim", (1, 8, 9), 2900, 7)):
        data = bpy.data.lights.new(name, "AREA")
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (Vector((0, 0, 1.5)) - obj.location).to_track_quat("-Z", "Y").to_euler()
        data.energy, data.shape, data.size = energy, "DISK", size
    camera = bpy.data.objects.new("review_camera", bpy.data.cameras.new("review_camera"))
    scene.collection.objects.link(camera)
    camera.location = (12, -16, 12)
    camera.rotation_euler = (Vector((0, -.4, 1.5)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type, camera.data.ortho_scale = "ORTHO", 15.8
    scene.camera = camera
    scene.render.resolution_x, scene.render.resolution_y = 1500, 1100
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    REVIEW.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(REVIEW)
    bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--replace", action="store_true")
    parser.add_argument("--render", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if not args.replace and any(p.exists() for p in (SOURCE, PUBLICATION, MANIFEST)):
        raise FileExistsError("Cutter exists; pass --replace for an intentional rebuild")
    build()
    authored = validate()
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    PUBLICATION.parent.mkdir(parents=True, exist_ok=True)
    staging_root = REPOSITORY / "artifacts/cutter-build"
    staging_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="stage-", dir=staging_root) as folder:
        staging = Path(folder)
        staged_source, staged_glb = staging / "model.blend", staging / "model.glb"
        bpy.ops.wm.save_as_mainfile(filepath=str(staged_source), compress=True)
        bpy.ops.export_scene.gltf(filepath=str(staged_glb), export_format="GLB",
                                  export_yup=True, export_extras=True,
                                  export_apply=True, export_animations=False)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(staged_glb))
        bpy.context.view_layer.update()
        imported = validate()
        report = {
            "asset_id": ASSET_ID, "status": "technical publication; owner/Godot review pending",
            "reference": "art/concepts/station-escape-ship-combat-v1/escape-cutter-exterior-turnaround-v1.png",
            "approval": "owner-approved station escape plan, 2026-09-12",
            "rights": "original dimensional Blender geometry/materials from project-owned concept; no external model/texture inputs",
            "tool": bpy.app.version_string,
            "reproduce": "tools/blender/build_escape_cutter.py -- --replace --render",
            "source": str(SOURCE.relative_to(REPOSITORY)).replace("\\", "/"),
            "publication": str(PUBLICATION.relative_to(REPOSITORY)).replace("\\", "/"),
            "source_bytes": staged_source.stat().st_size,
            "publication_bytes": staged_glb.stat().st_size,
            "authored": authored, "fresh_reimport": imported,
        }
        staged_manifest = staging / "manifest.json"
        staged_manifest.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        promote_staged_artifacts(((SOURCE, staged_source), (PUBLICATION, staged_glb),
                                 (MANIFEST, staged_manifest)), replace=args.replace,
                                transaction_id=str(os.getpid()),
                                lock_path=publication_lock_path(SOURCE, PUBLICATION))
    if args.render:
        render_review()
    print("SPACEADVENTURE_ESCAPE_CUTTER " + json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
