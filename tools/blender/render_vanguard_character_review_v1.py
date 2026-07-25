"""Render the standardized Blender review set for the published Vanguard GLB.

Blender 5.2 LTS:
  blender --background --factory-startup --python \
    tools/blender/render_vanguard_character_review_v1.py -- \
    <published.glb> <editable.blend> <output-dir> <manifest.json>

All beauty, tactical, wireframe, and rig overlays are rendered from a fresh
import of the exact staged GLB. The editable .blend is recorded by path and
byte size; it is not used as the beauty-render source.
"""

from __future__ import annotations

import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector


ASSET_ID = "character.crew.vanguard.v1"
PROFILE_ID = "review.blender.character.v1"
SOCKET_NAMES = {
    "socket.weapon.hand_primary",
    "socket.weapon.holster_primary",
}


def cli_paths() -> tuple[Path, Path, Path, Path]:
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 4:
        raise SystemExit(
            "usage: script.py -- published.glb editable.blend output-dir manifest.json"
        )
    return tuple(Path(value).resolve() for value in args)  # type: ignore[return-value]


def rounded(value) -> list[float]:
    return [round(float(component), 6) for component in value]


def look_at(obj, target) -> None:
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def world_bounds(mesh_objects) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in mesh_objects
        for corner in obj.bound_box
    ]
    return (
        Vector(min(point[i] for point in points) for i in range(3)),
        Vector(max(point[i] for point in points) for i in range(3)),
    )


def make_principled_material(
    name: str,
    color,
    metallic: float = 0.0,
    roughness: float = 0.5,
    emission=None,
):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    if emission is not None and "Emission Color" in shader.inputs:
        shader.inputs["Emission Color"].default_value = (*emission, 1.0)
        shader.inputs["Emission Strength"].default_value = 3.0
    return material


def make_wire_material():
    material = bpy.data.materials.new("Review.Wireframe")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    wire = nodes.new("ShaderNodeWireframe")
    wire.inputs["Size"].default_value = 0.8
    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MIX"
    mix.inputs[1].default_value = (0.025, 0.035, 0.055, 1.0)
    mix.inputs[2].default_value = (0.12, 0.85, 1.0, 1.0)
    shader.inputs["Roughness"].default_value = 0.78
    shader.inputs["Metallic"].default_value = 0.08
    links.new(wire.outputs["Fac"], mix.inputs[0])
    links.new(mix.outputs["Color"], shader.inputs["Base Color"])
    if "Emission Color" in shader.inputs:
        links.new(mix.outputs["Color"], shader.inputs["Emission Color"])
        shader.inputs["Emission Strength"].default_value = 0.35
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def add_area(name, location, energy, size, color, target):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    look_at(obj, target)
    return obj


