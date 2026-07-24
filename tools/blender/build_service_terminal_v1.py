"""Build the reversible Blender-authored service-terminal baseline.

Run inside Blender 5.2. The source is authored Z-up and faces -Y; Blender's
glTF export converts it to +Y up and -Z forward.
"""

from __future__ import annotations

import math
import os
from pathlib import Path

import bpy
from mathutils import Vector


REPOSITORY = Path(
    os.environ.get(
        "SPACE_ADVENTURE_REPOSITORY",
        str(Path(__file__).resolve().parents[2]),
    )
).resolve()
SOURCE_PATH = (
    REPOSITORY
    / "art"
    / "source"
    / "prop.station.service_terminal.v1"
    / "service-terminal-v1.blend"
)
GLB_PATH = Path(
    os.environ.get(
        "SPACE_ADVENTURE_BASELINE_GLB",
        str(
            REPOSITORY
            / "artifacts"
            / "reviews"
            / "prop.station.service_terminal.v1"
            / "blender-only-baseline"
            / "prop.station.service_terminal.v1.glb"
        ),
    )
).resolve()


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission_strength > 0.0:
        principled.inputs["Emission Color"].default_value = color
        principled.inputs["Emission Strength"].default_value = emission_strength
    return material


def make_vertex_color_emissive_material(name: str) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    principled = material.node_tree.nodes.get("Principled BSDF")
    color = material.node_tree.nodes.new("ShaderNodeVertexColor")
    color.layer_name = "Color"
    material.node_tree.links.new(color.outputs["Color"], principled.inputs["Base Color"])
    material.node_tree.links.new(color.outputs["Color"], principled.inputs["Emission Color"])
    principled.inputs["Metallic"].default_value = 0.05
    principled.inputs["Roughness"].default_value = 0.30
    principled.inputs["Emission Strength"].default_value = 2.0
    return material


