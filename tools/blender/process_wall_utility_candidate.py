"""Inspect, reconstruct, validate, and review the Tripo wall-utility candidate.

This script is intentionally asset-specific but path-parameterized.  It never
edits the raw provider export.  The default ``process`` mode:

1. verifies the raw GLB hash and imports it into a factory scene;
2. removes the generated duplicate rear half at a configurable depth cut;
3. closes the cut with a flat, untextured mounting face;
4. reduces the retained front to the brief's triangle budget;
5. normalizes the editable source to Blender ``+Z`` up / ``+Y`` front so the
   exported GLB is ``+Y`` up / ``-Z`` front;
6. saves a packed editable ``.blend`` and candidate-specific GLB;
7. freshly imports the exact GLB, runs mechanical validation, and produces
   standardized Blender review renders.

Run with Blender 5.2:

    blender --background --factory-startup \
        --python tools/blender/process_wall_utility_candidate.py -- \
        --repository C:/Developpement/space-adventure-art-production \
        --mode process

Use ``--mode inspect`` for a read-only raw-structure report.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import bmesh
import bpy
from mathutils import Matrix, Vector


ASSET_ID = "prop.station.wall_utility.v1"
DEFAULT_RUN_ID = (
    "prop.station.wall_utility.v1__tripo__"
    "v3.1-best-quality__2026-07-24__01"
)
DEFAULT_RAW_NAME = (
    "prop.station.wall_utility.v1__raw__"
    "tripo-v3.1__candidate-01.glb"
)
EXPECTED_RAW_SHA256 = (
    "7D1B87029212C9DA8757DABBA7643B7811808F124FFEC7E80C4A7E546F969059"
)
TARGET_SIZE_BLENDER = Vector((1.20, 0.22, 0.80))
HARD_TRIANGLE_LIMIT = 3000
HARD_MESH_LIMIT = 4
HARD_MATERIAL_LIMIT = 3
MAX_TEXTURE_SIZE = 1024


def blender_args() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def default_repository() -> Path:
    configured = os.environ.get("SPACE_ADVENTURE_REPOSITORY")
    if configured:
        return Path(configured).resolve()
    return Path(__file__).resolve().parents[2]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repository",
        type=Path,
        default=default_repository(),
    )
    parser.add_argument(
        "--mode",
        choices=("inspect", "process"),
        default="process",
    )
    parser.add_argument("--run-id", default=DEFAULT_RUN_ID)
    parser.add_argument("--raw", type=Path)
    parser.add_argument("--source-blend", type=Path)
    parser.add_argument("--derived-glb", type=Path)
    parser.add_argument("--expected-raw-sha256", default=EXPECTED_RAW_SHA256)
    parser.add_argument(
        "--cut-fraction",
        type=float,
        default=0.56,
        help=(
            "Fraction from the front-most Blender -Y bound to the rear-most "
            "bound where the generated duplicate rear is removed."
        ),
    )
    parser.add_argument("--target-triangles", type=int, default=2780)
    parser.add_argument("--resolution", type=int, default=800)
    return parser.parse_args(blender_args())


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def repo_path(path: Path, repository: Path) -> str:
    try:
        return str(path.relative_to(repository)).replace("\\", "/")
    except ValueError:
        return str(path)


def vec(values: Vector) -> list[float]:
    return [round(float(value), 6) for value in values]


def matrix_is_identity(matrix: Matrix, tolerance: float = 1e-6) -> bool:
    identity = Matrix.Identity(4)
    return all(
        abs(matrix[row][column] - identity[row][column]) <= tolerance
        for row in range(4)
        for column in range(4)
    )


def activate(obj: bpy.types.Object) -> None:
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds_for_objects(
    objects: list[bpy.types.Object],
) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                minimum[axis] = min(minimum[axis], world_corner[axis])
                maximum[axis] = max(maximum[axis], world_corner[axis])
    return minimum, maximum


def topology_metrics(mesh: bpy.types.Mesh) -> dict[str, Any]:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    result: dict[str, Any] = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(
            1 for edge in bm.edges if not edge.is_manifold
        ),
        "wire_edges": sum(1 for edge in bm.edges if edge.is_wire),
        "loose_vertices": sum(
            1 for vertex in bm.verts if not vertex.link_edges
        ),
        "zero_area_faces": sum(
            1 for face in bm.faces if face.calc_area() <= 1e-12
        ),
    }
    # glTF uses one index for position, normal, tangent, and UV attributes, so
    # a fresh import legitimately duplicates vertices at UV and hard-normal
    # seams.  Report the exact imported topology above, then evaluate physical
    # closure on a disposable spatially welded copy.
    before_spatial_weld = len(bm.verts)
    bmesh.ops.remove_doubles(
        bm,
        verts=list(bm.verts),
        dist=1e-6,
    )
    bm.normal_update()
    result["spatial_weld_diagnostic"] = {
        "distance_m": 1e-6,
        "vertices_before": before_spatial_weld,
        "vertices_after": len(bm.verts),
        "merged": before_spatial_weld - len(bm.verts),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(
            1 for edge in bm.edges if not edge.is_manifold
        ),
        "wire_edges": sum(1 for edge in bm.edges if edge.is_wire),
        "loose_vertices": sum(
            1 for vertex in bm.verts if not vertex.link_edges
        ),
        "zero_area_faces": sum(
            1 for face in bm.faces if face.calc_area() <= 1e-12
        ),
    }
    bm.free()
    return result


def image_records() -> list[dict[str, Any]]:
    return [
        {
            "name": image.name,
            "size": [int(image.size[0]), int(image.size[1])],
            "colorspace": image.colorspace_settings.name,
            "file_format": image.file_format,
            "packed": bool(image.packed_file),
        }
        for image in bpy.data.images
        if image.type == "IMAGE" and image.size[0] and image.size[1]
    ]


def configure_factory_scene(name: str) -> bpy.types.Scene:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = name
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    return scene


def import_raw(raw_path: Path) -> tuple[list[str], list[bpy.types.Object]]:
    import_result = bpy.ops.import_scene.gltf(
        filepath=str(raw_path),
        import_pack_images=True,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()
    meshes = sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    if not meshes:
        raise RuntimeError("Raw GLB contains no mesh objects")
    return sorted(import_result), meshes


def bake_world_transforms(meshes: list[bpy.types.Object]) -> None:
    for obj in meshes:
        if obj.data.users > 1:
            obj.data = obj.data.copy()
        world = obj.matrix_world.copy()
        obj.data.transform(world)
        obj.parent = None
        obj.matrix_world = Matrix.Identity(4)
        obj.data.update()
    bpy.context.view_layer.update()


def join_meshes(meshes: list[bpy.types.Object]) -> bpy.types.Object:
    if len(meshes) == 1:
        return meshes[0]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    result = bpy.context.active_object
    if result is None or result.type != "MESH":
        raise RuntimeError("Failed to join imported raw mesh objects")
    return result


def raw_metrics(
    raw_path: Path,
    raw_hash: str,
    import_result: list[str],
    meshes: list[bpy.types.Object],
    repository: Path,
) -> dict[str, Any]:
    minimum, maximum = bounds_for_objects(meshes)
    mesh_rows: list[dict[str, Any]] = []
    for obj in meshes:
        mesh_rows.append(
            {
                "name": obj.name,
                "data": obj.data.name,
                "vertices": len(obj.data.vertices),
                "triangles": triangle_count(obj),
                "materials": [
                    slot.material.name if slot.material else None
                    for slot in obj.material_slots
                ],
                "matrix_world_identity": matrix_is_identity(obj.matrix_world),
                "parent": obj.parent.name if obj.parent else None,
            }
        )
    return {
        "asset_id": ASSET_ID,
        "inspected_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "raw_path": repo_path(raw_path, repository),
        "raw_bytes": raw_path.stat().st_size,
        "raw_sha256": raw_hash,
        "import_result": import_result,
        "mesh_objects": len(meshes),
        "vertices": sum(row["vertices"] for row in mesh_rows),
        "triangles": sum(row["triangles"] for row in mesh_rows),
        "materials": sorted(
            {
                slot.material.name
                for obj in meshes
                for slot in obj.material_slots
                if slot.material
            }
        ),
        "images": image_records(),
        "armatures": [
            obj.name for obj in bpy.context.scene.objects
            if obj.type == "ARMATURE"
        ],
        "actions": [action.name for action in bpy.data.actions],
        "bounds_min_blender_units": vec(minimum),
        "bounds_max_blender_units": vec(maximum),
        "bounds_size_blender_units": vec(maximum - minimum),
        "mesh_breakdown": mesh_rows,
    }


def create_palette_material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"{name} has no Principled BSDF node")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def reconstruct_rear(
    obj: bpy.types.Object,
    cut_fraction: float,
    rear_material_index: int,
) -> dict[str, Any]:
    if not 0.40 <= cut_fraction <= 0.72:
        raise RuntimeError(
            f"Cut fraction {cut_fraction} is outside the conservative range"
        )
    minimum, maximum = bounds_for_objects([obj])
    depth = maximum.y - minimum.y
    cut_y = minimum.y + depth * cut_fraction

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    original_faces = len(bm.faces)
    vertices_before_weld = len(bm.verts)
    bmesh.ops.remove_doubles(
        bm,
        verts=list(bm.verts),
        dist=max(depth * 0.0000035, 1e-6),
    )
    vertices_after_weld = len(bm.verts)
    operation = bmesh.ops.bisect_plane(
        bm,
        geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        dist=max(depth * 1e-7, 1e-8),
        plane_co=Vector((0.0, cut_y, 0.0)),
        plane_no=Vector((0.0, 1.0, 0.0)),
        clear_outer=True,
        clear_inner=False,
    )
    cut_tolerance = max(depth * 1e-5, 1e-6)
    cut_edges = [
        edge
        for edge in bm.edges
        if edge.is_valid
        and edge.is_boundary
        and all(
            abs(vertex.co.y - cut_y) <= cut_tolerance
            for vertex in edge.verts
        )
    ]
    fill_result = bmesh.ops.holes_fill(
        bm,
        edges=cut_edges,
        sides=0,
    )
    rear_faces = [
        face for face in fill_result.get("faces", []) if face.is_valid
    ]
    if not rear_faces and cut_edges:
        fill_result = bmesh.ops.edgenet_fill(
            bm,
            edges=cut_edges,
            mat_nr=rear_material_index,
            use_smooth=False,
            sides=0,
        )
        rear_faces = [
            face for face in fill_result.get("faces", []) if face.is_valid
        ]
    for face in rear_faces:
        face.material_index = rear_material_index

    bmesh.ops.remove_doubles(
        bm,
        verts=list(bm.verts),
        dist=max(depth * 1e-8, 1e-9),
    )
    bmesh.ops.dissolve_degenerate(
        bm,
        edges=list(bm.edges),
        dist=max(depth * 1e-9, 1e-10),
    )
    wire_edges = [edge for edge in bm.edges if edge.is_wire]
    if wire_edges:
        bmesh.ops.delete(bm, geom=wire_edges, context="EDGES")
    loose_vertices = [
        vertex for vertex in bm.verts if not vertex.link_edges
    ]
    if loose_vertices:
        bmesh.ops.delete(bm, geom=loose_vertices, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    bpy.context.view_layer.update()

    after_minimum, after_maximum = bounds_for_objects([obj])
    return {
        "original_faces": original_faces,
        "vertices_before_spatial_weld": vertices_before_weld,
        "vertices_after_spatial_weld": vertices_after_weld,
        "vertices_spatially_welded": (
            vertices_before_weld - vertices_after_weld
        ),
        "cut_fraction_from_front": cut_fraction,
        "cut_y_raw_units": round(float(cut_y), 8),
        "cut_boundary_edges": len(cut_edges),
        "rear_cap_faces_created": len(rear_faces),
        "faces_after_reconstruction": len(obj.data.polygons),
        "bounds_after_cut_min": vec(after_minimum),
        "bounds_after_cut_max": vec(after_maximum),
    }


def decimate_to_target(
    obj: bpy.types.Object,
    target_triangles: int,
) -> dict[str, Any]:
    before = triangle_count(obj)
    if before <= target_triangles:
        return {
            "triangles_before": before,
            "triangles_after": before,
            "ratio": 1.0,
        }
    modifier = obj.modifiers.new(
        name="BakeoffRetopology",
        type="DECIMATE",
    )
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = max(0.000001, target_triangles / before)
    modifier.use_collapse_triangulate = True
    modifier.use_symmetry = False
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.dissolve_degenerate(
        bm,
        edges=list(bm.edges),
        dist=1e-10,
    )
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
    bpy.context.view_layer.update()

    return {
        "triangles_before": before,
        "triangles_after": triangle_count(obj),
        "ratio": round(float(modifier.ratio), 8),
    }


def normalize_object(obj: bpy.types.Object) -> dict[str, Any]:
    minimum, maximum = bounds_for_objects([obj])
    size = maximum - minimum
    if min(size) <= 1e-9:
        raise RuntimeError(f"Degenerate bounds before normalization: {size}")

    # Move the front-facing retained geometry so the rear cap is at Blender
    # Y=0, the bottom at Z=0, and the width is centered on X=0.
    obj.location = Vector(
        (
            -(minimum.x + maximum.x) * 0.5,
            -maximum.y,
            -minimum.z,
        )
    )
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()

    minimum, maximum = bounds_for_objects([obj])
    size = maximum - minimum
    obj.scale = Vector(
        (
            TARGET_SIZE_BLENDER.x / size.x,
            TARGET_SIZE_BLENDER.y / size.y,
            TARGET_SIZE_BLENDER.z / size.z,
        )
    )
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()

    # Correct floating-point residue without moving the rear mounting plane.
    minimum, maximum = bounds_for_objects([obj])
    obj.location = Vector(
        (
            -(minimum.x + maximum.x) * 0.5,
            -maximum.y,
            -minimum.z,
        )
    )
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()

    # Blender glTF export maps authoring +Y to published -Z.  The retained raw
    # front initially extends into Blender -Y, so rotate 180 degrees around Z.
    # Rotating instead of reflecting one axis preserves the approved front-view
    # handedness (vent left, utility runs right).  The final authoring envelope
    # is Y=[0,+0.22], which publishes as Z=[-0.22,0].
    # Apply directly to mesh data because imported glTF objects may use
    # quaternion rotation mode; writing rotation_euler in that mode is not an
    # authoritative transform.
    obj.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
    obj.data.update()
    bpy.context.view_layer.update()

    minimum, maximum = bounds_for_objects([obj])
    obj.location = Vector(
        (
            -(minimum.x + maximum.x) * 0.5,
            -minimum.y,
            -minimum.z,
        )
    )
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()

    final_minimum, final_maximum = bounds_for_objects([obj])
    return {
        "bounds_min_blender_m": vec(final_minimum),
        "bounds_max_blender_m": vec(final_maximum),
        "bounds_size_blender_m": vec(final_maximum - final_minimum),
        "authoring_up": "+Z",
        "authoring_front": "+Y",
        "authoring_visible_geometry_direction": "+Y from rear Y=0",
        "rear_mounting_plane": "Y = 0",
        "published_up": "+Y",
        "published_front": "-Z",
        "published_visible_geometry_direction": "-Z from rear Z=0",
        "published_mounting_plane": "Z = 0",
    }


def repair_small_topology_defects(
    obj: bpy.types.Object,
    rear_material_index: int,
) -> dict[str, Any]:
    """Remove local decimation slivers and close the resulting tiny holes.

    The generated source is spatially welded before the depth cut, but a
    700:1 collapse can still create a handful of overlapping sliver triangles.
    This bounded repair keeps the largest two faces around an over-subscribed
    edge, removes only smaller local faces, and fills the exposed edge loops.
    """

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(
        bm,
        verts=list(bm.verts),
        dist=1e-6,
    )
    bmesh.ops.dissolve_degenerate(
        bm,
        edges=list(bm.edges),
        dist=1e-7,
    )
    bm.normal_update()

    before = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(
            1 for edge in bm.edges if not edge.is_manifold
        ),
    }
    removed_faces = 0
    filled_faces = 0
    passes: list[dict[str, int]] = []

    def manually_fill_simple_boundary_loops(
        boundary_edges: list[bmesh.types.BMEdge],
    ) -> list[bmesh.types.BMFace]:
        edge_pool = set(boundary_edges)
        created: list[bmesh.types.BMFace] = []
        while edge_pool:
            first = next(iter(edge_pool))
            edge_pool.remove(first)
            start = first.verts[0]
            current = first.verts[1]
            vertices = [start, current]
            closed = False
            while True:
                candidates = [
                    edge
                    for edge in current.link_edges
                    if edge in edge_pool
                ]
                if len(candidates) != 1:
                    break
                edge = candidates[0]
                edge_pool.remove(edge)
                next_vertex = edge.other_vert(current)
                if next_vertex == start:
                    closed = True
                    break
                if next_vertex in vertices:
                    break
                vertices.append(next_vertex)
                current = next_vertex
            if closed and len(vertices) >= 3:
                try:
                    face = bm.faces.new(vertices)
                except ValueError:
                    continue
                created.append(face)
        return created

    def collapse_tiny_boundary_loops(
        boundary_edges: list[bmesh.types.BMEdge],
    ) -> tuple[int, int]:
        remaining = set(boundary_edges)
        collapsed_loops = 0
        collapsed_vertices = 0
        while remaining:
            seed = remaining.pop()
            component_edges = {seed}
            component_vertices = set(seed.verts)
            frontier = list(seed.verts)
            while frontier:
                vertex = frontier.pop()
                for edge in vertex.link_edges:
                    if edge not in remaining:
                        continue
                    remaining.remove(edge)
                    component_edges.add(edge)
                    for linked_vertex in edge.verts:
                        if linked_vertex not in component_vertices:
                            component_vertices.add(linked_vertex)
                            frontier.append(linked_vertex)
            perimeter = sum(edge.calc_length() for edge in component_edges)
            if (
                len(component_edges) <= 16
                and len(component_vertices) <= 16
                and perimeter <= 0.02
            ):
                center = sum(
                    (vertex.co for vertex in component_vertices),
                    Vector((0.0, 0.0, 0.0)),
                ) / len(component_vertices)
                bmesh.ops.pointmerge(
                    bm,
                    verts=list(component_vertices),
                    merge_co=center,
                )
                collapsed_loops += 1
                collapsed_vertices += len(component_vertices)
        if collapsed_loops:
            bmesh.ops.dissolve_degenerate(
                bm,
                edges=list(bm.edges),
                dist=1e-7,
            )
        return collapsed_loops, collapsed_vertices

    for pass_index in range(6):
        bm.normal_update()
        overfull = [
            edge for edge in bm.edges if len(edge.link_faces) > 2
        ]
        faces_to_remove: set[bmesh.types.BMFace] = set()
        for edge in overfull:
            linked = sorted(
                (
                    face for face in edge.link_faces
                    if face.is_valid
                ),
                key=lambda face: face.calc_area(),
                reverse=True,
            )
            faces_to_remove.update(linked[2:])
        if faces_to_remove:
            removed_faces += len(faces_to_remove)
            bmesh.ops.delete(
                bm,
                geom=list(faces_to_remove),
                context="FACES",
            )

        boundary_before_collapse = [
            edge for edge in bm.edges
            if edge.is_valid and edge.is_boundary
        ]
        collapsed_loops, collapsed_vertices = (
            collapse_tiny_boundary_loops(boundary_before_collapse)
        )
        boundary = [
            edge for edge in bm.edges
            if edge.is_valid and edge.is_boundary
        ]
        new_faces: list[bmesh.types.BMFace] = []
        if boundary:
            fill = bmesh.ops.holes_fill(
                bm,
                edges=boundary,
                sides=0,
            )
            new_faces = [
                face for face in fill.get("faces", [])
                if face.is_valid
            ]
            if not new_faces:
                fill = bmesh.ops.edgenet_fill(
                    bm,
                    edges=boundary,
                    mat_nr=0,
                    use_smooth=False,
                    sides=0,
                )
                new_faces = [
                    face for face in fill.get("faces", [])
                    if face.is_valid
                ]
            if not new_faces:
                new_faces = manually_fill_simple_boundary_loops(boundary)
        filled_faces += len(new_faces)
        for face in new_faces:
            if face.verts and all(
                abs(vertex.co.y) <= 1e-5 for vertex in face.verts
            ):
                face.material_index = rear_material_index
            else:
                face.material_index = 0

        bmesh.ops.remove_doubles(
            bm,
            verts=list(bm.verts),
            dist=1e-6,
        )
        bmesh.ops.dissolve_degenerate(
            bm,
            edges=list(bm.edges),
            dist=1e-7,
        )
        bm.normal_update()
        boundary_count = sum(
            1 for edge in bm.edges if edge.is_boundary
        )
        non_manifold_count = sum(
            1 for edge in bm.edges if not edge.is_manifold
        )
        passes.append(
            {
                "pass": pass_index + 1,
                "overfull_edges_before": len(overfull),
                "faces_removed": len(faces_to_remove),
                "tiny_boundary_loops_collapsed": collapsed_loops,
                "tiny_boundary_vertices_collapsed": collapsed_vertices,
                "faces_filled": len(new_faces),
                "boundary_edges_after": boundary_count,
                "non_manifold_edges_after": non_manifold_count,
            }
        )
        if boundary_count == 0 and non_manifold_count == 0:
            break
        if not faces_to_remove and not new_faces:
            break

    wire_edges = [edge for edge in bm.edges if edge.is_wire]
    if wire_edges:
        bmesh.ops.delete(bm, geom=wire_edges, context="EDGES")
    loose_vertices = [
        vertex for vertex in bm.verts if not vertex.link_edges
    ]
    if loose_vertices:
        bmesh.ops.delete(bm, geom=loose_vertices, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    after = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(
            1 for edge in bm.edges if not edge.is_manifold
        ),
        "wire_edges": sum(1 for edge in bm.edges if edge.is_wire),
        "loose_vertices": sum(
            1 for vertex in bm.verts if not vertex.link_edges
        ),
        "zero_area_faces": sum(
            1 for face in bm.faces if face.calc_area() <= 1e-12
        ),
    }
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    bpy.context.view_layer.update()
    return {
        "before": before,
        "passes": passes,
        "faces_removed_total": removed_faces,
        "faces_filled_total": filled_faces,
        "after": after,
    }


def enforce_rear_plane_material(
    obj: bpy.types.Object,
    rear_material_index: int,
) -> int:
    minimum, maximum = bounds_for_objects([obj])
    tolerance = max((maximum.y - minimum.y) * 0.0005, 1e-6)
    assigned = 0
    for polygon in obj.data.polygons:
        coordinates = [
            obj.data.vertices[index].co for index in polygon.vertices
        ]
        if coordinates and all(
            abs(vertex.y - minimum.y) <= tolerance
            for vertex in coordinates
        ):
            polygon.material_index = rear_material_index
            assigned += 1
    obj.data.update()
    return assigned


def resize_and_pack_images() -> list[dict[str, Any]]:
    operations: list[dict[str, Any]] = []
    for image in bpy.data.images:
        if image.type != "IMAGE" or not image.size[0] or not image.size[1]:
            continue
        before = [int(image.size[0]), int(image.size[1])]
        if image.size[0] > MAX_TEXTURE_SIZE or image.size[1] > MAX_TEXTURE_SIZE:
            factor = min(
                MAX_TEXTURE_SIZE / image.size[0],
                MAX_TEXTURE_SIZE / image.size[1],
            )
            image.scale(
                max(1, round(image.size[0] * factor)),
                max(1, round(image.size[1] * factor)),
            )
        image.pack()
        operations.append(
            {
                "name": image.name,
                "before": before,
                "after": [int(image.size[0]), int(image.size[1])],
                "colorspace": image.colorspace_settings.name,
                "packed": bool(image.packed_file),
            }
        )
    return operations


def prepare_asset_hierarchy(
    obj: bpy.types.Object,
    raw_hash: str,
) -> bpy.types.Object:
    # Remove imported non-mesh hierarchy after its transforms have been baked.
    for other in list(bpy.context.scene.objects):
        if other != obj:
            bpy.data.objects.remove(other, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    obj.name = "wall_utility.body"
    obj.data.name = "wall_utility.body.mesh"
    obj["part_role"] = "generated-front-with-reconstructed-flat-rear"
    obj["raw_sha256"] = raw_hash
    obj["selection_status"] = "provisional-pending-owner-review"
    obj["collision"] = "none"

    root = bpy.data.objects.new(name=ASSET_ID, object_data=None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.location = Vector((0.0, 0.0, 0.0))
    root["asset_id"] = ASSET_ID
    root["authoring_up"] = "+Z"
    root["authoring_front"] = "+Y"
    root["authoring_visible_geometry_direction"] = "+Y"
    root["published_up"] = "+Y"
    root["published_front"] = "-Z"
    root["published_visible_geometry_direction"] = "-Z"
    root["pivot"] = "bottom-center-rear-mounting-plane"
    root["mounting_plane"] = "published Z=0"
    root["selection_status"] = "provisional-pending-owner-review"
    root["collision"] = "none"
    obj.parent = root
    obj.matrix_parent_inverse = Matrix.Identity(4)
    return root


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_area_light(
    scene: bpy.types.Scene,
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
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, target)
    return light


def review_material(
    name: str,
    color: tuple[float, float, float, float],
    roughness: float = 0.8,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = roughness
    return material


def create_wire_overlay(
    meshes: list[bpy.types.Object],
) -> bpy.types.Object:
    material = review_material(
        "review.wire.material",
        (0.01, 0.85, 0.95, 1.0),
        0.55,
    )
    curve = bpy.data.curves.new("review.wire.geometry", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = 0.0011
    curve.bevel_resolution = 0
    curve.materials.append(material)
    for source in meshes:
        for edge in source.data.edges:
            spline = curve.splines.new("POLY")
            spline.points.add(1)
            for point_index, vertex_index in enumerate(edge.vertices):
                position = (
                    source.matrix_world
                    @ source.data.vertices[vertex_index].co
                )
                spline.points[point_index].co = (*position, 1.0)
    overlay = bpy.data.objects.new("review.wire.overlay", curve)
    bpy.context.scene.collection.objects.link(overlay)
    overlay.hide_render = True
    return overlay


def configure_review_scene(
    scene: bpy.types.Scene,
    meshes: list[bpy.types.Object],
    resolution: int,
) -> tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object]:
    scene.name = "Wall Utility Exact GLB Blender Review"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = bpy.data.worlds.new("review.world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.028,
        0.038,
        0.055,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.12
    scene.world = world

    minimum, maximum = bounds_for_objects(meshes)
    center = (minimum + maximum) * 0.5
    target = Vector((center.x, center.y, minimum.z + (maximum.z - minimum.z) * 0.52))
    add_area_light(scene, "review.key", (-2.2, 2.7, 2.6), 190.0, 2.2, target)
    add_area_light(scene, "review.fill", (2.6, 1.8, 1.7), 85.0, 2.4, target)
    add_area_light(scene, "review.rim", (0.4, -2.3, 2.2), 150.0, 2.0, target)

    camera_data = bpy.data.cameras.new("review.camera.data")
    camera_data.type = "ORTHO"
    camera_data.clip_start = 0.01
    camera_data.clip_end = 20.0
    camera = bpy.data.objects.new("review.camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    floor_material = review_material(
        "review.floor.material",
        (0.075, 0.095, 0.125, 1.0),
    )
    bpy.ops.mesh.primitive_plane_add(
        size=8.0,
        location=(0.0, 0.0, minimum.z - 0.004),
    )
    floor = bpy.context.active_object
    floor.name = "review.floor"
    floor.data.materials.append(floor_material)

    overlay = create_wire_overlay(meshes)
    bpy.context.view_layer.update()
    return camera, floor, overlay


def render_review_views(
    output_dir: Path,
    meshes: list[bpy.types.Object],
    camera: bpy.types.Object,
    floor: bpy.types.Object,
    overlay: bpy.types.Object,
) -> list[dict[str, Any]]:
    output_dir.mkdir(parents=True, exist_ok=True)
    minimum, maximum = bounds_for_objects(meshes)
    center = (minimum + maximum) * 0.5
    target = (center.x, center.y, minimum.z + (maximum.z - minimum.z) * 0.5)
    views = [
        ("front", (0.0, 3.0, target[2]), 1.38, True, False),
        ("back", (0.0, -3.0, target[2]), 1.38, True, False),
        ("right", (3.0, target[1], target[2]), 1.06, True, False),
        ("front-right-3q", (2.4, 2.7, 1.55), 1.48, True, False),
        (
            "wireframe-front-right-3q",
            (2.4, 2.7, 1.55),
            1.48,
            True,
            True,
        ),
    ]
    records: list[dict[str, Any]] = []
    for name, location, ortho_scale, floor_visible, wire_visible in views:
        camera.location = location
        point_at(camera, Vector(target))
        camera.data.ortho_scale = ortho_scale
        floor.hide_render = not floor_visible
        overlay.hide_render = not wire_visible
        output_path = output_dir / f"{name}.png"
        bpy.context.scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
        if not output_path.is_file():
            raise RuntimeError(f"Render was not created: {output_path}")
        records.append(
            {
                "name": name,
                "file": output_path.name,
                "bytes": output_path.stat().st_size,
                "sha256": sha256(output_path),
                "camera_location_blender_m": [
                    round(float(value), 6) for value in location
                ],
                "target_blender_m": [
                    round(float(value), 6) for value in target
                ],
                "projection": "orthographic",
                "ortho_scale_m": ortho_scale,
                "wire_overlay": wire_visible,
            }
        )
    return records


def fresh_import_metrics(
    derived_glb: Path,
) -> tuple[
    dict[str, Any],
    list[bpy.types.Object],
    list[str],
]:
    configure_factory_scene("Wall Utility Fresh GLB Validation")
    import_result = bpy.ops.import_scene.gltf(
        filepath=str(derived_glb),
        import_pack_images=False,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()
    meshes = sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    minimum, maximum = bounds_for_objects(meshes)
    materials = sorted(
        {
            slot.material.name
            for obj in meshes
            for slot in obj.material_slots
            if slot.material
        }
    )
    images = image_records()
    root = next(
        (
            obj for obj in bpy.context.scene.objects
            if obj.type == "EMPTY" and obj.get("asset_id") == ASSET_ID
        ),
        None,
    )
    rows: list[dict[str, Any]] = []
    for obj in meshes:
        rows.append(
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "triangles": triangle_count(obj),
                "materials": [
                    slot.material.name if slot.material else None
                    for slot in obj.material_slots
                ],
                "uv_layers": [layer.name for layer in obj.data.uv_layers],
                "topology": topology_metrics(obj.data),
                "matrix_world_identity": matrix_is_identity(obj.matrix_world),
            }
        )
    metrics = {
        "import_result": sorted(import_result),
        "mesh_objects": len(meshes),
        "vertices": sum(row["vertices"] for row in rows),
        "triangles": sum(row["triangles"] for row in rows),
        "materials": materials,
        "material_count": len(materials),
        "images": images,
        "image_count": len(images),
        "texture_sets": 1 if images else 0,
        "armatures": [
            obj.name for obj in bpy.context.scene.objects
            if obj.type == "ARMATURE"
        ],
        "actions": [action.name for action in bpy.data.actions],
        "collision_like_objects": [
            obj.name
            for obj in bpy.context.scene.objects
            if any(
                token in obj.name.lower()
                for token in ("collision", "collider", "colshape")
            )
        ],
        "bounds_min_blender_m": vec(minimum),
        "bounds_max_blender_m": vec(maximum),
        "bounds_size_blender_m": vec(maximum - minimum),
        "published_bounds_m": {
            "min": vec(Vector((minimum.x, minimum.z, -maximum.y))),
            "max": vec(Vector((maximum.x, maximum.z, -minimum.y))),
            "size": vec(Vector((
                maximum.x - minimum.x,
                maximum.z - minimum.z,
                maximum.y - minimum.y,
            ))),
        },
        "root_found": root is not None,
        "root_world_identity": (
            matrix_is_identity(root.matrix_world) if root else False
        ),
        "root_extras": (
            {str(key): root[key] for key in root.keys()} if root else {}
        ),
        "mesh_breakdown": rows,
    }
    return metrics, meshes, sorted(import_result)


def validation_checks(metrics: dict[str, Any]) -> list[dict[str, Any]]:
    size = metrics["bounds_size_blender_m"]
    minimum = metrics["bounds_min_blender_m"]
    maximum = metrics["bounds_max_blender_m"]
    topology = metrics["mesh_breakdown"]
    published_minimum = metrics["published_bounds_m"]["min"]
    published_maximum = metrics["published_bounds_m"]["max"]
    all_uv = all(bool(row["uv_layers"]) for row in topology)
    all_manifold = all(
        row["topology"]["spatial_weld_diagnostic"]["boundary_edges"] == 0
        and (
            row["topology"]["spatial_weld_diagnostic"][
                "non_manifold_edges"
            ]
            == 0
        )
        and row["topology"]["spatial_weld_diagnostic"]["wire_edges"] == 0
        and (
            row["topology"]["spatial_weld_diagnostic"]["loose_vertices"]
            == 0
        )
        and (
            row["topology"]["spatial_weld_diagnostic"]["zero_area_faces"]
            == 0
        )
        for row in topology
    )
    max_texture = max(
        (
            max(image["size"])
            for image in metrics["images"]
        ),
        default=0,
    )

    def within(value: float, target: float) -> bool:
        return abs(value - target) <= target * 0.02 + 1e-6

    return [
        {
            "id": "bounds",
            "pass": (
                within(size[0], 1.20)
                and within(size[1], 0.22)
                and within(size[2], 0.80)
            ),
            "actual": size,
            "requirement": "1.20 x 0.22 x 0.80 m in Blender (±2%)",
        },
        {
            "id": "pivot_and_mounting_plane",
            "pass": (
                abs(minimum[0] + maximum[0]) <= 0.0001
                and abs(minimum[2]) <= 0.0001
                and abs(minimum[1]) <= 0.0001
                and metrics["root_found"]
                and metrics["root_world_identity"]
            ),
            "actual": {
                "min": minimum,
                "max": maximum,
                "root_found": metrics["root_found"],
                "root_world_identity": metrics["root_world_identity"],
            },
            "requirement": (
                "bottom-center rear pivot at origin; Blender rear Y=0 "
                "(published rear Z=0)"
            ),
        },
        {
            "id": "published_front_and_depth_sign",
            "pass": (
                abs(published_minimum[2] + 0.22) <= 0.0001
                and abs(published_maximum[2]) <= 0.0001
                and metrics["root_extras"].get("authoring_front") == "+Y"
                and metrics["root_extras"].get("published_front") == "-Z"
            ),
            "actual": {
                "published_bounds": metrics["published_bounds_m"],
                "authoring_front": metrics["root_extras"].get(
                    "authoring_front"
                ),
                "published_front": metrics["root_extras"].get(
                    "published_front"
                ),
            },
            "requirement": (
                "published AABB Z=[-0.22,0], detailed geometry extends "
                "toward -Z from the rear mounting plane"
            ),
        },
        {
            "id": "triangles",
            "pass": metrics["triangles"] <= HARD_TRIANGLE_LIMIT,
            "actual": metrics["triangles"],
            "requirement": f"≤ {HARD_TRIANGLE_LIMIT}",
        },
        {
            "id": "mesh_objects",
            "pass": 1 <= metrics["mesh_objects"] <= HARD_MESH_LIMIT,
            "actual": metrics["mesh_objects"],
            "requirement": f"1–{HARD_MESH_LIMIT}",
        },
        {
            "id": "materials",
            "pass": metrics["material_count"] <= HARD_MATERIAL_LIMIT,
            "actual": metrics["material_count"],
            "requirement": f"≤ {HARD_MATERIAL_LIMIT}",
        },
        {
            "id": "texture_sets",
            "pass": metrics["texture_sets"] <= 1,
            "actual": metrics["texture_sets"],
            "requirement": "≤ 1",
        },
        {
            "id": "texture_resolution",
            "pass": max_texture <= MAX_TEXTURE_SIZE,
            "actual": max_texture,
            "requirement": f"≤ {MAX_TEXTURE_SIZE}",
        },
        {
            "id": "uvs",
            "pass": all_uv,
            "actual": [
                {"name": row["name"], "uv_layers": row["uv_layers"]}
                for row in topology
            ],
            "requirement": "UV layer on every textured candidate mesh",
        },
        {
            "id": "topology",
            "pass": all_manifold,
            "actual": [
                {
                    "name": row["name"],
                    "exact_fresh_import": {
                        key: value
                        for key, value in row["topology"].items()
                        if key != "spatial_weld_diagnostic"
                    },
                    "spatial_weld_diagnostic": (
                        row["topology"]["spatial_weld_diagnostic"]
                    ),
                }
                for row in topology
            ],
            "requirement": (
                "no physical boundary, non-manifold, wire, loose, or "
                "zero-area geometry after diagnostic weld of glTF "
                "attribute-seam duplicates"
            ),
        },
        {
            "id": "no_rig_or_animation",
            "pass": (
                not metrics["armatures"]
                and not metrics["actions"]
            ),
            "actual": {
                "armatures": metrics["armatures"],
                "actions": metrics["actions"],
            },
            "requirement": "0 armatures and 0 actions",
        },
        {
            "id": "no_collision",
            "pass": not metrics["collision_like_objects"],
            "actual": metrics["collision_like_objects"],
            "requirement": "no generated collision objects",
        },
    ]


def write_markdown_validation(
    path: Path,
    result: dict[str, Any],
) -> None:
    status = "PASS" if result["passed"] else "FAIL"
    lines = [
        "# Blender validation — station wall utility v1",
        "",
        f"Status: **{status}**; provisional pending owner visual review",
        "",
        f"- Asset: `{ASSET_ID}`",
        f"- Blender: `{result['blender_version']}`",
        f"- Raw SHA-256: `{result['raw']['sha256']}`",
        f"- Derived GLB SHA-256: `{result['derived']['sha256']}`",
        f"- Active cleanup time: `{result['timings_seconds']['active_cleanup']:.3f} s`",
        f"- Standardized render time: `{result['timings_seconds']['review_renders']:.3f} s`",
        "",
        "| Check | Result | Requirement |",
        "|---|---|---|",
    ]
    for check in result["checks"]:
        lines.append(
            f"| `{check['id']}` | "
            f"{'PASS' if check['pass'] else 'FAIL'} | "
            f"{check['requirement']} |"
        )
    lines.extend(
        [
            "",
            "## Rear reconstruction",
            "",
            (
                "The generated duplicate detailed rear and its blue baked "
                "artifact were removed with one planar depth cut. Blender "
                "closed the retained front with a new flat, untextured dark "
                "mounting face. No generated rear pixels remain in the "
                "derived candidate."
            ),
            "",
            "## Decision",
            "",
            (
                "Passed Blender hard gates for isolated Godot review; remains "
                "provisional pending owner visual review."
                if result["passed"]
                else (
                    "Rejected at the Blender hard gate. Preserve the raw and "
                    "derived evidence; do not publish this candidate."
                )
            ),
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


def main() -> dict[str, Any]:
    args = parse_args()
    repository = args.repository.resolve()
    run_root = (
        repository
        / "art"
        / "generated"
        / ASSET_ID
        / args.run_id
    )
    raw_path = (
        args.raw.resolve()
        if args.raw
        else run_root / "raw" / DEFAULT_RAW_NAME
    )
    source_blend = (
        args.source_blend.resolve()
        if args.source_blend
        else (
            repository
            / "art"
            / "source"
            / ASSET_ID
            / "wall-utility-v1__tripo-candidate-01.blend"
        )
    )
    derived_dir = run_root / "derived"
    derived_glb = (
        args.derived_glb.resolve()
        if args.derived_glb
        else (
            derived_dir
            / "prop.station.wall_utility.v1__clean__"
            "tripo-v3.1__candidate-01.glb"
        )
    )
    if not raw_path.is_file():
        raise FileNotFoundError(raw_path)
    raw_hash = sha256(raw_path)
    expected = args.expected_raw_sha256.upper()
    if raw_hash != expected:
        raise RuntimeError(
            f"Raw candidate hash changed: {raw_hash} != {expected}"
        )
    if raw_path.stat().st_size != 58_219_864:
        raise RuntimeError(
            f"Raw candidate size changed: {raw_path.stat().st_size}"
        )
    derived_dir.mkdir(parents=True, exist_ok=True)
    source_blend.parent.mkdir(parents=True, exist_ok=True)

    scene = configure_factory_scene("Wall Utility Raw Candidate Inspection")
    imported, meshes = import_raw(raw_path)
    raw = raw_metrics(raw_path, raw_hash, imported, meshes, repository)
    inspection_path = derived_dir / "raw-inspection.json"
    inspection_path.write_text(
        json.dumps(raw, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    if args.mode == "inspect":
        result = {
            "status": "raw inspection complete; no source modified",
            "inspection": repo_path(inspection_path, repository),
            "inspection_sha256": sha256(inspection_path),
            "raw": raw,
        }
        print(json.dumps(result, indent=2, sort_keys=True))
        return result

    if args.target_triangles > HARD_TRIANGLE_LIMIT:
        raise RuntimeError(
            f"Target triangles exceed hard limit: {args.target_triangles}"
        )

    active_started = time.perf_counter()
    bake_world_transforms(meshes)
    obj = join_meshes(meshes)
    activate(obj)
    if not obj.data.uv_layers:
        raise RuntimeError("Raw candidate contains no UV layer")

    generated_materials = [
        slot.material for slot in obj.material_slots if slot.material
    ]
    if not generated_materials:
        raise RuntimeError("Raw candidate contains no material")
    generated_materials[0].name = "mat.station.wall.generated.candidate01"
    rear_material = create_palette_material(
        "mat.station.wall.dark",
        (0.028, 0.042, 0.064, 1.0),
        metallic=0.62,
        roughness=0.68,
    )
    obj.data.materials.append(rear_material)
    rear_material_index = len(obj.data.materials) - 1

    reconstruction_started = time.perf_counter()
    reconstruction = reconstruct_rear(
        obj,
        args.cut_fraction,
        rear_material_index,
    )
    reconstruction_seconds = time.perf_counter() - reconstruction_started

    decimation_started = time.perf_counter()
    decimation = decimate_to_target(obj, args.target_triangles)
    decimation_seconds = time.perf_counter() - decimation_started

    normalization_started = time.perf_counter()
    normalization = normalize_object(obj)
    topology_repair = repair_small_topology_defects(
        obj,
        rear_material_index,
    )
    rear_faces_assigned = enforce_rear_plane_material(
        obj,
        rear_material_index,
    )
    activate(obj)
    bpy.ops.object.shade_smooth_by_angle(
        angle=math.radians(42.0),
        keep_sharp_edges=True,
    )
    bpy.context.view_layer.update()
    normalization_seconds = time.perf_counter() - normalization_started

    texture_started = time.perf_counter()
    texture_operations = resize_and_pack_images()
    texture_seconds = time.perf_counter() - texture_started

    root = prepare_asset_hierarchy(obj, raw_hash)
    scene = bpy.context.scene
    scene.name = "Wall Utility Tripo Candidate 01 Cleanup"
    scene["asset_id"] = ASSET_ID
    scene["raw_sha256"] = raw_hash
    scene["cleanup_policy"] = (
        "Remove duplicate generated rear, close planar cut, decimate retained "
        "front, normalize bottom-center rear pivot"
    )
    scene["status"] = "provisional-pending-owner-review"
    bpy.context.view_layer.update()

    save_started = time.perf_counter()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(
        filepath=str(source_blend),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )
    save_seconds = time.perf_counter() - save_started

    activate(root)
    root.select_set(True)
    obj.select_set(True)
    export_started = time.perf_counter()
    export_result = bpy.ops.export_scene.gltf(
        filepath=str(derived_glb),
        check_existing=False,
        export_format="GLB",
        export_copyright=(
            "SpaceAdventure provisional Tripo bake-off candidate; "
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
    active_cleanup_seconds = time.perf_counter() - active_started

    validation_started = time.perf_counter()
    fresh_metrics, fresh_meshes, fresh_import = fresh_import_metrics(derived_glb)
    checks = validation_checks(fresh_metrics)
    validation_seconds = time.perf_counter() - validation_started

    glb_hash = sha256(derived_glb)
    review_dir = (
        repository
        / "artifacts"
        / "reviews"
        / ASSET_ID
        / glb_hash
        / "blender"
    )
    review_started = time.perf_counter()
    camera, floor, overlay = configure_review_scene(
        bpy.context.scene,
        fresh_meshes,
        args.resolution,
    )
    renders = render_review_views(
        review_dir,
        fresh_meshes,
        camera,
        floor,
        overlay,
    )
    review_seconds = time.perf_counter() - review_started

    passed = all(check["pass"] for check in checks)
    result = {
        "asset_id": ASSET_ID,
        "run_id": args.run_id,
        "status": (
            "Blender hard gates passed; provisional pending owner visual review"
            if passed
            else "Blender hard gate failed; candidate rejected"
        ),
        "passed": passed,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "raw": {
            "path": repo_path(raw_path, repository),
            "bytes": raw_path.stat().st_size,
            "sha256": raw_hash,
            "inspection": repo_path(inspection_path, repository),
            "inspection_sha256": sha256(inspection_path),
            "metrics": raw,
        },
        "operations": {
            "rear_reconstruction": reconstruction,
            "retopology": decimation,
            "post_decimation_topology_repair": topology_repair,
            "normalization": normalization,
            "rear_plane_faces_assigned_dark_material": rear_faces_assigned,
            "texture_resize": texture_operations,
            "no_provider_rig_or_animation_retained": True,
            "no_collision_authored": True,
        },
        "source": {
            "path": repo_path(source_blend, repository),
            "bytes": source_blend.stat().st_size,
            "sha256": sha256(source_blend),
        },
        "derived": {
            "path": repo_path(derived_glb, repository),
            "bytes": derived_glb.stat().st_size,
            "sha256": glb_hash,
            "export_result": sorted(export_result),
            "fresh_import_result": fresh_import,
            "fresh_import_metrics": fresh_metrics,
        },
        "checks": checks,
        "review": {
            "directory": repo_path(review_dir, repository),
            "resolution": [args.resolution, args.resolution],
            "engine": bpy.context.scene.render.engine,
            "view_transform": bpy.context.scene.view_settings.view_transform,
            "look": bpy.context.scene.view_settings.look,
            "renders": renders,
        },
        "timings_seconds": {
            "rear_reconstruction": round(reconstruction_seconds, 3),
            "decimation": round(decimation_seconds, 3),
            "normalization_and_shading": round(normalization_seconds, 3),
            "texture_resize_and_pack": round(texture_seconds, 3),
            "save_blend": round(save_seconds, 3),
            "export_glb": round(export_seconds, 3),
            "active_cleanup": round(active_cleanup_seconds, 3),
            "fresh_import_validation": round(validation_seconds, 3),
            "review_renders": round(review_seconds, 3),
        },
        "cleanup_cap_seconds": 1800,
        "cleanup_within_cap": active_cleanup_seconds <= 1800,
    }

    report_path = derived_dir / "blender-validation.json"
    markdown_path = derived_dir / "blender-validation.md"
    report_path.write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    write_markdown_validation(markdown_path, result)
    result["reports"] = {
        "json": {
            "path": repo_path(report_path, repository),
            "bytes": report_path.stat().st_size,
            "sha256": sha256(report_path),
        },
        "markdown": {
            "path": repo_path(markdown_path, repository),
            "bytes": markdown_path.stat().st_size,
            "sha256": sha256(markdown_path),
        },
    }
    manifest_path = review_dir / "render-manifest.json"
    manifest_path.write_text(
        json.dumps(
            {
                "asset_id": ASSET_ID,
                "derived_glb_sha256": glb_hash,
                "derived_glb_bytes": derived_glb.stat().st_size,
                "status": result["status"],
                "render_profile": {
                    "engine": bpy.context.scene.render.engine,
                    "resolution": [args.resolution, args.resolution],
                    "view_transform": bpy.context.scene.view_settings.view_transform,
                    "look": bpy.context.scene.view_settings.look,
                    "projection": "orthographic",
                },
                "renders": renders,
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    result["review"]["manifest"] = {
        "path": repo_path(manifest_path, repository),
        "bytes": manifest_path.stat().st_size,
        "sha256": sha256(manifest_path),
    }

    print(json.dumps(result, indent=2, sort_keys=True))
    if not passed:
        raise RuntimeError(
            "Wall utility failed one or more Blender hard gates; "
            "see blender-validation.json"
        )
    return result


result = main()