def configure_scene(output_dir: Path, minimum: Vector, maximum: Vector):
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.filepath = str(output_dir / "vanguard-front.png")

    if scene.world is None:
        scene.world = bpy.data.worlds.new("ReviewWorld")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.026, 0.043, 1.0)
    background.inputs["Strength"].default_value = 0.23

    center = (minimum + maximum) * 0.5
    height = maximum.z - minimum.z
    span = max(height, maximum.x - minimum.x, maximum.y - minimum.y)
    ground_material = make_principled_material(
        "Review.Ground", (0.055, 0.068, 0.09), roughness=0.83
    )
    bpy.ops.mesh.primitive_plane_add(
        size=8.0, location=(0.0, 0.0, minimum.z - 0.004)
    )
    ground = bpy.context.object
    ground.name = "ReviewGround"
    ground.data.materials.append(ground_material)

    camera_data = bpy.data.cameras.new("ReviewCamera")
    camera = bpy.data.objects.new("ReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height * 1.16
    camera_data.lens = 50
    scene.camera = camera

    target = (center.x, center.y, center.z)
    distance = span * 2.0
    add_area(
        "Key",
        (-distance, -distance, center.z + distance),
        420.0,
        2.4,
        (0.78, 0.88, 1.0),
        target,
    )
    add_area(
        "Fill",
        (distance, -distance * 0.55, center.z + distance * 0.45),
        200.0,
        2.8,
        (0.46, 0.67, 1.0),
        target,
    )
    add_area(
        "Rim",
        (distance * 0.35, distance, center.z + distance),
        360.0,
        2.0,
        (1.0, 0.43, 0.20),
        target,
    )
    return scene, camera, ground, center, span


def render_ortho(
    scene,
    camera,
    center,
    span,
    output_dir,
    name: str,
    direction,
    target=None,
    ortho_scale=None,
):
    direction = Vector(direction).normalized()
    target = center if target is None else Vector(target)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = span * 1.16 if ortho_scale is None else ortho_scale
    camera.location = target + direction * span * 2.5
    look_at(camera, target)
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.filepath = str(output_dir / f"vanguard-{name}.png")
    bpy.ops.render.render(write_still=True)


def render_tactical(
    scene,
    camera,
    output_dir,
    distance_meters: float,
    pitch_radians: float = 0.90,
):
    target = Vector((0.0, 0.0, 0.88))
    horizontal = distance_meters * math.cos(pitch_radians)
    vertical = distance_meters * math.sin(pitch_radians)
    camera.data.type = "PERSP"
    camera.data.lens_unit = "FOV"
    camera.data.angle = math.radians(48.0)
    camera.location = Vector(
        (horizontal * 0.32, -horizontal * 0.947, target.z + vertical)
    )
    look_at(camera, target)
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    label = str(distance_meters).replace(".", "-")
    scene.render.filepath = str(
        output_dir / f"vanguard-tactical-{label}m.png"
    )
    bpy.ops.render.render(write_still=True)


def add_bone_rod(name, start, end, radius, material, collection):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    if direction.length < 0.0001:
        return None
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6,
        radius=radius,
        depth=direction.length,
        end_fill_type="NGON",
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.data.materials.append(material)
    for current_collection in list(obj.users_collection):
        current_collection.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def create_rig_overlay(armature_obj):
    collection = bpy.data.collections.new("Review.RigOverlay")
    bpy.context.scene.collection.children.link(collection)
    bone_material = make_principled_material(
        "Review.RigBone",
        (0.04, 0.52, 0.72),
        metallic=0.2,
        roughness=0.34,
        emission=(0.04, 0.52, 0.72),
    )
    socket_material = make_principled_material(
        "Review.RigSocket",
        (1.0, 0.26, 0.04),
        metallic=0.1,
        roughness=0.3,
        emission=(1.0, 0.18, 0.02),
    )
    for bone in armature_obj.data.bones:
        start = armature_obj.matrix_world @ bone.head_local
        end = armature_obj.matrix_world @ bone.tail_local
        is_socket = bone.name in SOCKET_NAMES
        add_bone_rod(
            f"ReviewBone.{bone.name}",
            start,
            end,
            0.010 if is_socket else 0.006,
            socket_material if is_socket else bone_material,
            collection,
        )
    return collection


def set_collection_hidden(collection, hidden: bool) -> None:
    collection.hide_render = hidden
    collection.hide_viewport = hidden


def set_action(armature_obj, action_name: str | None) -> None:
    armature_obj.animation_data_create()
    armature_obj.animation_data.action = (
        bpy.data.actions.get(action_name) if action_name else None
    )
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()


def add_scale_bar():
    material = make_principled_material(
        "Review.ScaleBar",
        (1.0, 0.33, 0.06),
        metallic=0.05,
        roughness=0.45,
        emission=(0.8, 0.12, 0.01),
    )
    objects = []
    bpy.ops.mesh.primitive_cube_add(
        location=(-0.68, 0.0, 1.0), scale=(0.012, 0.012, 1.0)
    )
    bar = bpy.context.object
    bar.name = "Review.ScaleBar.2m"
    bar.data.materials.append(material)
    objects.append(bar)
    for z in (0.0, 0.5, 1.0, 1.5, 2.0):
        bpy.ops.mesh.primitive_cube_add(
            location=(-0.64, 0.0, z), scale=(0.05, 0.012, 0.008)
        )
        tick = bpy.context.object
        tick.name = f"Review.ScaleTick.{z:.1f}m"
        tick.data.materials.append(material)
        objects.append(tick)
    return objects


def set_objects_hidden(objects, hidden: bool) -> None:
    for obj in objects:
        obj.hide_render = hidden
        obj.hide_viewport = hidden


def main() -> None:
    glb_path, blend_path, output_dir, manifest_path = cli_paths()
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    if not glb_path.is_file() or not blend_path.is_file():
        raise FileNotFoundError("Published GLB or editable blend is missing")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(glb_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB import failed: {result}")
    mesh_objects = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and not any(
            collection.name == "glTF_not_exported"
            for collection in obj.users_collection
        )
    ]
    armatures = [
        obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"
    ]
    if len(mesh_objects) != 2 or len(armatures) != 1:
        raise RuntimeError(
            f"Expected two product meshes and one rig, got "
            f"{len(mesh_objects)} / {len(armatures)}"
        )
    armature_obj = armatures[0]
    set_action(armature_obj, "anim.humanoid.idle_holstered")
    minimum, maximum = world_bounds(mesh_objects)
    scene, camera, ground, center, span = configure_scene(
        output_dir, minimum, maximum
    )
    view_layer = scene.view_layers[0]

    beauty_views = {
        "front": (0.0, -1.0, 0.0),
        "back": (0.0, 1.0, 0.0),
        "left": (-1.0, 0.0, 0.0),
        "right": (1.0, 0.0, 0.0),
        "front-left-3q": (-1.0, -1.0, 0.18),
        "front-right-3q": (1.0, -1.0, 0.18),
    }
    for name, direction in beauty_views.items():
        render_ortho(
            scene, camera, center, span, output_dir, name, direction
        )

    ground.hide_render = True
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "top",
        (0.0, 0.0, 1.0),
    )
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "underside",
        (0.0, 0.0, -1.0),
    )
    ground.hide_render = False

    for distance in (7.5, 14.5, 20.0):
        render_tactical(scene, camera, output_dir, distance)

    wire_material = make_wire_material()
    view_layer.material_override = wire_material
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "wire-front-right-3q",
        (1.0, -1.0, 0.18),
    )
    torso_target = Vector((0.0, 0.0, 1.22))
    render_ortho(
        scene,
        camera,
        torso_target,
        span,
        output_dir,
        "wire-torso-close",
        (1.0, -1.0, 0.10),
        target=torso_target,
        ortho_scale=0.95,
    )
    view_layer.material_override = None

    scale_objects = add_scale_bar()
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "scale-reference-2m",
        (0.0, -1.0, 0.0),
        ortho_scale=2.35,
    )
    set_objects_hidden(scale_objects, True)

    rig_collection = create_rig_overlay(armature_obj)
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "rig-rest-front-right-3q",
        (1.0, -1.0, 0.18),
    )
    socket_target = Vector((0.18, -0.01, 0.88))
    render_ortho(
        scene,
        camera,
        socket_target,
        span,
        output_dir,
        "rig-sockets-close",
        (1.0, -1.0, 0.06),
        target=socket_target,
        ortho_scale=0.78,
    )
    set_collection_hidden(rig_collection, True)

    set_action(armature_obj, "anim.humanoid.raise_aim")
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "pose-raise-aim-unbound",
        (1.0, -1.0, 0.18),
    )
    set_action(armature_obj, "anim.humanoid.hit_reaction")
    render_ortho(
        scene,
        camera,
        center,
        span,
        output_dir,
        "pose-hit-reaction-unbound",
        (1.0, -1.0, 0.18),
    )
    set_action(armature_obj, "anim.humanoid.idle_holstered")

    output_files = sorted(output_dir.glob("vanguard-*.png"))
    manifest = {
        "asset_id": ASSET_ID,
        "render_profile": PROFILE_ID,
        "blender_version": bpy.app.version_string,
        "published_glb": {
            "path": str(glb_path),
            "size_bytes": glb_path.stat().st_size,
        },
        "editable_source": {
            "path": str(blend_path),
            "size_bytes": blend_path.stat().st_size,
        },
        "render_script": {
            "path": str(Path(__file__).resolve()),
        },
        "bounds_fresh_import_blender_z_up": {
            "minimum": rounded(minimum),
            "maximum": rounded(maximum),
            "dimensions": rounded(maximum - minimum),
        },
        "camera": {
            "beauty": "orthographic fixed framing",
            "tactical_fov_degrees": 48.0,
            "tactical_pitch_radians": 0.90,
            "tactical_distances_meters": [7.5, 14.5, 20.0],
            "tactical_resolution": [1280, 720],
        },
        "rig": {
            "bone_count": len(armature_obj.data.bones),
            "sockets": sorted(
                name
                for name in SOCKET_NAMES
                if name in armature_obj.data.bones
            ),
            "action_count": len(bpy.data.actions),
            "actions": sorted(action.name for action in bpy.data.actions),
            "pose_review_status": (
                "generic unbound one-frame presentation poses; no gameplay "
                "attack phase or duration asserted"
            ),
        },
        "views": [
            {
                "file": file.name,
                "size_bytes": file.stat().st_size,
            }
            for file in output_files
        ],
    }
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(
        {
            "asset_id": ASSET_ID,
            "glb_size_bytes": manifest["published_glb"]["size_bytes"],
            "render_count": len(output_files),
            "manifest": str(manifest_path),
        },
        indent=2,
    ))


if __name__ == "__main__":
    main()
