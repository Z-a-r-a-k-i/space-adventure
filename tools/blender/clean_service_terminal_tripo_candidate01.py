"""Create a conservative bake-off candidate from the exact Tripo raw GLB.

The raw export is read-only. This script imports it into a factory scene,
normalizes the visual envelope, collapses the dense scan mesh below the brief's
4,000-triangle ceiling while preserving its UVs, downsizes the one texture set
to 1024 px, saves an editable packed .blend, and exports a candidate-specific
GLB. It deliberately does not overwrite the authored baseline or canonical GLB.
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
RAW_PATH = (
    RUN_ROOT
    / "raw"
    / "prop.station.service_terminal.v1__raw__tripo-v3.1__candidate-01.glb"
)
DERIVED_DIR = RUN_ROOT / "derived"
SOURCE_PATH = (
    REPOSITORY
    / "art"
    / "source"
    / ASSET_ID
    / "service-terminal-v1__tripo-candidate-01.blend"
)
DERIVED_GLB_PATH = (
    DERIVED_DIR
    / "prop.station.service_terminal.v1__clean__tripo-v3.1__candidate-01.glb"
)
REPORT_PATH = DERIVED_DIR / "cleanup-report.json"

EXPECTED_RAW_SHA256 = (
    "09249AE0F5D5201B684839C3ED81680F645161C99917117027F803BC28DB4CA0"
)
TARGET_SIZE_BLENDER = Vector((0.80, 0.42, 1.30))
TARGET_TRIANGLES = 3980
HARD_TRIANGLE_LIMIT = 4000
MAX_TEXTURE_SIZE = 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


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


def activate(obj: bpy.types.Object) -> None:
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


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
        "zero_area_faces": sum(1 for face in bm.faces if face.calc_area() <= 1e-12),
    }
    bm.free()
    return metrics


def main() -> dict[str, object]:
    total_started = time.perf_counter()
    if not RAW_PATH.is_file():
        raise FileNotFoundError(RAW_PATH)
    raw_hash = sha256(RAW_PATH)
    if raw_hash != EXPECTED_RAW_SHA256:
        raise RuntimeError(
            f"Raw candidate hash changed: {raw_hash} != {EXPECTED_RAW_SHA256}"
        )

    DERIVED_DIR.mkdir(parents=True, exist_ok=True)
    SOURCE_PATH.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Service Terminal Tripo Candidate 01 Cleanup"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"

    import_started = time.perf_counter()
    import_result = bpy.ops.import_scene.gltf(
        filepath=str(RAW_PATH),
        import_pack_images=True,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()
    import_seconds = time.perf_counter() - import_started

    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one raw mesh object, found {len(meshes)}")
    obj = meshes[0]
    activate(obj)

    raw_minimum, raw_maximum = bounds_for(obj)
    raw_size = raw_maximum - raw_minimum
    raw_triangles = triangle_count(obj)
    raw_vertices = len(obj.data.vertices)
    raw_materials = [
        slot.material.name if slot.material else None
        for slot in obj.material_slots
    ]
    raw_images = [
        {
            "name": image.name,
            "size": [int(image.size[0]), int(image.size[1])],
            "colorspace": image.colorspace_settings.name,
            "file_format": image.file_format,
        }
        for image in bpy.data.images
        if image.type == "IMAGE" and image.size[0] and image.size[1]
    ]

    normalize_started = time.perf_counter()
    center_xy = Vector(
        (
            (raw_minimum.x + raw_maximum.x) * 0.5,
            (raw_minimum.y + raw_maximum.y) * 0.5,
            0.0,
        )
    )
    obj.location -= center_xy
    obj.location.z -= raw_minimum.z
    obj.scale = Vector(
        (
            TARGET_SIZE_BLENDER.x / raw_size.x,
            TARGET_SIZE_BLENDER.y / raw_size.y,
            TARGET_SIZE_BLENDER.z / raw_size.z,
        )
    )
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()
    normalize_seconds = time.perf_counter() - normalize_started

    normalized_minimum, normalized_maximum = bounds_for(obj)
    normalized_size = normalized_maximum - normalized_minimum
    if any(
        abs(normalized_size[axis] - TARGET_SIZE_BLENDER[axis]) > 0.0005
        for axis in range(3)
    ):
        raise RuntimeError(
            f"Normalization failed: expected {tuple(TARGET_SIZE_BLENDER)}, "
            f"found {tuple(normalized_size)}"
        )
    if abs(normalized_minimum.z) > 0.0005:
        raise RuntimeError(
            f"Ground-plane pivot contract failed: min Z is {normalized_minimum.z}"
        )

    weld_started = time.perf_counter()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    vertices_before_weld = len(bm.verts)
    bmesh.ops.remove_doubles(
        bm,
        verts=list(bm.verts),
        dist=1e-6,
    )
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    vertices_after_weld = len(obj.data.vertices)
    weld_seconds = time.perf_counter() - weld_started

    decimate_started = time.perf_counter()
    decimate = obj.modifiers.new(name="ProductionRetopology", type="DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = min(1.0, TARGET_TRIANGLES / raw_triangles)
    decimate.use_collapse_triangulate = True
    decimate.use_symmetry = False
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=decimate.name)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
    wire_edges = [edge for edge in bm.edges if edge.is_wire]
    if wire_edges:
        bmesh.ops.delete(bm, geom=wire_edges, context="EDGES")
    loose_vertices = [vertex for vertex in bm.verts if not vertex.link_edges]
    if loose_vertices:
        bmesh.ops.delete(bm, geom=loose_vertices, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()

    activate(obj)
    bpy.ops.object.shade_smooth_by_angle(
        angle=math.radians(48.0),
        keep_sharp_edges=True,
    )
    bpy.context.view_layer.update()
    decimate_seconds = time.perf_counter() - decimate_started

    cleaned_triangles = triangle_count(obj)
    if cleaned_triangles > HARD_TRIANGLE_LIMIT:
        raise RuntimeError(
            f"Retopology exceeded hard limit: {cleaned_triangles} triangles"
        )
    if not obj.data.uv_layers:
        raise RuntimeError("Retopology lost the source UV map")

    final_normalize_started = time.perf_counter()
    post_decimate_minimum, post_decimate_maximum = bounds_for(obj)
    post_decimate_size = post_decimate_maximum - post_decimate_minimum
    obj.location.x -= (post_decimate_minimum.x + post_decimate_maximum.x) * 0.5
    obj.location.y -= (post_decimate_minimum.y + post_decimate_maximum.y) * 0.5
    obj.location.z -= post_decimate_minimum.z
    obj.scale = Vector(
        (
            TARGET_SIZE_BLENDER.x / post_decimate_size.x,
            TARGET_SIZE_BLENDER.y / post_decimate_size.y,
            TARGET_SIZE_BLENDER.z / post_decimate_size.z,
        )
    )
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()
    final_normalize_seconds = time.perf_counter() - final_normalize_started

    texture_started = time.perf_counter()
    texture_operations: list[dict[str, object]] = []
    for image in bpy.data.images:
        if image.type != "IMAGE" or not image.size[0] or not image.size[1]:
            continue
        before = [int(image.size[0]), int(image.size[1])]
        if image.size[0] > MAX_TEXTURE_SIZE or image.size[1] > MAX_TEXTURE_SIZE:
            scale = min(
                MAX_TEXTURE_SIZE / image.size[0],
                MAX_TEXTURE_SIZE / image.size[1],
            )
            width = max(1, round(image.size[0] * scale))
            height = max(1, round(image.size[1] * scale))
            image.scale(width, height)
        image.pack()
        texture_operations.append(
            {
                "name": image.name,
                "before": before,
                "after": [int(image.size[0]), int(image.size[1])],
                "colorspace": image.colorspace_settings.name,
                "packed": bool(image.packed_file),
            }
        )
    texture_seconds = time.perf_counter() - texture_started

    obj.name = ASSET_ID
    obj.data.name = f"{ASSET_ID}.mesh"
    obj["asset_id"] = ASSET_ID
    obj["authoring_up"] = "+Z"
    obj["authoring_front"] = "-Y"
    obj["published_up"] = "+Y"
    obj["published_front"] = "-Z"
    obj["selection_status"] = (
        "tripo-candidate-01-bakeoff-rejected-geometry"
    )
    obj["raw_sha256"] = raw_hash
    obj["retopology"] = (
        "Weld coincident source vertices; Blender Decimate Collapse; "
        "UV-preserving target 3980"
    )

    if len(obj.material_slots) != 1 or obj.material_slots[0].material is None:
        raise RuntimeError(
            f"Expected one generated material, found {len(obj.material_slots)}"
        )
    material = obj.material_slots[0].material
    material.name = "mat.station.generated.candidate01"
    for image in bpy.data.images:
        if image.type != "IMAGE":
            continue
        lowered = image.name.lower()
        if "basecolor" in lowered:
            image.name = f"{ASSET_ID}.candidate01.basecolor"
        elif "normal" in lowered:
            image.name = f"{ASSET_ID}.candidate01.normal"
        elif "_rm" in lowered or lowered.endswith("rm.jpg"):
            image.name = f"{ASSET_ID}.candidate01.roughness-metallic"

    topology = topology_metrics(obj.data)
    cleaned_minimum, cleaned_maximum = bounds_for(obj)
    cleaned_size = cleaned_maximum - cleaned_minimum

    save_started = time.perf_counter()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(
        filepath=str(SOURCE_PATH),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )
    save_seconds = time.perf_counter() - save_started

    activate(obj)
    export_started = time.perf_counter()
    export_result = bpy.ops.export_scene.gltf(
        filepath=str(DERIVED_GLB_PATH),
        check_existing=False,
        export_format="GLB",
        export_copyright=(
            "SpaceAdventure provisional Tripo candidate; "
            f"raw SHA-256 {raw_hash}"
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
            "generated candidate 01 cleanup comparator; rejected at geometry "
            "hard gate"
        ),
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "raw": {
            "path": str(RAW_PATH.relative_to(REPOSITORY)).replace("\\", "/"),
            "bytes": RAW_PATH.stat().st_size,
            "sha256": raw_hash,
            "vertices": raw_vertices,
            "triangles": raw_triangles,
            "materials": raw_materials,
            "images": raw_images,
            "bounds_min_blender_m": vec(raw_minimum),
            "bounds_max_blender_m": vec(raw_maximum),
            "bounds_size_blender_m": vec(raw_size),
        },
        "operations": [
            "fresh import of exact raw GLB",
            "center X/Y and place lowest support at Blender Z=0",
            "apply non-uniform normalization to 0.80 x 0.42 x 1.30 m",
            (
                "weld coincident source vertices at 0.000001 m while "
                "retaining per-loop UV data"
            ),
            "Decimate Collapse to a 3,980-triangle target with UV retention",
            (
                "dissolve degenerate edges, remove wire-only geometry, "
                "and recalculate face normals"
            ),
            "smooth by 48-degree angle while retaining sharp edges",
            "re-apply the exact envelope and ground contact after reduction",
            "downsize the single generated texture set from 4096 to 1024",
            "pack editable source and export candidate-specific GLB",
        ],
        "cleaned": {
            "source_blend": str(SOURCE_PATH.relative_to(REPOSITORY)).replace(
                "\\", "/"
            ),
            "source_blend_bytes": SOURCE_PATH.stat().st_size,
            "source_blend_sha256": sha256(SOURCE_PATH),
            "derived_glb": str(
                DERIVED_GLB_PATH.relative_to(REPOSITORY)
            ).replace("\\", "/"),
            "derived_glb_bytes": DERIVED_GLB_PATH.stat().st_size,
            "derived_glb_sha256": sha256(DERIVED_GLB_PATH),
            "export_result": sorted(export_result),
            "vertices": len(obj.data.vertices),
            "triangles": cleaned_triangles,
            "mesh_objects": 1,
            "materials": [material.name],
            "material_count": 1,
            "texture_operations": texture_operations,
            "weld": {
                "vertices_before": vertices_before_weld,
                "vertices_after": vertices_after_weld,
                "merged": vertices_before_weld - vertices_after_weld,
                "distance_m": 1e-6,
            },
            "bounds_min_blender_m": vec(cleaned_minimum),
            "bounds_max_blender_m": vec(cleaned_maximum),
            "bounds_size_blender_m": vec(cleaned_size),
            "topology": topology,
            "uv_layers": [layer.name for layer in obj.data.uv_layers],
        },
        "timings_seconds": {
            "import": round(import_seconds, 3),
            "normalize": round(normalize_seconds, 3),
            "weld": round(weld_seconds, 3),
            "decimate_and_normals": round(decimate_seconds, 3),
            "final_normalize": round(final_normalize_seconds, 3),
            "texture_resize": round(texture_seconds, 3),
            "save_blend": round(save_seconds, 3),
            "export_glb": round(export_seconds, 3),
            "total": round(time.perf_counter() - total_started, 3),
        },
        "fallback": (
            "The authored comparator remains unchanged at "
            "art/source/prop.station.service_terminal.v1/"
            "service-terminal-v1.blend; the Godot gallery keeps its "
            "independent CSG greybox fallback."
        ),
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
