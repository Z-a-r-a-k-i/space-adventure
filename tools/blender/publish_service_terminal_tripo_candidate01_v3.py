"""Correct candidate 01's retained v2 source without touching retained inputs.

The editable source is Blender Z-up while the GLB is exported Y-up. Rotating
the mesh by pi around Blender +Z therefore becomes the required review
rotation around exported +Y. Geometry, topology, UVs, packed images, and
materials are otherwise preserved byte-for-byte where applicable. Transient
v1/v2 GLBs and earlier staged copies are not required to reproduce the final
v3 outputs.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import struct
import time
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


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
V2_SOURCE = (
    REPOSITORY
    / "art"
    / "source"
    / ASSET_ID
    / "service-terminal-v1__tripo-candidate-01-v2.blend"
)
V3_SOURCE = (
    REPOSITORY
    / "art"
    / "source"
    / ASSET_ID
    / "service-terminal-v1__tripo-candidate-01-v3.blend"
)
V3_DIR = RUN_ROOT / "derived" / "v3"
V3_GLB = (
    V3_DIR
    / "prop.station.service_terminal.v1__clean__tripo-v3.1__candidate-01-v3.glb"
)
REPORT_PATH = V3_DIR / "orientation-fix-report.json"

RAW_GLB = (
    RUN_ROOT
    / "raw"
    / "prop.station.service_terminal.v1__raw__tripo-v3.1__candidate-01.glb"
)

EXPECTED_HASHES = {
    RAW_GLB: "09249AE0F5D5201B684839C3ED81680F645161C99917117027F803BC28DB4CA0",
    V2_SOURCE: "BCC9F1F39133AF3696856EAE1DEB21EF087B9F358B86DFB46B47E6D62926364A",
}
EXPECTED_MATERIALS = [
    "mat.station.generated.candidate01.v2",
    "mat.state.optional.violet",
]
EXPECTED_GLTF_IMAGES_BY_PACKED_SHA256 = {
    "9270E4EEC944F3C5C7EB75EE5875DC43450D6A397882872586068A0D8A390607": {
        "index": 0,
        "mime_type": "image/jpeg",
        "bytes": 70352,
        "sha256": "567ABF6CF655AFE58784CA5F05D251ECD3F99A8E1CF8569A5EFE653E624BC649",
    },
    "DACC3B327D003AAC7561BE515B7F7AD993821A62A4A422277B90E63C66FE2679": {
        "index": 1,
        "mime_type": "image/jpeg",
        "bytes": 128653,
        "sha256": "63B6F3FA0B8D9A7C87AD6F223381CE905725E800DE625A269B33FD36A045F48F",
    },
    "78B5A06F25E6A0B39272B629E90E8150ACA4FEBEFC806E6A68CB094768E465C7": {
        "index": 2,
        "mime_type": "image/jpeg",
        "bytes": 66840,
        "sha256": "7669A01808AC3E02347D741711B968DF9E65AEAAA2C8E228ADE2A4AC2EF4F180",
    },
}
TARGET_SIZE = Vector((0.80, 0.42, 1.30))


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
    result = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(
            1 for edge in bm.edges if not edge.is_manifold
        ),
        "wire_edges": sum(1 for edge in bm.edges if edge.is_wire),
        "loose_vertices": sum(1 for vertex in bm.verts if not vertex.link_edges),
        "zero_area_faces": sum(
            1 for face in bm.faces if face.calc_area() <= 1e-12
        ),
    }
    bm.free()
    return result


def material_names(obj: bpy.types.Object) -> list[str]:
    return [
        slot.material.name
        for slot in obj.material_slots
        if slot.material is not None
    ]


def packed_image_hashes() -> dict[str, str]:
    result: dict[str, str] = {}
    for image in bpy.data.images:
        if image.type != "IMAGE" or image.users == 0:
            continue
        if image.packed_file is None:
            raise RuntimeError(f"Image is not packed: {image.name}")
        result[image.name] = hashlib.sha256(
            bytes(image.packed_file.data)
        ).hexdigest().upper()
    return dict(sorted(result.items()))


def expected_glb_images(
    packed_images: dict[str, str],
) -> list[dict[str, object]]:
    packed_hashes = set(packed_images.values())
    expected_hashes = set(EXPECTED_GLTF_IMAGES_BY_PACKED_SHA256)
    if packed_hashes != expected_hashes:
        raise RuntimeError(
            "Retained v2 packed image hashes changed: "
            f"expected {sorted(expected_hashes)}, found {sorted(packed_hashes)}"
        )
    return sorted(
        (
            dict(EXPECTED_GLTF_IMAGES_BY_PACKED_SHA256[digest])
            for digest in packed_images.values()
        ),
        key=lambda image: int(image["index"]),
    )


def violet_export_expectation(
    obj: bpy.types.Object,
) -> dict[str, object]:
    violet = next(
        (
            slot.material
            for slot in obj.material_slots
            if slot.material is not None
            and slot.material.name == "mat.state.optional.violet"
        ),
        None,
    )
    if violet is None or violet.node_tree is None:
        raise RuntimeError("Retained v2 violet material has no node tree")
    principled_nodes = [
        node
        for node in violet.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    ]
    if len(principled_nodes) != 1:
        raise RuntimeError(
            "Expected one Principled BSDF in retained v2 violet material, "
            f"found {len(principled_nodes)}"
        )
    principled = principled_nodes[0]
    emission_color = [
        float(value)
        for value in principled.inputs["Emission Color"].default_value[:3]
    ]
    emission_strength = float(
        principled.inputs["Emission Strength"].default_value
    )
    maximum = max(emission_color)
    if maximum <= 0.0 or emission_strength <= 0.0:
        raise RuntimeError(
            "Retained v2 violet material has no positive emission"
        )
    return {
        "factor": [value / maximum for value in emission_color],
        "strength": maximum * emission_strength,
    }


def parse_glb(path: Path) -> tuple[dict[str, object], bytes]:
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<III", data, 0)
    if magic != 0x46546C67 or version != 2 or length != len(data):
        raise RuntimeError(f"Invalid GLB header: {path}")
    offset = 12
    document: dict[str, object] | None = None
    binary = b""
    while offset < len(data):
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset : offset + chunk_length]
        offset += chunk_length
        if chunk_type == 0x4E4F534A:
            document = json.loads(chunk.rstrip(b" \t\r\n\0"))
        elif chunk_type == 0x004E4942:
            binary = chunk
    if document is None:
        raise RuntimeError(f"GLB has no JSON chunk: {path}")
    return document, binary


def glb_image_hashes(path: Path) -> list[dict[str, object]]:
    document, binary = parse_glb(path)
    buffer_views = document.get("bufferViews", [])
    result: list[dict[str, object]] = []
    for index, image in enumerate(document.get("images", [])):
        view = buffer_views[image["bufferView"]]
        start = int(view.get("byteOffset", 0))
        end = start + int(view["byteLength"])
        payload = binary[start:end]
        result.append(
            {
                "index": index,
                "mime_type": image.get("mimeType"),
                "bytes": len(payload),
                "sha256": hashlib.sha256(payload).hexdigest().upper(),
            }
        )
    return result


def glb_summary(path: Path) -> dict[str, object]:
    document, _ = parse_glb(path)
    materials = document.get("materials", [])
    accessors = document.get("accessors", [])
    meshes = document.get("meshes", [])
    primitives: list[dict[str, object]] = []
    for mesh in meshes:
        for primitive in mesh.get("primitives", []):
            material_index = int(primitive.get("material", -1))
            material_name = (
                materials[material_index].get("name")
                if material_index >= 0
                else None
            )
            position_accessor = accessors[primitive["attributes"]["POSITION"]]
            primitives.append(
                {
                    "material": material_name,
                    "position_min": position_accessor.get("min"),
                    "position_max": position_accessor.get("max"),
                    "index_count": accessors[primitive["indices"]]["count"],
                }
            )
    violet_material = next(
        material
        for material in materials
        if material.get("name") == "mat.state.optional.violet"
    )
    violet_primitive = next(
        primitive
        for primitive in primitives
        if primitive["material"] == "mat.state.optional.violet"
    )
    return {
        "materials": [material.get("name") for material in materials],
        "primitives": primitives,
        "violet_primitive": violet_primitive,
        "violet_emissive_factor": violet_material.get("emissiveFactor"),
        "violet_emissive_strength": (
            violet_material
            .get("extensions", {})
            .get("KHR_materials_emissive_strength", {})
            .get("emissiveStrength")
        ),
        "images": glb_image_hashes(path),
        "cameras": len(document.get("cameras", [])),
        "animations": len(document.get("animations", [])),
        "skins": len(document.get("skins", [])),
        "lights": len(
            document
            .get("extensions", {})
            .get("KHR_lights_punctual", {})
            .get("lights", [])
        ),
    }


def verify_preserved_files() -> dict[str, dict[str, object]]:
    result: dict[str, dict[str, object]] = {}
    for path, expected in EXPECTED_HASHES.items():
        actual = sha256(path)
        if actual != expected:
            raise RuntimeError(f"Preserved input changed: {path}: {actual}")
        result[
            str(path.relative_to(REPOSITORY)).replace("\\", "/")
        ] = {
            "bytes": path.stat().st_size,
            "sha256": actual,
        }
    return result


def main() -> dict[str, object]:
    started = time.perf_counter()
    if Path(bpy.data.filepath) != V2_SOURCE:
        raise RuntimeError(
            f"Expected Blender to open {V2_SOURCE}, opened {bpy.data.filepath}"
        )
    if V3_SOURCE.exists() or V3_GLB.exists() or REPORT_PATH.exists():
        raise RuntimeError("V3 outputs already exist; refusing to overwrite")

    preserved_before = verify_preserved_files()
    V3_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.name = "Service Terminal Tripo Candidate 01 v3"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh object, found {len(meshes)}")
    obj = meshes[0]
    if obj.data.users != 1:
        raise RuntimeError(f"Expected single-user mesh data, found {obj.data.users}")
    activate(obj)

    triangles_before = triangle_count(obj)
    topology_before = topology_metrics(obj.data)
    bounds_min_before, bounds_max_before = bounds_for(obj)
    materials_before = material_names(obj)
    packed_images_before = packed_image_hashes()
    expected_export_images = expected_glb_images(packed_images_before)
    violet_export_before = violet_export_expectation(obj)
    if triangles_before != 3979:
        raise RuntimeError(f"Unexpected v2 triangle count: {triangles_before}")
    if materials_before != EXPECTED_MATERIALS:
        raise RuntimeError(f"Unexpected v2 materials: {materials_before}")
    if len(packed_images_before) != 3:
        raise RuntimeError(
            f"Expected three packed images, found {len(packed_images_before)}"
        )

    # Blender +Z is exported +Y. This is the requested review +Y turn.
    obj.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
    obj.data.update()
    obj["selection_status"] = (
        "tripo-candidate-01-v3-bakeoff-rejected-geometry"
    )
    obj["orientation_revision"] = "v3"
    obj["orientation_correction"] = (
        "pi around Blender +Z; maps to pi around exported +Y"
    )
    obj["published_front_axis"] = "-Z"
    obj["source_revision_sha256"] = EXPECTED_HASHES[V2_SOURCE]
    bpy.context.view_layer.update()

    triangles_after = triangle_count(obj)
    topology_after = topology_metrics(obj.data)
    bounds_min_after, bounds_max_after = bounds_for(obj)
    bounds_size_after = bounds_max_after - bounds_min_after
    materials_after = material_names(obj)
    packed_images_after = packed_image_hashes()
    if triangles_after != triangles_before:
        raise RuntimeError("Triangle count changed during orientation correction")
    if topology_after != topology_before:
        raise RuntimeError("Topology changed during orientation correction")
    if materials_after != materials_before:
        raise RuntimeError("Material slots changed during orientation correction")
    if packed_images_after != packed_images_before:
        raise RuntimeError("Packed image bytes changed during orientation correction")
    if any(
        abs(bounds_size_after[axis] - TARGET_SIZE[axis]) > 0.0005
        for axis in range(3)
    ):
        raise RuntimeError(f"Envelope changed: {tuple(bounds_size_after)}")
    if abs(bounds_min_after.z) > 0.0005:
        raise RuntimeError(f"Ground contact changed: {bounds_min_after.z}")

    violet_index = materials_after.index("mat.state.optional.violet")
    violet_polygons = [
        polygon
        for polygon in obj.data.polygons
        if polygon.material_index == violet_index
    ]
    if len(violet_polygons) != 33:
        raise RuntimeError(
            f"Expected 33 violet faces, found {len(violet_polygons)}"
        )
    violet_centers_y = [
        float((obj.matrix_world @ polygon.center).y)
        for polygon in violet_polygons
    ]
    if min(violet_centers_y) <= 0.12:
        raise RuntimeError(
            "Violet screen did not rotate to Blender +Y / exported -Z"
        )

    save_started = time.perf_counter()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(
        filepath=str(V3_SOURCE),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )
    save_seconds = time.perf_counter() - save_started

    activate(obj)
    export_started = time.perf_counter()
    export_result = bpy.ops.export_scene.gltf(
        filepath=str(V3_GLB),
        check_existing=False,
        export_format="GLB",
        export_copyright=(
            "SpaceAdventure provisional Tripo candidate 01 v3; "
            "orientation-only revision from v2"
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

    v3_glb = glb_summary(V3_GLB)
    if v3_glb["materials"] != materials_before:
        raise RuntimeError(f"V3 GLB materials changed: {v3_glb['materials']}")
    if v3_glb["images"] != expected_export_images:
        raise RuntimeError(
            "V3 embedded texture payloads do not match the retained v2 "
            "packed-image export mapping"
        )
    violet_factor = v3_glb["violet_emissive_factor"]
    if len(violet_factor) != len(violet_export_before["factor"]) or any(
        abs(float(actual) - float(expected)) > 1e-6
        for actual, expected in zip(
            violet_factor,
            violet_export_before["factor"],
        )
    ):
        raise RuntimeError(
            "Violet emissive factor changed from retained v2 material"
        )
    if (
        abs(
            float(v3_glb["violet_emissive_strength"])
            - float(violet_export_before["strength"])
        )
        > 1e-6
    ):
        raise RuntimeError(
            "Violet emissive strength changed from retained v2 material"
        )
    violet_bounds = v3_glb["violet_primitive"]
    if float(violet_bounds["position_max"][2]) >= -0.12:
        raise RuntimeError(
            f"Violet GLB primitive is not on -Z front: {violet_bounds}"
        )
    if any(v3_glb[key] != 0 for key in ("cameras", "lights", "animations", "skins")):
        raise RuntimeError("Unexpected non-mesh content in v3 GLB")

    # Fresh import into an empty Blender database.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_result = bpy.ops.import_scene.gltf(filepath=str(V3_GLB))
    imported_meshes = [
        obj for obj in bpy.context.scene.objects if obj.type == "MESH"
    ]
    if len(imported_meshes) != 1:
        raise RuntimeError(
            f"Fresh import expected one mesh, found {len(imported_meshes)}"
        )
    imported = imported_meshes[0]
    imported_triangles = triangle_count(imported)
    imported_min, imported_max = bounds_for(imported)
    imported_size = imported_max - imported_min
    imported_materials = material_names(imported)
    if imported_triangles != 3979:
        raise RuntimeError(
            f"Fresh import triangle count changed: {imported_triangles}"
        )
    if imported_materials != EXPECTED_MATERIALS:
        raise RuntimeError(
            f"Fresh import materials changed: {imported_materials}"
        )
    if any(
        abs(imported_size[axis] - TARGET_SIZE[axis]) > 0.0005
        for axis in range(3)
    ):
        raise RuntimeError(f"Fresh import envelope changed: {tuple(imported_size)}")
    if abs(imported_min.z) > 0.0005:
        raise RuntimeError(f"Fresh import lost ground contact: {imported_min.z}")

    preserved_after = verify_preserved_files()
    if preserved_after != preserved_before:
        raise RuntimeError("Retained v2 source/raw evidence changed")

    result: dict[str, object] = {
        "asset_id": ASSET_ID,
        "status": (
            "candidate 01 v3 orientation comparator; rejected at geometry "
            "hard gate"
        ),
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "correction": {
            "reason": (
                "V2 violet screen imported on +Z despite the documented -Z "
                "export front."
            ),
            "rotation_radians": math.pi,
            "blender_axis": "+Z",
            "published_axis": "+Y",
            "published_front_before": "+Z",
            "published_front_after": "-Z",
            "geometry_operation": "mesh data transform; object origin unchanged",
        },
        "input": {
            "v2_source": str(V2_SOURCE.relative_to(REPOSITORY)).replace(
                "\\", "/"
            ),
            "v2_source_bytes": V2_SOURCE.stat().st_size,
            "v2_source_sha256": EXPECTED_HASHES[V2_SOURCE],
            "raw_glb": str(RAW_GLB.relative_to(REPOSITORY)).replace(
                "\\", "/"
            ),
            "raw_glb_bytes": RAW_GLB.stat().st_size,
            "raw_glb_sha256": EXPECTED_HASHES[RAW_GLB],
        },
        "preserved_inputs": preserved_after,
        "before": {
            "triangles": triangles_before,
            "topology": topology_before,
            "bounds_min_blender_m": vec(bounds_min_before),
            "bounds_max_blender_m": vec(bounds_max_before),
            "materials": materials_before,
            "packed_image_hashes": packed_images_before,
            "violet_export_expectation": violet_export_before,
        },
        "after": {
            "triangles": triangles_after,
            "topology": topology_after,
            "bounds_min_blender_m": vec(bounds_min_after),
            "bounds_max_blender_m": vec(bounds_max_after),
            "bounds_size_blender_m": vec(bounds_size_after),
            "materials": materials_after,
            "packed_image_hashes": packed_images_after,
            "violet_faces": len(violet_polygons),
            "violet_center_y_min_blender_m": round(
                min(violet_centers_y), 6
            ),
            "violet_center_y_max_blender_m": round(
                max(violet_centers_y), 6
            ),
        },
        "gltf": v3_glb,
        "fresh_import": {
            "result": sorted(import_result),
            "objects": len(bpy.context.scene.objects),
            "meshes": 1,
            "triangles": imported_triangles,
            "bounds_min_blender_m": vec(imported_min),
            "bounds_max_blender_m": vec(imported_max),
            "bounds_size_blender_m": vec(imported_size),
            "materials": imported_materials,
        },
        "outputs": {
            "v3_source": str(V3_SOURCE.relative_to(REPOSITORY)).replace(
                "\\", "/"
            ),
            "v3_source_bytes": V3_SOURCE.stat().st_size,
            "v3_source_sha256": sha256(V3_SOURCE),
            "v3_derived_glb": str(V3_GLB.relative_to(REPOSITORY)).replace(
                "\\", "/"
            ),
            "v3_derived_glb_bytes": V3_GLB.stat().st_size,
            "v3_derived_glb_sha256": sha256(V3_GLB),
            "export_result": sorted(export_result),
        },
        "preservation": {
            "raw_glb_overwritten": False,
            "v2_source_overwritten": False,
        },
        "timings_seconds": {
            "save_source": round(save_seconds, 3),
            "export_glb": round(export_seconds, 3),
            "total": round(time.perf_counter() - started, 3),
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
