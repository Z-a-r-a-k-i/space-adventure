"""Normalize the selected Vanguard-carbine candidate in Blender 5.2.

The immutable Tripo GLB is preserved elsewhere. This script consumes the
versioned 7,396-triangle / 2K staging derivative, converts the baked texture
palette into three editable de-lit PBR materials, normalizes the weapon to the
brief envelope, authors the required marker frames, saves an editable .blend,
and exports a review GLB.

Run:

    blender --background --factory-startup \
      --python tools/blender/build_vanguard_carbine_v1.py -- \
      <input.glb> <output.blend> <output.glb> <report.json>

The script refuses to overwrite any output.
"""

from __future__ import annotations

import colorsys
import json
import math
import sys
from array import array
from collections import Counter, defaultdict, deque
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


ASSET_ID = "weapon.crew.vanguard_carbine.v1"
RUN_ID = "prod-tripo-v31bq-20260723-01"
TASK_ID = "01bb9aea-6b10-419d-bbeb-9648c9867a97"
REFERENCE_SHA256 = (
    "FE6CB280507202CD63E1B72EBF6F1E6329AD165AB1EA96E0B0E517D195C9B099"
)
TARGET_WIDTH = 0.13
TARGET_LENGTH = 0.82
TARGET_HEIGHT = 0.27


def parse_paths() -> tuple[Path, Path, Path, Path]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError(
            "Expected -- <input.glb> <output.blend> <output.glb> <report.json>"
        ) from exc
    values = [Path(value).resolve() for value in sys.argv[separator + 1 :]]
    if len(values) != 4:
        raise RuntimeError(
            "Expected four paths after --: input GLB, output blend, output GLB, "
            "and JSON report"
        )
    return values[0], values[1], values[2], values[3]


def ensure_non_overwriting(
    input_path: Path,
    output_paths: tuple[Path, ...],
) -> None:
    if not input_path.is_file():
        raise FileNotFoundError(input_path)
    collisions = [path for path in output_paths if path.exists()]
    if collisions:
        raise FileExistsError(
            "Refusing to overwrite: " + ", ".join(str(path) for path in collisions)
        )
    for path in output_paths:
        path.parent.mkdir(parents=True, exist_ok=True)


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


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


def vec(value: Vector) -> list[float]:
    return [round(float(component), 8) for component in value]


