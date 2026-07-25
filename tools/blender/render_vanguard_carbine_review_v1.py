"""Fresh-import validation and standard review renders for the Vanguard carbine.

Run:

    blender --background --factory-startup \
      --python tools/blender/render_vanguard_carbine_review_v1.py -- \
      <weapon.glb> <output-directory> <manifest.json>
"""

from __future__ import annotations

import json
import math
import sys
from collections import deque
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ASSET_ID = "weapon.crew.vanguard_carbine.v1"
MARKERS = (
    "socket.grip.primary",
    "socket.grip.support",
    "socket.attack.muzzle.primary",
)
RESOLUTION_X = 960
RESOLUTION_Y = 720


def parse_paths() -> tuple[Path, Path, Path]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError(
            "Expected -- <weapon.glb> <output-directory> <manifest.json>"
        ) from exc
    values = sys.argv[separator + 1 :]
    if len(values) != 3:
        raise RuntimeError(
            "Expected weapon GLB, output directory, and manifest path"
        )
    return Path(values[0]).resolve(), Path(values[1]).resolve(), Path(values[2]).resolve()


def vec(value: Vector) -> list[float]:
    return [round(float(component), 8) for component in value]


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                minimum[axis] = min(minimum[axis], point[axis])
                maximum[axis] = max(maximum[axis], point[axis])
    return minimum, maximum


def topology(meshes: list[bpy.types.Object], *, weld: bool) -> dict[str, int]:
    vertices = edges = faces = triangles = 0
    components = boundary_edges = non_manifold_edges = loose_edges = 0
    zero_area_faces = 0
    for obj in meshes:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        if weld:
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1.0e-6)
            bmesh.ops.dissolve_degenerate(
                bm,
                edges=list(bm.edges),
                dist=1.0e-10,
            )
        remaining = set(bm.verts)
        while remaining:
            components += 1
            seed = remaining.pop()
            queue: deque[bmesh.types.BMVert] = deque([seed])
            while queue:
                vertex = queue.popleft()
                for edge in vertex.link_edges:
                    other = edge.other_vert(vertex)
                    if other in remaining:
                        remaining.remove(other)
                        queue.append(other)
        vertices += len(bm.verts)
        edges += len(bm.edges)
        faces += len(bm.faces)
        triangles += sum(max(1, len(face.verts) - 2) for face in bm.faces)
        boundary_edges += sum(1 for edge in bm.edges if edge.is_boundary)
        non_manifold_edges += sum(1 for edge in bm.edges if not edge.is_manifold)
        loose_edges += sum(1 for edge in bm.edges if edge.is_wire)
        zero_area_faces += sum(1 for face in bm.faces if face.calc_area() <= 1.0e-12)
        bm.free()
    return {
        "vertices": vertices,
        "edges": edges,
        "polygons": faces,
        "triangles": triangles,
        "connected_components": components,
        "boundary_edges": boundary_edges,
        "non_manifold_edges": non_manifold_edges,
        "loose_edges": loose_edges,
        "zero_area_faces": zero_area_faces,
    }


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.use_nodes = True
    material.diffuse_color = color
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = 0.05
    principled.inputs["Roughness"].default_value = 0.75
    if emission_strength:
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = emission_strength
    return material


def add_area_light(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    size: float,
    target: Vector,
) -> bpy.types.Object:
    data = bpy.data.lights.new(name=f"{name}.data", type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name=name, object_data=data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    point_at(light, target)
    return light


def add_wire_overlay(
    meshes: list[bpy.types.Object],
    material: bpy.types.Material,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name="review.wire.geometry", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = 0.00065
    curve.bevel_resolution = 0
    curve.materials.append(material)
    for source in meshes:
        for edge in source.data.edges:
            spline = curve.splines.new(type="POLY")
            spline.points.add(1)
            for point_index, vertex_index in enumerate(edge.vertices):
                point = source.matrix_world @ source.data.vertices[vertex_index].co
                spline.points[point_index].co = (*point, 1.0)
    overlay = bpy.data.objects.new(name="review.wire.overlay", object_data=curve)
    bpy.context.scene.collection.objects.link(overlay)
    overlay.hide_render = True
    return overlay


def add_axis_arrow(
    name: str,
    start: Vector,
    direction: Vector,
    length: float,
    material: bpy.types.Material,
) -> list[bpy.types.Object]:
    direction = direction.normalized()
    end = start + direction * length
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=12,
        radius=0.003,
        depth=length * 0.78,
        location=midpoint - direction * length * 0.11,
    )
    shaft = bpy.context.active_object
    shaft.name = f"{name}.shaft"
    shaft.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    shaft.data.materials.append(material)
    bpy.ops.mesh.primitive_cone_add(
        vertices=12,
        radius1=0.009,
        radius2=0.0,
        depth=length * 0.22,
        location=end - direction * length * 0.11,
    )
    head = bpy.context.active_object
    head.name = f"{name}.head"
    head.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    head.data.materials.append(material)
    for obj in (shaft, head):
        obj.hide_render = True
    return [shaft, head]


def add_scale_bar(
    minimum: Vector,
    material: bpy.types.Material,
) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    y_start = minimum.y - 0.05
    z = minimum.z - 0.065
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0.0, y_start + 0.5, z),
    )
    bar = bpy.context.active_object
    bar.name = "review.scale_bar.1m"
    bar.dimensions = (0.006, 1.0, 0.006)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bar.data.materials.append(material)
    objects.append(bar)
    for tick in range(11):
        bpy.ops.mesh.primitive_cube_add(
            size=1.0,
            location=(0.0, y_start + tick * 0.10, z + 0.012),
        )
        marker = bpy.context.active_object
        marker.name = f"review.scale_bar.tick_{tick:02d}"
        marker.dimensions = (0.008, 0.006, 0.026 if tick in {0, 5, 10} else 0.016)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        marker.data.materials.append(material)
        objects.append(marker)
    for obj in objects:
        obj.hide_render = True
    return objects