def add_box(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    dimensions: tuple[float, float, float],
    location: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    bevel: float = 0.018,
    rotation_x_degrees: float = 0.0,
    vertex_color: tuple[float, float, float, float] | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=location,
        rotation=(math.radians(rotation_x_degrees), 0.0, 0.0),
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = f"{name}.mesh"
    obj.dimensions = dimensions
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    if bevel > 0.0:
        modifier = obj.modifiers.new(name="Bevel", type="BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(30.0)
        activate(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    for polygon in obj.data.polygons:
        polygon.use_smooth = False

    if vertex_color is not None:
        color_attribute = obj.data.color_attributes.new(
            name="Color",
            type="BYTE_COLOR",
            domain="CORNER",
        )
        for value in color_attribute.data:
            value.color = vertex_color

    obj.data.materials.append(material)
    obj.parent = root

    for current in tuple(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def bounds_for(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, world_corner.x)
            minimum.y = min(minimum.y, world_corner.y)
            minimum.z = min(minimum.z, world_corner.z)
            maximum.x = max(maximum.x, world_corner.x)
            maximum.y = max(maximum.y, world_corner.y)
            maximum.z = max(maximum.z, world_corner.z)
    return minimum, maximum


def join_by_material(objects: list[bpy.types.Object]) -> list[bpy.types.Object]:
    grouped: dict[str, list[bpy.types.Object]] = {}
    for obj in objects:
        material_name = obj.material_slots[0].material.name
        grouped.setdefault(material_name, []).append(obj)

    joined: list[bpy.types.Object] = []
    output_names = {
        "mat.station.wall.dark": "terminal.dark",
        "mat.station.trim.neutral": "terminal.trim",
        "mat.station.accent.vertex": "terminal.accent",
    }
    for material_name, group in grouped.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        bpy.ops.object.join()
        merged = bpy.context.active_object
        merged.name = output_names[material_name]
        merged.data.name = f"{merged.name}.mesh"
        joined.append(merged)
    return joined


def build() -> dict[str, object]:
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for collection in tuple(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for mesh in tuple(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for material in tuple(bpy.data.materials):
        bpy.data.materials.remove(material)
    for camera in tuple(bpy.data.cameras):
        bpy.data.cameras.remove(camera)
    for light in tuple(bpy.data.lights):
        bpy.data.lights.remove(light)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"

    asset_collection = bpy.data.collections.new("ASSET")
    scene.collection.children.link(asset_collection)

    root = bpy.data.objects.new("prop.station.service_terminal.v1", None)
    root.empty_display_type = "PLAIN_AXES"
    root["asset_id"] = "prop.station.service_terminal.v1"
    root["authoring_up"] = "+Z"
    root["authoring_front"] = "-Y"
    root["published_up"] = "+Y"
    root["published_front"] = "-Z"
    root["selection_status"] = "blender-baseline-provisional"
    asset_collection.objects.link(root)

    dark = make_material(
        "mat.station.wall.dark",
        (0.050, 0.080, 0.120, 1.0),
        metallic=0.32,
        roughness=0.70,
    )
    trim = make_material(
        "mat.station.trim.neutral",
        (0.30, 0.29, 0.27, 1.0),
        metallic=0.62,
        roughness=0.48,
    )
    accent = make_vertex_color_emissive_material("mat.station.accent.vertex")

    parts: list[bpy.types.Object] = []

    def box(
        name: str,
        dimensions: tuple[float, float, float],
        location: tuple[float, float, float],
        material: bpy.types.Material,
        *,
        bevel: float = 0.018,
        rotation_x_degrees: float = 0.0,
        vertex_color: tuple[float, float, float, float] | None = None,
    ) -> None:
        parts.append(
            add_box(
                asset_collection,
                root,
                name,
                dimensions,
                location,
                material,
                bevel=bevel,
                rotation_x_degrees=rotation_x_degrees,
                vertex_color=vertex_color,
            )
        )

    box("base.shell", (0.78, 0.39, 0.12), (0.0, 0.0, 0.06), dark, bevel=0.025)
    box("base.trim.left", (0.14, 0.40, 0.16), (-0.32, 0.0, 0.08), trim, bevel=0.022)
    box("base.trim.right", (0.14, 0.40, 0.16), (0.32, 0.0, 0.08), trim, bevel=0.022)

    box("body.lower", (0.58, 0.32, 0.62), (0.0, 0.025, 0.43), dark, bevel=0.025)
    box("body.mid", (0.66, 0.35, 0.32), (0.0, 0.005, 0.79), dark, bevel=0.022)
    box("rail.left", (0.085, 0.36, 0.92), (-0.31, 0.0, 0.62), trim, bevel=0.018)
    box("rail.right", (0.085, 0.36, 0.92), (0.31, 0.0, 0.62), trim, bevel=0.018)

    box("hood.shell", (0.64, 0.34, 0.20), (0.0, 0.025, 1.20), dark, bevel=0.028)
    box("hood.trim.left", (0.075, 0.35, 0.20), (-0.285, 0.02, 1.20), trim, bevel=0.016)
    box("hood.trim.right", (0.075, 0.35, 0.20), (0.285, 0.02, 1.20), trim, bevel=0.016)

    box(
        "screen.frame",
        (0.49, 0.035, 0.33),
        (0.0, -0.155, 0.985),
        trim,
        bevel=0.018,
        rotation_x_degrees=-15.0,
    )
    box(
        "screen.violet",
        (0.405, 0.022, 0.245),
        (0.0, -0.178, 0.982),
        accent,
        bevel=0.012,
        rotation_x_degrees=-15.0,
        vertex_color=(0.32, 0.075, 0.78, 1.0),
    )
    box(
        "status.cyan",
        (0.17, 0.022, 0.035),
        (0.0, -0.174, 0.790),
        accent,
        bevel=0.008,
        vertex_color=(0.0, 0.55, 0.82, 1.0),
    )

    box("hatch.frame", (0.39, 0.025, 0.36), (0.0, -0.160, 0.43), trim, bevel=0.016)
    box("hatch.panel", (0.325, 0.020, 0.295), (0.0, -0.174, 0.43), dark, bevel=0.014)
    box("hatch.handle", (0.11, 0.022, 0.025), (0.0, -0.188, 0.49), trim, bevel=0.006)
    box("hatch.vent", (0.16, 0.022, 0.022), (0.0, -0.188, 0.37), trim, bevel=0.005)

    box("back.frame", (0.42, 0.024, 0.54), (0.0, 0.162, 0.60), trim, bevel=0.016)
    box("back.panel", (0.36, 0.020, 0.48), (0.0, 0.176, 0.60), dark, bevel=0.013)

    bpy.context.view_layer.update()
    parts = join_by_material(parts)
    bpy.context.view_layer.update()
    minimum, maximum = bounds_for(parts)
    size = maximum - minimum

    if minimum.z < -0.0001:
        raise RuntimeError(f"Ground contract violated: minimum Z is {minimum.z}")
    if size.x > 0.816 or size.y > 0.428 or maximum.z > 1.326:
        raise RuntimeError(
            f"Envelope contract violated: size={tuple(size)}, max_z={maximum.z}"
        )

    triangles = sum(len(obj.data.loop_triangles) for obj in parts)
    if triangles == 0:
        for obj in parts:
            obj.data.calc_loop_triangles()
        triangles = sum(len(obj.data.loop_triangles) for obj in parts)

    SOURCE_PATH.parent.mkdir(parents=True, exist_ok=True)
    GLB_PATH.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.save_as_mainfile(
        filepath=str(SOURCE_PATH),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    export_result = bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        check_existing=False,
        export_format="GLB",
        export_copyright="SpaceAdventure bake-off comparator",
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
        export_vertex_color="MATERIAL",
        export_normals=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_skins=False,
        use_selection=True,
        export_extras=True,
    )

    bpy.ops.wm.save_as_mainfile(
        filepath=str(SOURCE_PATH),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )

    return {
        "asset_id": "prop.station.service_terminal.v1",
        "source_path": str(SOURCE_PATH),
        "glb_path": str(GLB_PATH),
        "objects": len(parts),
        "triangles": triangles,
        "materials": sorted({slot.material.name for obj in parts for slot in obj.material_slots}),
        "bounds_min_blender": tuple(round(value, 5) for value in minimum),
        "bounds_max_blender": tuple(round(value, 5) for value in maximum),
        "bounds_size_blender": tuple(round(value, 5) for value in size),
        "export_result": sorted(export_result),
    }


result = build()
