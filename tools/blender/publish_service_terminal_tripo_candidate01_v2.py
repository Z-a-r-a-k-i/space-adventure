"""Publish the reversible service-terminal material fix as candidate 01 v2.

The retained v1 editable source is opened read-only by the Blender process.
This script duplicates and lifts the generated base-color texture, isolates
the purple screen faces into ``mat.state.optional.violet``, and saves/exports
new v2 artifacts. Geometry is intentionally unchanged.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import time
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector


REPOSITORY = Path(
    os.environ.get(
        "SPACE_ADVENTURE_REPOSITORY",
        str(Path(__file__).resolve().parents[2]),
    )
).resolve()
ASSET_ID = "prop.station.service_terminal.v1"
RUN_ID = (
    "prop.station.service_terminal.v1__tripo__"
    "v3.1-best-quality__2026-07-23__01"
)
RUN_ROOT = REPOSITORY / "art" / "generated" / ASSET_ID / RUN_ID
V1_SOURCE = (
    REPOSITORY
    / "art"
    / "source"
    / ASSET_ID
    / "service-terminal-v1__tripo-candidate-01.blend"
)
V2_SOURCE = (
    REPOSITORY
    / "art"
    / "source"
    / ASSET_ID
    / "service-terminal-v1__tripo-candidate-01-v2.blend"
)
V2_DERIVED_DIR = RUN_ROOT / "derived" / "v2"
V2_GLB = (
    V2_DERIVED_DIR
    / "prop.station.service_terminal.v1__clean__tripo-v3.1__candidate-01-v2.glb"
)
REPORT_PATH = V2_DERIVED_DIR / "material-fix-report.json"

EXPECTED_V1_SOURCE_SHA256 = (
    "1701568CDC6647324E5A6EF90EE307065E4EE4B397724522B42CED7A568C95A3"
)
EXPECTED_RAW_SHA256 = (
    "09249AE0F5D5201B684839C3ED81680F645161C99917117027F803BC28DB4CA0"
)
TARGET_SIZE = Vector((0.80, 0.42, 1.30))
VIOLET = (0.30, 0.055, 0.72, 1.0)
VIOLET_EMISSION_STRENGTH = 1.8


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def activate(obj: bpy.types.Object) -> None:
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bounds_for(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for corner in obj.bound_box:
        world_corner = obj.matrix_world @ Vector(corner)
        for axis in range(3):
            minimum[axis] = min(minimum[axis], world_corner[axis])
            maximum[axis] = max(maximum[axis], world_corner[axis])
    return minimum, maximum


def vec(values: Vector) -> list[float]:
    return [round(float(value), 6) for value in values]


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def topology_metrics(mesh: bpy.types.Mesh) -> dict[str, int]:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    metrics = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(1 for edge in bm.edges if not edge.is_manifold),
        "wire_edges": sum(1 for edge in bm.edges if edge.is_wire),
        "loose_vertices": sum(1 for vertex in bm.verts if not vertex.link_edges),
        "zero_area_faces": sum(
            1 for face in bm.faces if face.calc_area() <= 1e-12
        ),
    }
    bm.free()
    return metrics


def find_images(
    material: bpy.types.Material,
) -> tuple[bpy.types.Image, list[bpy.types.Image], list[bpy.types.Node]]:
    if not material.use_nodes or material.node_tree is None:
        raise RuntimeError("Generated housing material has no node tree")
    image_nodes = [
        node
        for node in material.node_tree.nodes
        if node.bl_idname == "ShaderNodeTexImage" and node.image is not None
    ]
    srgb_nodes = [
        node
        for node in image_nodes
        if node.image.colorspace_settings.name == "sRGB"
    ]
    if len(srgb_nodes) != 1:
        raise RuntimeError(
            f"Expected one sRGB base-color node, found {len(srgb_nodes)}"
        )
    base_image = srgb_nodes[0].image
    other_images = [
        node.image for node in image_nodes if node.image != base_image
    ]
    return base_image, other_images, srgb_nodes


def image_pixels(image: bpy.types.Image) -> np.ndarray:
    count = int(image.size[0] * image.size[1] * 4)
    pixels = np.empty(count, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return pixels.reshape((-1, 4))


def sample_rgb(
    uv: Vector,
    pixels: np.ndarray,
    width: int,
    height: int,
) -> tuple[float, float, float]:
    u = float(uv.x % 1.0)
    v = float(uv.y % 1.0)
    x = min(width - 1, max(0, int(u * (width - 1))))
    y = min(height - 1, max(0, int(v * (height - 1))))
    rgb = pixels[y * width + x, :3]
    return float(rgb[0]), float(rgb[1]), float(rgb[2])


def sample_face_rgbs(
    polygon: bpy.types.MeshPolygon,
    uv_data: bpy.types.MeshUVLoopLayer,
    pixels: np.ndarray,
    width: int,
    height: int,
) -> list[tuple[float, float, float]]:
    vertices = [uv_data[index].uv.copy() for index in polygon.loop_indices]
    uv_centroid = Vector((0.0, 0.0))
    for uv in vertices:
        uv_centroid += uv
    uv_centroid /= len(vertices)
    sample_points = [uv_centroid] + [
        uv_centroid + (uv - uv_centroid) * 0.75 for uv in vertices
    ]
    return [
        sample_rgb(uv, pixels, width, height)
        for uv in sample_points
    ]


def make_violet_material() -> bpy.types.Material:
    material = bpy.data.materials.new(name="mat.state.optional.violet")
    material.diffuse_color = VIOLET
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = VIOLET
    principled.inputs["Metallic"].default_value = 0.04
    principled.inputs["Roughness"].default_value = 0.32
    principled.inputs["Emission Color"].default_value = VIOLET
    principled.inputs["Emission Strength"].default_value = (
        VIOLET_EMISSION_STRENGTH
    )
    return material


def main() -> dict[str, object]:
    started = time.perf_counter()
    if Path(bpy.data.filepath) != V1_SOURCE:
        raise RuntimeError(
            f"Expected Blender to open {V1_SOURCE}, opened {bpy.data.filepath}"
        )
    source_hash = sha256(V1_SOURCE)
    if source_hash != EXPECTED_V1_SOURCE_SHA256:
        raise RuntimeError(
            f"V1 source changed: {source_hash} != {EXPECTED_V1_SOURCE_SHA256}"
        )

    V2_DERIVED_DIR.mkdir(parents=True, exist_ok=True)
    V2_SOURCE.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.name = "Service Terminal Tripo Candidate 01 v2"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh object, found {len(meshes)}")
    obj = meshes[0]
    activate(obj)
    triangles_before = triangle_count(obj)
    vertices_before = len(obj.data.vertices)
    bounds_min_before, bounds_max_before = bounds_for(obj)
    topology_before = topology_metrics(obj.data)

    if len(obj.material_slots) != 1 or obj.material_slots[0].material is None:
        raise RuntimeError(
            f"Expected one v1 generated material, found {len(obj.material_slots)}"
        )
    housing = obj.material_slots[0].material
    base_image, other_images, base_nodes = find_images(housing)
    if tuple(base_image.size) != (1024, 1024):
        raise RuntimeError(f"Expected 1024² base color, found {tuple(base_image.size)}")

    original_pixels = image_pixels(base_image)
    uv_layer = obj.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("V1 source has no active UV layer")

    selected_faces: list[int] = []
    screen_min = Vector((float("inf"), float("inf"), float("inf")))
    screen_max = Vector((float("-inf"), float("-inf"), float("-inf")))
    screen_area = 0.0
    normal_matrix = obj.matrix_world.to_3x3()
    for polygon in obj.data.polygons:
        center = obj.matrix_world @ polygon.center
        normal = (normal_matrix @ polygon.normal).normalized()
        samples = sample_face_rgbs(
            polygon,
            uv_layer.data,
            original_pixels,
            int(base_image.size[0]),
            int(base_image.size[1]),
        )
        in_screen_envelope = (
            abs(center.x) <= 0.180
            and 0.800 <= center.z <= 1.080
            and center.y <= -0.120
            and normal.y <= -0.20
        )
        violet_sample_count = sum(
            1
            for red, green, blue in samples
            if (
                blue > 0.15
                and blue > red * 1.04
                and red > green * 1.08
            )
        )
        if not (in_screen_envelope and violet_sample_count >= 2):
            continue
        selected_faces.append(polygon.index)
        screen_area += polygon.area
        for vertex_index in polygon.vertices:
            world_vertex = obj.matrix_world @ obj.data.vertices[vertex_index].co
            for axis in range(3):
                screen_min[axis] = min(screen_min[axis], world_vertex[axis])
                screen_max[axis] = max(screen_max[axis], world_vertex[axis])

    if not 15 <= len(selected_faces) <= 80:
        raise RuntimeError(
            f"Screen selection is implausible: {len(selected_faces)} faces"
        )
    if not 0.025 <= screen_area <= 0.15:
        raise RuntimeError(
            f"Screen selection area is implausible: {screen_area} m²"
        )

    housing.name = "mat.station.generated.candidate01.v2"
    lifted_image = base_image.copy()
    lifted_image.name = f"{ASSET_ID}.candidate01.v2.basecolor"
    lifted_pixels = original_pixels.copy()
    lifted_pixels[:, :3] = np.clip(
        np.power(np.maximum(lifted_pixels[:, :3], 0.0), 0.82) * 0.95 + 0.015,
        0.0,
        1.0,
    )
    lifted_image.pixels.foreach_set(lifted_pixels.reshape(-1))
    lifted_image.update()
    lifted_image.pack()
    for node in base_nodes:
        node.image = lifted_image
    if base_image.users == 0:
        bpy.data.images.remove(base_image)

    violet = make_violet_material()
    obj.data.materials.append(violet)
    violet_index = len(obj.data.materials) - 1
    for polygon_index in selected_faces:
        obj.data.polygons[polygon_index].material_index = violet_index

    obj["selection_status"] = (
        "tripo-candidate-01-v2-bakeoff-rejected-geometry"
    )
    obj["material_revision"] = "v2"
    obj["screen_material"] = "mat.state.optional.violet"
    obj["screen_emission_strength"] = VIOLET_EMISSION_STRENGTH
    obj["housing_shadow_lift"] = "rgb = pow(rgb, 0.82) * 0.95 + 0.015"
    obj["raw_sha256"] = EXPECTED_RAW_SHA256

    bpy.context.view_layer.update()
    triangles_after = triangle_count(obj)
    vertices_after = len(obj.data.vertices)
    bounds_min_after, bounds_max_after = bounds_for(obj)
    bounds_size_after = bounds_max_after - bounds_min_after
    topology_after = topology_metrics(obj.data)

    if triangles_after != triangles_before or triangles_after > 4000:
        raise RuntimeError(
            f"Geometry changed unexpectedly: {triangles_before} -> {triangles_after}"
        )
    if vertices_after != vertices_before:
        raise RuntimeError(
            f"Vertex count changed unexpectedly: {vertices_before} -> {vertices_after}"
        )
    if any(
        abs(bounds_size_after[axis] - TARGET_SIZE[axis]) > 0.0005
        for axis in range(3)
    ):
        raise RuntimeError(f"Envelope changed: {tuple(bounds_size_after)}")
    if abs(bounds_min_after.z) > 0.0005:
        raise RuntimeError(f"Ground contact changed: min Z {bounds_min_after.z}")
    if topology_after != topology_before:
        raise RuntimeError(
            f"Topology changed unexpectedly: {topology_before} -> {topology_after}"
        )
    if len(obj.material_slots) > 3:
        raise RuntimeError(f"Material limit exceeded: {len(obj.material_slots)}")

    save_started = time.perf_counter()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(
        filepath=str(V2_SOURCE),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )
    save_seconds = time.perf_counter() - save_started

    activate(obj)
    export_started = time.perf_counter()
    export_result = bpy.ops.export_scene.gltf(
        filepath=str(V2_GLB),
        check_existing=False,
        export_format="GLB",
        export_copyright=(
            "SpaceAdventure provisional Tripo candidate 01 v2; "
            f"raw SHA-256 {EXPECTED_RAW_SHA256}"
        ),
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
        export_image_format="JPEG",
        export_jpeg_quality=88,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_skins=False,
        export_morph=False,
        export_unused_images=False,
        export_unused_textures=False,
        export_vertex_color="MATERIAL",
        export_all_vertex_colors=False,
        export_draco_mesh_compression_enable=False,
        export_meshopt_compression_enable=False,
        use_selection=True,
        export_extras=True,
    )
    export_seconds = time.perf_counter() - export_started

    result = {
        "asset_id": ASSET_ID,
        "status": (
            "candidate 01 v2 material comparator; rejected at geometry hard "
            "gate"
        ),
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "input": {
            "v1_source": str(V1_SOURCE.relative_to(REPOSITORY)).replace("\\", "/"),
            "v1_source_bytes": V1_SOURCE.stat().st_size,
            "v1_source_sha256": source_hash,
            "raw_sha256": EXPECTED_RAW_SHA256,
        },
        "screen_selection": {
            "faces": len(selected_faces),
            "face_indices": selected_faces,
            "surface_area_m2": round(screen_area, 6),
            "bounds_min_blender_m": vec(screen_min),
            "bounds_max_blender_m": vec(screen_max),
            "criteria": {
                "center_abs_x_max": 0.180,
                "center_z_range": [0.800, 1.080],
                "center_y_max": -0.120,
                "normal_y_max": -0.20,
                "texture_samples_per_face": 4,
                "texture_samples_required": 2,
                "texture_blue_min": 0.15,
                "texture_blue_vs_red": 1.04,
                "texture_red_vs_green": 1.08,
            },
        },
        "materials": [
            {
                "name": housing.name,
                "role": "generated housing/trim/cyan texture set",
                "base_color_revision": (
                    "shadow lift: rgb = pow(rgb, 0.82) * 0.95 + 0.015"
                ),
            },
            {
                "name": violet.name,
                "role": "optional-state screen",
                "base_color": list(VIOLET),
                "emission_color": list(VIOLET),
                "emission_strength": VIOLET_EMISSION_STRENGTH,
                "metallic": 0.04,
                "roughness": 0.32,
            },
        ],
        "images": [
            {
                "name": image.name,
                "size": [int(image.size[0]), int(image.size[1])],
                "colorspace": image.colorspace_settings.name,
                "packed": bool(image.packed_file),
            }
            for image in bpy.data.images
            if image.type == "IMAGE" and image.users > 0
        ],
        "geometry": {
            "vertices": vertices_after,
            "triangles": triangles_after,
            "bounds_min_blender_m": vec(bounds_min_after),
            "bounds_max_blender_m": vec(bounds_max_after),
            "bounds_size_blender_m": vec(bounds_size_after),
            "topology": topology_after,
            "topology_changed": False,
            "topology_repair_decision": (
                "Preserved. Automated repair was not safe because the remaining "
                "provider-shell openings and multi-face edges could change the "
                "approved silhouette or UV projection."
            ),
        },
        "outputs": {
            "v2_source": str(V2_SOURCE.relative_to(REPOSITORY)).replace("\\", "/"),
            "v2_source_bytes": V2_SOURCE.stat().st_size,
            "v2_source_sha256": sha256(V2_SOURCE),
            "v2_derived_glb": str(V2_GLB.relative_to(REPOSITORY)).replace("\\", "/"),
            "v2_derived_glb_bytes": V2_GLB.stat().st_size,
            "v2_derived_glb_sha256": sha256(V2_GLB),
            "export_result": sorted(export_result),
        },
        "timings_seconds": {
            "save_source": round(save_seconds, 3),
            "export_glb": round(export_seconds, 3),
            "total": round(time.perf_counter() - started, 3),
        },
        "preservation": {
            "v1_source_overwritten": False,
            "v1_glb_overwritten": False,
            "raw_overwritten": False,
        },
    }
    REPORT_PATH.write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    result["report"] = str(REPORT_PATH)
    return result


result = main()
print(json.dumps(result, indent=2, sort_keys=True))