def set_visibility(objects: list[bpy.types.Object], visible: bool) -> None:
    for obj in objects:
        obj.hide_render = not visible


def render_view(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    output_dir: Path,
    name: str,
    target: Vector,
    *,
    location: Vector,
    projection: str,
    ortho_scale: float | None = None,
    wire: bpy.types.Object | None = None,
    wire_visible: bool = False,
    marker_geometry: list[bpy.types.Object] | None = None,
    markers_visible: bool = False,
    scale_geometry: list[bpy.types.Object] | None = None,
    scale_visible: bool = False,
) -> dict[str, object]:
    camera.data.type = projection
    camera.location = location
    point_at(camera, target)
    if projection == "ORTHO":
        if ortho_scale is None:
            raise RuntimeError("Orthographic render requires ortho_scale")
        camera.data.ortho_scale = ortho_scale
    else:
        camera.data.angle = math.radians(48.0)
    if wire is not None:
        wire.hide_render = not wire_visible
    if marker_geometry is not None:
        set_visibility(marker_geometry, markers_visible)
    if scale_geometry is not None:
        set_visibility(scale_geometry, scale_visible)

    output_path = output_dir / f"{name}.png"
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    if not output_path.is_file():
        raise RuntimeError(f"Blender did not create {output_path}")
    return {
        "name": name,
        "file": output_path.name,
        "bytes": output_path.stat().st_size,
        "projection": projection.lower(),
        "camera_location_blender_m": vec(location),
        "target_blender_m": vec(target),
        "ortho_scale_m": ortho_scale,
        "wire_overlay": wire_visible,
        "marker_overlay": markers_visible,
        "scale_reference": scale_visible,
    }


