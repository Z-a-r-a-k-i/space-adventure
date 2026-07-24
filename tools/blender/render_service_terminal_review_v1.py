"""Render and measure the exact derived service-terminal review GLB.

Run with Blender 5.2:

    blender --background --factory-startup \
        --python tools/blender/render_service_terminal_review_v1.py

The script imports the derived GLB into a disposable scene. It does not modify
the authoring .blend or review GLB. Evidence is stored in ignored local staging
beneath a directory named for the GLB SHA-256, so reviews remain tied to exact
bytes without publishing an unapproved candidate into the game project.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


REPOSITORY = Path(
    os.environ.get(
        "SPACE_ADVENTURE_REPOSITORY",
        str(Path(__file__).resolve().parents[2]),
    )
).resolve()
ASSET_ID = os.environ.get(
    "SPACE_ADVENTURE_REVIEW_ASSET_ID",
    "prop.station.service_terminal.v1",
)
RUN_ID = (
    "prop.station.service_terminal.v1__tripo__"
    "v3.1-best-quality__2026-07-23__01"
)
GLB_PATH = Path(
    os.environ.get(
        "SPACE_ADVENTURE_REVIEW_GLB",
        str(
            REPOSITORY
            / "art"
            / "generated"
            / ASSET_ID
            / RUN_ID
            / "derived"
            / "v3"
            / (
                "prop.station.service_terminal.v1__clean__"
                "tripo-v3.1__candidate-01-v3.glb"
            )
        ),
    )
)
REVIEW_STATUS = os.environ.get(
    "SPACE_ADVENTURE_REVIEW_STATUS",
    "Tripo bake-off comparator; rejected at geometry hard gate",
)
FRONT_BLENDER_AXIS = os.environ.get(
    "SPACE_ADVENTURE_REVIEW_FRONT_BLENDER_AXIS",
    "+Y",
)
if FRONT_BLENDER_AXIS not in {"-Y", "+Y"}:
    raise RuntimeError(
        "SPACE_ADVENTURE_REVIEW_FRONT_BLENDER_AXIS must be -Y or +Y"
    )
REVIEW_ROOT = REPOSITORY / "artifacts" / "reviews" / ASSET_ID
RESOLUTION = 960


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    metallic: float = 0.0,
    roughness: float = 0.7,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def add_area_light(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    size: float,
    target: Vector,
) -> bpy.types.Object:
    light_data = bpy.data.lights.new(name=f"{name}.data", type="AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name=name, object_data=light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    point_at(light, target)
    return light


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                minimum[axis] = min(minimum[axis], world_corner[axis])
                maximum[axis] = max(maximum[axis], world_corner[axis])
    return minimum, maximum


def vec(values: Vector) -> list[float]:
    return [round(float(value), 6) for value in values]


def build_wire_overlay(
    meshes: list[bpy.types.Object],
    material: bpy.types.Material,
) -> list[bpy.types.Object]:
    curve_data = bpy.data.curves.new(name="review.wire.geometry", type="CURVE")
    curve_data.dimensions = "3D"
    curve_data.resolution_u = 1
    curve_data.bevel_depth = 0.0012
    curve_data.bevel_resolution = 0
    curve_data.resolution_u = 1
    curve_data.materials.append(material)

    for source in meshes:
        for edge in source.data.edges:
            spline = curve_data.splines.new(type="POLY")
            spline.points.add(1)
            for point_index, vertex_index in enumerate(edge.vertices):
                world_position = (
                    source.matrix_world @ source.data.vertices[vertex_index].co
                )
                spline.points[point_index].co = (*world_position, 1.0)

    overlay = bpy.data.objects.new(
        name="review.wire.overlay",
        object_data=curve_data,
    )
    bpy.context.scene.collection.objects.link(overlay)
    overlay.hide_render = True
    return [overlay]


def render_view(
    name: str,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    ortho_scale: float,
    output_dir: Path,
    camera: bpy.types.Object,
    floor: bpy.types.Object,
    wire_overlays: list[bpy.types.Object],
    *,
    floor_visible: bool = True,
    wire_visible: bool = False,
) -> dict[str, object]:
    camera.location = location
    point_at(camera, Vector(target))
    camera.data.ortho_scale = ortho_scale
    floor.hide_render = not floor_visible
    for overlay in wire_overlays:
        overlay.hide_render = not wire_visible

    output_path = output_dir / f"{name}.png"
    bpy.context.scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    if not output_path.is_file():
        raise RuntimeError(f"Blender did not create {output_path}")

    return {
        "file": output_path.name,
        "camera_location_blender_m": [round(float(value), 6) for value in location],
        "target_blender_m": [round(float(value), 6) for value in target],
        "projection": "orthographic",
        "ortho_scale_m": ortho_scale,
        "floor_visible": floor_visible,
        "wire_overlay": wire_visible,
        "bytes": output_path.stat().st_size,
        "sha256": sha256(output_path),
    }


def main() -> dict[str, object]:
    if not GLB_PATH.is_file():
        raise FileNotFoundError(GLB_PATH)

    glb_hash = sha256(GLB_PATH)
    output_dir = REVIEW_ROOT / glb_hash
    output_dir.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Service Terminal Derived GLB Review"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.render.filepath = str(output_dir)
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = bpy.data.worlds.new("review.world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.035,
        0.045,
        0.065,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.08
    scene.world = world

    import_result = bpy.ops.import_scene.gltf(
        filepath=str(GLB_PATH),
        import_pack_images=False,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()

    meshes = sorted(
        (obj for obj in scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    roots = [obj for obj in scene.objects if obj.parent is None and obj.type != "CAMERA"]
    if not 1 <= len(meshes) <= 4:
        raise RuntimeError(
            f"Expected one to four review mesh objects, found {len(meshes)}"
        )

    minimum, maximum = object_bounds(meshes)
    size = maximum - minimum
    center = (minimum + maximum) * 0.5
    triangles = 0
    vertices = 0
    mesh_stats: list[dict[str, object]] = []
    for obj in meshes:
        obj.data.calc_loop_triangles()
        mesh_triangles = len(obj.data.loop_triangles)
        mesh_vertices = len(obj.data.vertices)
        triangles += mesh_triangles
        vertices += mesh_vertices
        mesh_stats.append(
            {
                "name": obj.name,
                "vertices": mesh_vertices,
                "triangles": mesh_triangles,
                "materials": [
                    slot.material.name if slot.material else None
                    for slot in obj.material_slots
                ],
            }
        )

    camera_data = bpy.data.cameras.new("review.camera.data")
    camera_data.type = "ORTHO"
    camera_data.lens = 50.0
    camera_data.clip_start = 0.01
    camera_data.clip_end = 20.0
    camera = bpy.data.objects.new("review.camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    light_target = Vector((center.x, center.y, minimum.z + size.z * 0.52))
    front_sign = 1.0 if FRONT_BLENDER_AXIS == "+Y" else -1.0
    add_area_light(
        "review.key",
        (-2.4, 2.8 * front_sign, 3.4),
        180.0,
        2.3,
        light_target,
    )
    add_area_light(
        "review.fill",
        (2.8, 1.8 * front_sign, 2.1),
        70.0,
        2.4,
        light_target,
    )
    add_area_light(
        "review.rim",
        (0.6, -2.5 * front_sign, 3.0),
        130.0,
        2.0,
        light_target,
    )

    floor_material = make_material(
        "review.floor.material",
        (0.095, 0.115, 0.145, 1.0),
        metallic=0.0,
        roughness=0.82,
    )
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, minimum.z - 0.004))
    floor = bpy.context.active_object
    floor.name = "review.floor"
    floor.data.materials.append(floor_material)

    wire_material = make_material(
        "review.wire.material",
        (0.004, 0.006, 0.010, 1.0),
        metallic=0.0,
        roughness=0.9,
    )
    wire_overlays = build_wire_overlay(meshes, wire_material)
    bpy.context.view_layer.update()

    elevation_target = (center.x, center.y, minimum.z + size.z * 0.50)
    three_quarter_target = (center.x, center.y, minimum.z + size.z * 0.48)
    views = [
        (
            "front",
            (0.0, 3.0 * front_sign, elevation_target[2]),
            elevation_target,
            1.58,
            True,
            False,
        ),
        (
            "back",
            (0.0, -3.0 * front_sign, elevation_target[2]),
            elevation_target,
            1.58,
            True,
            False,
        ),
        ("left", (-3.0, 0.0, elevation_target[2]), elevation_target, 1.58, True, False),
        ("right", (3.0, 0.0, elevation_target[2]), elevation_target, 1.58, True, False),
        (
            "front-right-3q",
            (2.45, 2.75 * front_sign, 1.70),
            three_quarter_target,
            1.72,
            True,
            False,
        ),
        ("top", (0.0, 0.0, 4.0), (center.x, center.y, center.z), 1.02, False, False),
        (
            "underside",
            (0.0, 0.0, -3.0),
            (center.x, center.y, minimum.z + 0.08),
            1.02,
            False,
            False,
        ),
        (
            "wireframe-front-right-3q",
            (2.45, 2.75 * front_sign, 1.70),
            three_quarter_target,
            1.72,
            True,
            True,
        ),
    ]
    rendered = [
        render_view(
            name,
            location,
            target,
            ortho_scale,
            output_dir,
            camera,
            floor,
            wire_overlays,
            floor_visible=floor_visible,
            wire_visible=wire_visible,
        )
        for (
            name,
            location,
            target,
            ortho_scale,
            floor_visible,
            wire_visible,
        ) in views
    ]

    root_extras: dict[str, object] = {}
    asset_root = next(
        (obj for obj in roots if obj.get("asset_id") == ASSET_ID),
        None,
    )
    if asset_root is not None:
        root_extras = {str(key): value for key, value in asset_root.items()}

    published_minimum = Vector(
        (minimum.x, minimum.z, -maximum.y)
    )
    published_maximum = Vector(
        (maximum.x, maximum.z, -minimum.y)
    )
    published_size = published_maximum - published_minimum

    manifest = {
        "asset_id": ASSET_ID,
        "status": REVIEW_STATUS,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "source_glb": (
            str(GLB_PATH.relative_to(REPOSITORY)).replace("\\", "/")
            if GLB_PATH.is_relative_to(REPOSITORY)
            else str(GLB_PATH)
        ),
        "source_glb_bytes": GLB_PATH.stat().st_size,
        "source_glb_sha256": glb_hash,
        "import_result": sorted(import_result),
        "root_extras": root_extras,
        "validation_scope": (
            "Fresh Blender import plus render inspection; not Khronos glTF "
            "Validator conformance."
        ),
        "metrics_after_fresh_import": {
            "mesh_objects": len(meshes),
            "materials": sorted(
                {
                    slot.material.name
                    for obj in meshes
                    for slot in obj.material_slots
                    if slot.material
                }
            ),
            "material_count": len(
                {
                    slot.material.name
                    for obj in meshes
                    for slot in obj.material_slots
                    if slot.material
                }
            ),
            "vertices": vertices,
            "triangles": triangles,
            "bounds_min_blender_m": vec(minimum),
            "bounds_max_blender_m": vec(maximum),
            "bounds_size_blender_m": vec(size),
            "mesh_breakdown": mesh_stats,
        },
        "coordinate_interpretation": {
            "fresh_import_in_blender": (
                f"+Z up, {FRONT_BLENDER_AXIS} review front"
            ),
            "published_glb_contract": "+Y up, -Z front",
            "published_glb_bounds_m": {
                "min": vec(published_minimum),
                "max": vec(published_maximum),
                "size": vec(published_size),
            },
            "published_ground_plane": "Y = 0",
        },
        "render_preset": {
            "engine": scene.render.engine,
            "resolution": [RESOLUTION, RESOLUTION],
            "view_transform": scene.view_settings.view_transform,
            "look": scene.view_settings.look,
            "projection": "orthographic",
        },
        "renders": rendered,
    }
    manifest_path = output_dir / "render-manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    manifest["manifest"] = str(manifest_path)
    manifest["manifest_bytes"] = manifest_path.stat().st_size
    manifest["manifest_sha256"] = sha256(manifest_path)
    return manifest


result = main()
print(json.dumps(result, indent=2, sort_keys=True))