def make_material(
    name: str,
    base_color: tuple[float, float, float, float],
    *,
    metallic: float,
    roughness: float,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.use_nodes = True
    material.diffuse_color = base_color
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Principled BSDF missing from {name}")
    principled.inputs["Base Color"].default_value = base_color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission_strength:
        principled.inputs["Emission Color"].default_value = base_color
        principled.inputs["Emission Strength"].default_value = emission_strength
    material["palette_role"] = name.rsplit(".", 1)[-1]
    material["texture_set"] = "none"
    material["delit"] = True
    return material


def find_base_color_image(
    material: bpy.types.Material,
) -> bpy.types.Image:
    if not material.use_nodes or material.node_tree is None:
        raise RuntimeError("Imported material has no node tree")
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError("Imported material has no Principled BSDF")
    base_input = principled.inputs.get("Base Color")
    if base_input is None or not base_input.is_linked:
        raise RuntimeError("Imported material has no linked base-color image")
    node = base_input.links[0].from_node
    if node.type != "TEX_IMAGE" or node.image is None:
        raise RuntimeError("Imported base-color input is not an image texture")
    return node.image


def classify_material_regions(
    mesh_obj: bpy.types.Object,
    imported_material: bpy.types.Material,
) -> dict[str, object]:
    mesh = mesh_obj.data
    if len(mesh.uv_layers) != 1:
        raise RuntimeError(
            f"Expected one UV layer in selected candidate, found {len(mesh.uv_layers)}"
        )
    image = find_base_color_image(imported_material)
    width, height = int(image.size[0]), int(image.size[1])
    if width != 2048 or height != 2048:
        raise RuntimeError(
            f"Expected the staged 2048 texture, found {width}x{height}"
        )

    uv_data = mesh.uv_layers.active.data
    # Individual RNA pixel reads are prohibitively slow. Pull the texture into
    # one compact native float array, then sample that buffer.
    pixels = array("f", [0.0]) * (width * height * 4)
    image.pixels.foreach_get(pixels)

    def sample(uv: Vector) -> tuple[float, float, float]:
        u = min(max(float(uv.x), 0.0), 1.0)
        v = min(max(float(uv.y), 0.0), 1.0)
        x = min(width - 1, max(0, int(round(u * (width - 1)))))
        y = min(height - 1, max(0, int(round(v * (height - 1)))))
        offset = (y * width + x) * 4
        return (
            float(pixels[offset]),
            float(pixels[offset + 1]),
            float(pixels[offset + 2]),
        )

    labels: list[str] = []
    sampled_linear_rgb: list[tuple[float, float, float]] = []
    for polygon in mesh.polygons:
        colors = [sample(uv_data[index].uv) for index in polygon.loop_indices]
        count = float(len(colors))
        average = tuple(sum(color[channel] for color in colors) / count for channel in range(3))
        sampled_linear_rgb.append(average)
        red, green, blue = average
        hue, saturation, value = colorsys.rgb_to_hsv(red, green, blue)
        cyan = (
            0.47 <= hue <= 0.62
            and saturation >= 0.32
            and value >= 0.09
            and blue >= red * 1.22
        )
        luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue
        if cyan:
            labels.append("accent")
        elif luminance >= 0.16:
            labels.append("armor")
        else:
            labels.append("mechanism")

    edge_faces: dict[tuple[int, int], list[int]] = defaultdict(list)
    for polygon in mesh.polygons:
        for edge_key in polygon.edge_keys:
            edge_faces[tuple(sorted(edge_key))].append(polygon.index)
    neighbors: list[set[int]] = [set() for _ in mesh.polygons]
    for face_indices in edge_faces.values():
        if len(face_indices) == 2:
            left, right = face_indices
            neighbors[left].add(right)
            neighbors[right].add(left)

    # Remove isolated texture-lighting classifications without erasing the
    # deliberately sparse cyan accent.
    for _ in range(2):
        updated = list(labels)
        for face_index, label in enumerate(labels):
            if label == "accent":
                continue
            adjacent = [labels[index] for index in neighbors[face_index]]
            if len(adjacent) < 2:
                continue
            counts = Counter(adjacent)
            dominant, count = counts.most_common(1)[0]
            if dominant != "accent" and dominant != label and count >= max(
                2, math.ceil(len(adjacent) * 0.75)
            ):
                updated[face_index] = dominant
        labels = updated

    dark = make_material(
        "mat.weapon.vanguard_carbine.mechanism",
        (0.030, 0.045, 0.065, 1.0),
        metallic=0.46,
        roughness=0.54,
    )
    armor = make_material(
        "mat.weapon.vanguard_carbine.armor",
        (0.28, 0.25, 0.21, 1.0),
        metallic=0.58,
        roughness=0.46,
    )
    accent = make_material(
        "mat.weapon.vanguard_carbine.accent",
        (0.015, 0.43, 0.68, 1.0),
        metallic=0.18,
        roughness=0.30,
        emission_strength=0.65,
    )
    mesh.materials.clear()
    for material in (dark, armor, accent):
        mesh.materials.append(material)
    material_index = {"mechanism": 0, "armor": 1, "accent": 2}
    for polygon, label in zip(mesh.polygons, labels, strict=True):
        polygon.material_index = material_index[label]

    return {
        "source_image": image.name,
        "source_image_size": [width, height],
        "face_counts": dict(sorted(Counter(labels).items())),
        "sampled_linear_rgb_ranges": {
            channel: [
                round(min(values), 6),
                round(max(values), 6),
            ]
            for channel, values in zip(
                ("red", "green", "blue"),
                zip(*sampled_linear_rgb, strict=True),
                strict=True,
            )
        },
        "materials": [dark.name, armor.name, accent.name],
        "texture_sets_after_conversion": 0,
    }


def mesh_topology(mesh_obj: bpy.types.Object) -> dict[str, int]:
    mesh = mesh_obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()

    remaining = set(bm.verts)
    components = 0
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

    topology = {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "connected_components": components,
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(1 for edge in bm.edges if not edge.is_manifold),
        "loose_edges": sum(1 for edge in bm.edges if edge.is_wire),
        "zero_area_faces": sum(1 for face in bm.faces if face.calc_area() <= 1.0e-12),
    }
    bm.free()
    return topology


def reconstruct_manifold_surface(
    mesh_obj: bpy.types.Object,
    *,
    voxel_size: float = 0.0035,
    target_triangles: int = 7400,
) -> dict[str, object]:
    """Weld glTF seams, voxel-reconstruct, decimate, and restore materials."""

    before_weld = mesh_topology(mesh_obj)
    bm = bmesh.new()
    bm.from_mesh(mesh_obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1.0e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1.0e-10)
    bm.to_mesh(mesh_obj.data)
    bm.free()
    mesh_obj.data.update()
    after_weld = mesh_topology(mesh_obj)

    # UVs and the provider normal map are deliberately discarded after the
    # de-lit palette conversion. Their attribute splits are not part of the
    # production topology.
    for uv_layer in tuple(mesh_obj.data.uv_layers):
        mesh_obj.data.uv_layers.remove(uv_layer)

    source_vertices = [tuple(vertex.co) for vertex in mesh_obj.data.vertices]
    source_polygons = [tuple(polygon.vertices) for polygon in mesh_obj.data.polygons]
    source_material_indices = [
        int(polygon.material_index) for polygon in mesh_obj.data.polygons
    ]
    material_bvh = BVHTree.FromPolygons(
        source_vertices,
        source_polygons,
        all_triangles=True,
    )
    if material_bvh is None:
        raise RuntimeError("Could not create material-transfer BVH")

    activate(mesh_obj)
    remesh = mesh_obj.modifiers.new(name="ManifoldVoxelReconstruction", type="REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = voxel_size
    remesh.use_remove_disconnected = False
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    mesh_obj.data.calc_loop_triangles()
    remesh_triangles = len(mesh_obj.data.loop_triangles)
    if remesh_triangles <= target_triangles:
        raise RuntimeError(
            f"Voxel reconstruction unexpectedly produced only {remesh_triangles} triangles"
        )

    decimate = mesh_obj.modifiers.new(name="ProductionTriangleTarget", type="DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = target_triangles / remesh_triangles
    decimate.use_collapse_triangulate = True
    activate(mesh_obj)
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    mesh_obj.data.update()
    mesh_obj.data.calc_loop_triangles()

    unmatched = 0
    for polygon in mesh_obj.data.polygons:
        nearest = material_bvh.find_nearest(polygon.center)
        if nearest is None or nearest[2] is None:
            unmatched += 1
            polygon.material_index = 0
        else:
            polygon.material_index = source_material_indices[int(nearest[2])]
        polygon.use_smooth = True
    mesh_obj.data.update()

    # Voxel sampling shifts the extrema by sub-millimetre amounts. Reassert the
    # exact brief envelope and apply that final scale around the grip-root.
    mesh_obj.dimensions = (TARGET_WIDTH, TARGET_LENGTH, TARGET_HEIGHT)
    activate(mesh_obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.update()

    after_reconstruction = mesh_topology(mesh_obj)
    if after_reconstruction["boundary_edges"] != 0:
        raise RuntimeError(
            "Voxel reconstruction left boundary edges: "
            f"{after_reconstruction['boundary_edges']}"
        )
    if after_reconstruction["non_manifold_edges"] != 0:
        raise RuntimeError(
            "Voxel reconstruction left non-manifold edges: "
            f"{after_reconstruction['non_manifold_edges']}"
        )
    if unmatched:
        raise RuntimeError(f"Material transfer missed {unmatched} reconstructed faces")

    return {
        "operation": "weld -> 3.5 mm voxel reconstruction -> collapse decimation",
        "voxel_size_m": voxel_size,
        "target_triangles": target_triangles,
        "before_weld": before_weld,
        "after_weld": after_weld,
        "voxel_triangles_before_decimation": remesh_triangles,
        "after_reconstruction": after_reconstruction,
        "material_face_counts": dict(
            sorted(
                Counter(
                    mesh_obj.material_slots[polygon.material_index].material.name
                    for polygon in mesh_obj.data.polygons
                ).items()
            )
        ),
    }


def apply_authored_palette_mask(mesh_obj: bpy.types.Object) -> dict[str, object]:
    """Consolidate texture-derived labels into readable authored regions."""

    before = Counter(
        mesh_obj.material_slots[polygon.material_index].material.name
        for polygon in mesh_obj.data.polygons
    )
    for polygon in mesh_obj.data.polygons:
        # Preserve the sparse texture-derived cyan status region. Broad armor
        # and mechanism regions are authored from the normalized form so baked
        # texture highlights cannot create triangle-sized color noise.
        if polygon.material_index == 2:
            continue
        center = polygon.center
        armor_region = center.y <= 0.18 and (
            (center.y > 0.10 and center.z > 0.075)
            or (-0.48 < center.y <= 0.10 and center.z > 0.09)
            or (
                abs(center.x) > 0.042
                and -0.42 < center.y <= 0.10
                and center.z > 0.055
            )
        )
        polygon.material_index = 1 if armor_region else 0
    mesh_obj.data.update()
    after = Counter(
        mesh_obj.material_slots[polygon.material_index].material.name
        for polygon in mesh_obj.data.polygons
    )
    return {
        "purpose": (
            "replace baked-lighting triangle noise with broad warm-armor and "
            "dark-mechanism regions while preserving sparse cyan status faces"
        ),
        "coordinate_space": "normalized Blender authoring coordinates",
        "before_face_counts": dict(sorted(before.items())),
        "after_face_counts": dict(sorted(after.items())),
    }


def add_marker(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    location: Vector,
    display_size: float,
) -> bpy.types.Object:
    marker = bpy.data.objects.new(name=name, object_data=None)
    marker.empty_display_type = "ARROWS"
    marker.empty_display_size = display_size
    marker.location = location
    marker.rotation_mode = "QUATERNION"
    marker.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
    marker.scale = (1.0, 1.0, 1.0)
    marker.parent = root
    marker.matrix_parent_inverse = Matrix.Identity(4)
    marker["marker_kind"] = "presentation_frame"
    marker["published_local_forward"] = "-Z"
    marker["published_local_up"] = "+Y"
    marker["authoring_local_forward"] = "-Y"
    marker["authoring_local_up"] = "+Z"
    collection.objects.link(marker)
    return marker


def build(
    input_path: Path,
    source_path: Path,
    glb_path: Path,
    report_path: Path,
) -> dict[str, object]:
    ensure_non_overwriting(input_path, (source_path, glb_path, report_path))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Vanguard Carbine v1"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"

    import_result = bpy.ops.import_scene.gltf(
        filepath=str(input_path),
        import_pack_images=True,
        import_shading="NORMALS",
    )
    bpy.context.view_layer.update()

    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one staged mesh, found {len(meshes)}")
    if armatures or bpy.data.actions:
        raise RuntimeError(
            f"Rigid weapon input unexpectedly contains {len(armatures)} armatures "
            f"and {len(bpy.data.actions)} actions"
        )
    mesh_obj = meshes[0]
    if len(mesh_obj.material_slots) != 1 or mesh_obj.material_slots[0].material is None:
        raise RuntimeError("Expected one imported textured material")
    imported_material = mesh_obj.material_slots[0].material
    classification = classify_material_regions(mesh_obj, imported_material)

    mesh_obj.name = "weapon.vanguard_carbine.body"
    mesh_obj.data.name = "weapon.vanguard_carbine.body.mesh"
    mesh_obj.rotation_mode = "XYZ"
    mesh_obj.rotation_euler = (0.0, 0.0, math.radians(-90.0))
    activate(mesh_obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bpy.context.view_layer.update()

    oriented_minimum, oriented_maximum = object_bounds([mesh_obj])
    oriented_size = oriented_maximum - oriented_minimum
    mesh_obj.scale = (
        TARGET_WIDTH / oriented_size.x,
        TARGET_LENGTH / oriented_size.y,
        TARGET_HEIGHT / oriented_size.z,
    )
    activate(mesh_obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.update()

    normalized_minimum, normalized_maximum = object_bounds([mesh_obj])
    normalized_size = normalized_maximum - normalized_minimum
    primary_grip_world = Vector(
        (
            (normalized_minimum.x + normalized_maximum.x) * 0.5,
            normalized_minimum.y + TARGET_LENGTH * 0.72,
            normalized_minimum.z + TARGET_HEIGHT * 0.31,
        )
    )
    support_grip_world = Vector(
        (
            (normalized_minimum.x + normalized_maximum.x) * 0.5,
            normalized_minimum.y + TARGET_LENGTH * 0.30,
            normalized_minimum.z + TARGET_HEIGHT * 0.18,
        )
    )
    muzzle_world = Vector(
        (
            (normalized_minimum.x + normalized_maximum.x) * 0.5,
            normalized_minimum.y,
            normalized_minimum.z + TARGET_HEIGHT * 0.71,
        )
    )

    mesh_obj.data.transform(Matrix.Translation(-primary_grip_world))
    mesh_obj.data.update()
    support_location = support_grip_world - primary_grip_world
    muzzle_location = muzzle_world - primary_grip_world
    bpy.context.view_layer.update()

    reconstruction = reconstruct_manifold_surface(mesh_obj)
    palette_mask = apply_authored_palette_mask(mesh_obj)
    reconstructed_minimum, reconstructed_maximum = object_bounds([mesh_obj])
    support_location = Vector(
        (
            (reconstructed_minimum.x + reconstructed_maximum.x) * 0.5,
            reconstructed_minimum.y + TARGET_LENGTH * 0.30,
            reconstructed_minimum.z + TARGET_HEIGHT * 0.18,
        )
    )
    muzzle_location = Vector(
        (
            (reconstructed_minimum.x + reconstructed_maximum.x) * 0.5,
            reconstructed_minimum.y,
            reconstructed_minimum.z + TARGET_HEIGHT * 0.71,
        )
    )

    asset_collection = bpy.data.collections.new("ASSET")
    scene.collection.children.link(asset_collection)
    for collection in tuple(mesh_obj.users_collection):
        collection.objects.unlink(mesh_obj)
    asset_collection.objects.link(mesh_obj)

    root = bpy.data.objects.new(name=ASSET_ID, object_data=None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    root.location = (0.0, 0.0, 0.0)
    root.rotation_mode = "QUATERNION"
    root.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)
    root["asset_id"] = ASSET_ID
    root["category"] = "separate_rigid_handheld_weapon"
    root["selection_status"] = "provisional_pending_owner_visual_review"
    root["authoring_up"] = "+Z"
    root["authoring_forward"] = "-Y"
    root["published_up"] = "+Y"
    root["published_forward"] = "-Z"
    root["source_run_id"] = RUN_ID
    root["source_task_id"] = TASK_ID
    root["input_bytes"] = input_path.stat().st_size
    root["reference_sha256"] = REFERENCE_SHA256
    root["gameplay_attack_reference"] = "pending_not_invented"
    asset_collection.objects.link(root)

    mesh_obj.parent = root
    mesh_obj.matrix_parent_inverse = Matrix.Identity(4)
    mesh_obj.location = (0.0, 0.0, 0.0)
    mesh_obj.rotation_mode = "QUATERNION"
    mesh_obj.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
    mesh_obj.scale = (1.0, 1.0, 1.0)
    mesh_obj["rigidity"] = "rigid"
    mesh_obj["source_operation"] = (
        "meshoptimizer simplification followed by Blender normalization"
    )

    primary = add_marker(
        asset_collection,
        root,
        "socket.grip.primary",
        Vector((0.0, 0.0, 0.0)),
        0.045,
    )
    support = add_marker(
        asset_collection,
        root,
        "socket.grip.support",
        support_location,
        0.04,
    )
    muzzle = add_marker(
        asset_collection,
        root,
        "socket.attack.muzzle.primary",
        muzzle_location,
        0.05,
    )

    if imported_material.users == 0:
        bpy.data.materials.remove(imported_material)
    for image in tuple(bpy.data.images):
        if image.users == 0 and image.name not in {"Render Result", "Viewer Node"}:
            bpy.data.images.remove(image)

    bpy.context.view_layer.update()
    minimum, maximum = object_bounds([mesh_obj])
    size = maximum - minimum
    expected = Vector((TARGET_WIDTH, TARGET_LENGTH, TARGET_HEIGHT))
    if max(abs(size[index] - expected[index]) for index in range(3)) > 1.0e-6:
        raise RuntimeError(f"Envelope mismatch after normalization: {tuple(size)}")
    if any(abs(value - 1.0) > 1.0e-8 for value in mesh_obj.scale):
        raise RuntimeError(f"Mesh scale was not applied: {tuple(mesh_obj.scale)}")
    if any(abs(value) > 1.0e-8 for value in mesh_obj.location):
        raise RuntimeError(f"Mesh object location is not zero: {tuple(mesh_obj.location)}")
    if any(abs(primary.location[index]) > 1.0e-8 for index in range(3)):
        raise RuntimeError("Primary grip is not coincident with the asset root")

    topology = mesh_topology(mesh_obj)
    if not 4000 <= topology["triangles"] <= 8000:
        raise RuntimeError(
            f"Triangle target violated after Blender import: {topology['triangles']}"
        )
    if len(mesh_obj.material_slots) > 4:
        raise RuntimeError("Material hard limit violated")
    if bpy.data.armatures or bpy.data.actions:
        raise RuntimeError("Weapon source contains armature or animation data")

    bpy.ops.wm.save_as_mainfile(
        filepath=str(source_path),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )

    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [root, mesh_obj, primary, support, muzzle]
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    export_result = bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        check_existing=False,
        export_format="GLB",
        export_copyright="SpaceAdventure provisional production asset",
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
        export_vertex_color="MATERIAL",
        export_normals=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_skins=False,
        export_extras=True,
        use_selection=True,
    )
    if not glb_path.is_file():
        raise RuntimeError(f"Blender did not create {glb_path}")

    # Save once more so the editable source records the exact export selection.
    bpy.ops.wm.save_as_mainfile(
        filepath=str(source_path),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )

    published_minimum = Vector((minimum.x, minimum.z, -maximum.y))
    published_maximum = Vector((maximum.x, maximum.z, -minimum.y))
    report = {
        "asset_id": ASSET_ID,
        "status": "normalized review derivative; not yet published",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "input": {
            "path": str(input_path),
            "bytes": input_path.stat().st_size,
            "run_id": RUN_ID,
            "task_id": TASK_ID,
            "approved_reference_sha256": REFERENCE_SHA256,
        },
        "outputs": {
            "source_blend": str(source_path),
            "source_blend_bytes": source_path.stat().st_size,
            "review_glb": str(glb_path),
            "review_glb_bytes": glb_path.stat().st_size,
        },
        "import_result": sorted(import_result),
        "export_result": sorted(export_result),
        "geometry": topology,
        "mesh_objects": 1,
        "materials": [
            slot.material.name
            for slot in mesh_obj.material_slots
            if slot.material is not None
        ],
        "material_count": len(
            [slot for slot in mesh_obj.material_slots if slot.material is not None]
        ),
        "texture_sets": 0,
        "armatures": 0,
        "actions": 0,
        "classification": classification,
        "reconstruction": reconstruction,
        "authored_palette_mask": palette_mask,
        "authoring_contract": {
            "units": "meters",
            "up": "+Z",
            "forward": "-Y",
            "root": "coincident with socket.grip.primary",
            "bounds_min_m": vec(minimum),
            "bounds_max_m": vec(maximum),
            "bounds_size_xyz_m": vec(size),
            "semantic_envelope_m": {
                "length": round(TARGET_LENGTH, 8),
                "width": round(TARGET_WIDTH, 8),
                "height": round(TARGET_HEIGHT, 8),
            },
        },
        "published_contract": {
            "up": "+Y",
            "forward": "-Z",
            "bounds_min_xyz_m": vec(published_minimum),
            "bounds_max_xyz_m": vec(published_maximum),
            "bounds_size_xyz_m": vec(published_maximum - published_minimum),
            "semantic_envelope_m": {
                "length": round(TARGET_LENGTH, 8),
                "width": round(TARGET_WIDTH, 8),
                "height": round(TARGET_HEIGHT, 8),
            },
        },
        "markers_authoring": {
            primary.name: {
                "location_m": vec(primary.location),
                "local_forward": "-Y",
                "local_up": "+Z",
            },
            support.name: {
                "location_m": vec(support.location),
                "local_forward": "-Y",
                "local_up": "+Z",
            },
            muzzle.name: {
                "location_m": vec(muzzle.location),
                "local_forward": "-Y",
                "local_up": "+Z",
            },
        },
        "gameplay_attack_reference": "pending; not invented",
    }
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    report["report_path"] = str(report_path)
    return report


input_glb, output_blend, output_glb, output_report = parse_paths()
result = build(input_glb, output_blend, output_glb, output_report)
print(json.dumps(result, indent=2, sort_keys=True))