def main(
    glb_path: Path,
    output_dir: Path,
    manifest_path: Path,
) -> dict[str, object]:
    if not glb_path.is_file():
        raise FileNotFoundError(glb_path)
    if manifest_path.exists():
        raise FileExistsError(f"Refusing to overwrite {manifest_path}")
    output_dir.mkdir(parents=True, exist_ok=True)
    if any(output_dir.iterdir()):
        raise FileExistsError(f"Review output directory is not empty: {output_dir}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Vanguard Carbine Published GLB Review"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RESOLUTION_X
    scene.render.resolution_y = RESOLUTION_Y
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.65

    world = bpy.data.worlds.new("review.world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.018,
        0.024,
        0.038,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.11
    scene.world = world

    import_result = bpy.ops.import_scene.gltf(
        filepath=str(glb_path),
        import_pack_images=False,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()

    meshes = sorted(
        (obj for obj in scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    if not meshes:
        raise RuntimeError("Fresh import contains no weapon mesh")
    if armatures or bpy.data.actions:
        raise RuntimeError(
            f"Rigid weapon import contains {len(armatures)} armatures and "
            f"{len(bpy.data.actions)} actions"
        )
    root = bpy.data.objects.get(ASSET_ID)
    if root is None:
        raise RuntimeError(f"Missing asset root {ASSET_ID}")
    marker_objects: dict[str, bpy.types.Object] = {}
    for marker_name in MARKERS:
        marker = bpy.data.objects.get(marker_name)
        if marker is None:
            raise RuntimeError(f"Missing required marker {marker_name}")
        marker_objects[marker_name] = marker

    minimum, maximum = object_bounds(meshes)
    size = maximum - minimum
    center = (minimum + maximum) * 0.5
    if max(abs(size[index] - expected) for index, expected in enumerate((0.13, 0.82, 0.27))) > 1.0e-5:
        raise RuntimeError(f"Fresh-import envelope mismatch: {tuple(size)}")

    primary = marker_objects["socket.grip.primary"]
    root_primary_offset = (
        primary.matrix_world.translation - root.matrix_world.translation
    ).length
    if root_primary_offset > 1.0e-6:
        raise RuntimeError(
            f"Root and primary grip differ by {root_primary_offset} metres"
        )

    marker_metrics: dict[str, object] = {}
    for name, marker in marker_objects.items():
        basis = marker.matrix_world.to_3x3().normalized()
        forward = basis @ Vector((0.0, -1.0, 0.0))
        up = basis @ Vector((0.0, 0.0, 1.0))
        marker_metrics[name] = {
            "location_blender_m": vec(marker.matrix_world.translation),
            "fresh_import_forward_vector": vec(forward),
            "fresh_import_up_vector": vec(up),
            "forward_dot_expected_minus_y": round(
                float(forward.dot(Vector((0.0, -1.0, 0.0)))),
                8,
            ),
            "up_dot_expected_plus_z": round(
                float(up.dot(Vector((0.0, 0.0, 1.0)))),
                8,
            ),
            "scale": [round(float(value), 8) for value in marker.scale],
            "parent": marker.parent.name if marker.parent else None,
        }
        if marker_metrics[name]["forward_dot_expected_minus_y"] < 0.99999:
            raise RuntimeError(f"{name} forward orientation is invalid")
        if marker_metrics[name]["up_dot_expected_plus_z"] < 0.99999:
            raise RuntimeError(f"{name} up orientation is invalid")
        if any(abs(value - 1.0) > 1.0e-6 for value in marker.scale):
            raise RuntimeError(f"{name} scale is not unit")

    materials = sorted(
        {
            slot.material.name
            for obj in meshes
            for slot in obj.material_slots
            if slot.material is not None
        }
    )
    images = [
        image
        for image in bpy.data.images
        if image.name not in {"Render Result", "Viewer Node"}
    ]
    if len(materials) != 3:
        raise RuntimeError(f"Expected three materials, found {materials}")
    if images:
        raise RuntimeError(f"Expected no production textures, found {[i.name for i in images]}")

    raw_topology = topology(meshes, weld=False)
    welded_topology = topology(meshes, weld=True)
    if not 4000 <= welded_topology["triangles"] <= 8000:
        raise RuntimeError(f"Triangle target violated: {welded_topology['triangles']}")
    if welded_topology["boundary_edges"] or welded_topology["non_manifold_edges"]:
        raise RuntimeError(f"Fresh-import welded topology is invalid: {welded_topology}")

    camera_data = bpy.data.cameras.new("review.camera.data")
    camera_data.clip_start = 0.005
    camera_data.clip_end = 50.0
    camera = bpy.data.objects.new("review.camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    add_area_light(
        "review.key",
        (-0.85, -0.62, 0.92),
        82.0,
        1.4,
        center,
    )
    add_area_light(
        "review.fill",
        (-0.72, 0.76, 0.43),
        34.0,
        1.7,
        center,
    )
    add_area_light(
        "review.rim",
        (0.78, -0.05, 0.86),
        68.0,
        1.3,
        center,
    )

    wire_material = make_material(
        "review.wire.material",
        (0.002, 0.004, 0.008, 1.0),
    )
    wire = add_wire_overlay(meshes, wire_material)

    marker_materials = {
        "socket.grip.primary": make_material(
            "review.marker.primary",
            (0.95, 0.16, 0.55, 1.0),
            emission_strength=1.5,
        ),
        "socket.grip.support": make_material(
            "review.marker.support",
            (0.15, 0.92, 0.32, 1.0),
            emission_strength=1.5,
        ),
        "socket.attack.muzzle.primary": make_material(
            "review.marker.muzzle",
            (0.05, 0.68, 1.0, 1.0),
            emission_strength=1.5,
        ),
    }
    marker_geometry: list[bpy.types.Object] = []
    for name, marker in marker_objects.items():
        marker_geometry.extend(
            add_axis_arrow(
                f"review.{name}",
                marker.matrix_world.translation,
                marker.matrix_world.to_3x3() @ Vector((0.0, -1.0, 0.0)),
                0.11 if "muzzle" not in name else 0.14,
                marker_materials[name],
            )
        )
    scale_material = make_material(
        "review.scale.material",
        (0.68, 0.74, 0.82, 1.0),
        emission_strength=0.5,
    )
    scale_geometry = add_scale_bar(minimum, scale_material)
    bpy.context.view_layer.update()

    side_scale = 0.96
    front_scale = 0.39
    three_quarter_scale = 0.96
    renders = [
        render_view(
            scene,
            camera,
            output_dir,
            "left",
            center,
            location=Vector((-1.5, center.y, center.z)),
            projection="ORTHO",
            ortho_scale=side_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "right",
            center,
            location=Vector((1.5, center.y, center.z)),
            projection="ORTHO",
            ortho_scale=side_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "front-muzzle",
            center,
            location=Vector((center.x, minimum.y - 1.2, center.z)),
            projection="ORTHO",
            ortho_scale=front_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "back-stock",
            center,
            location=Vector((center.x, maximum.y + 1.2, center.z)),
            projection="ORTHO",
            ortho_scale=front_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "front-left-3q",
            center,
            location=center + Vector((-1.05, -1.15, 0.62)),
            projection="ORTHO",
            ortho_scale=three_quarter_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "front-right-3q",
            center,
            location=center + Vector((1.05, -1.15, 0.62)),
            projection="ORTHO",
            ortho_scale=three_quarter_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "top",
            center,
            location=Vector((center.x, center.y, maximum.z + 1.2)),
            projection="ORTHO",
            ortho_scale=1.18,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "underside",
            center,
            location=Vector((center.x, center.y, minimum.z - 1.2)),
            projection="ORTHO",
            ortho_scale=1.18,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "wireframe-front-right-3q",
            center,
            location=center + Vector((1.05, -1.15, 0.62)),
            projection="ORTHO",
            ortho_scale=three_quarter_scale,
            wire=wire,
            wire_visible=True,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "markers-front-right-3q",
            center,
            location=center + Vector((1.05, -1.15, 0.62)),
            projection="ORTHO",
            ortho_scale=three_quarter_scale,
            wire=wire,
            marker_geometry=marker_geometry,
            markers_visible=True,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "scale-reference",
            Vector((center.x, center.y + 0.09, center.z - 0.03)),
            location=Vector((-1.5, center.y, center.z - 0.03)),
            projection="ORTHO",
            ortho_scale=1.20,
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
            scale_visible=True,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "tactical-7.5m",
            center,
            location=center + Vector((-8.9, -10.2, 5.1)).normalized() * 7.5,
            projection="PERSP",
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "tactical-14.5m",
            center,
            location=center + Vector((-8.9, -10.2, 5.1)).normalized() * 14.5,
            projection="PERSP",
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
        render_view(
            scene,
            camera,
            output_dir,
            "tactical-20m",
            center,
            location=center + Vector((-8.9, -10.2, 5.1)).normalized() * 20.0,
            projection="PERSP",
            wire=wire,
            marker_geometry=marker_geometry,
            scale_geometry=scale_geometry,
        ),
    ]

    published_minimum = Vector((minimum.x, minimum.z, -maximum.y))
    published_maximum = Vector((maximum.x, maximum.z, -minimum.y))
    manifest = {
        "asset_id": ASSET_ID,
        "status": "provisional pending owner visual and Godot assembly review",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "render_profile": "vanguard-carbine-review-v1",
        "source_glb": str(glb_path),
        "source_glb_bytes": glb_path.stat().st_size,
        "import_result": sorted(import_result),
        "metrics_after_fresh_import": {
            "mesh_objects": len(meshes),
            "materials": materials,
            "material_count": len(materials),
            "images": [],
            "texture_sets": 0,
            "armatures": 0,
            "actions": 0,
            "bounds_min_blender_m": vec(minimum),
            "bounds_max_blender_m": vec(maximum),
            "bounds_size_blender_xyz_m": vec(size),
            "raw_import_topology": raw_topology,
            "welded_topology": welded_topology,
        },
        "published_coordinate_contract": {
            "up": "+Y",
            "forward": "-Z",
            "bounds_min_xyz_m": vec(published_minimum),
            "bounds_max_xyz_m": vec(published_maximum),
            "bounds_size_xyz_m": vec(published_maximum - published_minimum),
            "semantic_envelope_m": {
                "length": 0.82,
                "width": 0.13,
                "height": 0.27,
            },
        },
        "root_primary_offset_m": round(root_primary_offset, 10),
        "markers": marker_metrics,
        "render_preset": {
            "engine": scene.render.engine,
            "resolution": [RESOLUTION_X, RESOLUTION_Y],
            "view_transform": scene.view_settings.view_transform,
            "look": scene.view_settings.look,
            "exposure": scene.view_settings.exposure,
            "fov_degrees_for_tactical_views": 48.0,
        },
        "renders": renders,
        "validation_scope": (
            "Exact fresh Blender import, welded mechanical topology, coordinate "
            "and marker validation, and standardized Blender renders. Khronos "
            "glTF Validator and Godot import are separate checks."
        ),
    }
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    manifest["manifest_path"] = str(manifest_path)
    manifest["manifest_bytes"] = manifest_path.stat().st_size
    return manifest


weapon_glb, review_dir, review_manifest = parse_paths()
result = main(weapon_glb, review_dir, review_manifest)
print(json.dumps(result, indent=2, sort_keys=True))
