"""Render sampled frames from a Vanguard shared-rig retarget proof.

Blender 5.2 LTS:

    blender --background --factory-startup --python \
      tools/blender/render_vanguard_retarget_review_v1.py -- \
      <staging.glb> <action-name> <output-dir> <manifest.json>
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ASSET_ID = "character.crew.vanguard.v1"
PROFILE_ID = "review.blender.retarget.v1"


def paths() -> tuple[Path, str, Path, Path]:
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 4:
        raise SystemExit(
            "usage: script.py -- staging.glb action-name output-dir manifest.json"
        )
    glb, action, output, manifest = args
    return (
        Path(glb).resolve(),
        action,
        Path(output).resolve(),
        Path(manifest).resolve(),
    )


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        for corner in obj.bound_box
    ]
    return (
        Vector(min(point[axis] for point in points) for axis in range(3)),
        Vector(max(point[axis] for point in points) for axis in range(3)),
    )


def principled(name: str, color: tuple[float, float, float]):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = 0.82
    return material


def add_area(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    size: float,
    color: tuple[float, float, float],
    target: Vector,
) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    look_at(obj, target)


def main() -> None:
    glb, action_name, output_dir, manifest_path = paths()
    if not glb.is_file():
        raise FileNotFoundError(glb)
    if output_dir.exists() and any(output_dir.iterdir()):
        raise FileExistsError(f"Refusing non-empty output directory {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(glb))
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB import failed: {result}")
    meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and not any(
            collection.name == "glTF_not_exported"
            for collection in obj.users_collection
        )
    ]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 2 or len(armatures) != 1:
        raise RuntimeError(
            f"Expected two meshes and one armature, got {len(meshes)} / "
            f"{len(armatures)}"
        )
    action = bpy.data.actions.get(action_name)
    if action is None:
        raise RuntimeError(f"Missing action {action_name}")
    if action.frame_range[1] <= action.frame_range[0]:
        raise RuntimeError(f"Action {action_name} is not multi-frame")
    armature = armatures[0]
    armature.animation_data_create()
    armature.animation_data.action = action

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("RetargetReviewWorld")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.026, 0.043, 1.0)
    background.inputs["Strength"].default_value = 0.23

    scene.frame_set(int(math.floor(action.frame_range[0])))
    bpy.context.view_layer.update()
    minimum, maximum = world_bounds(meshes)
    center = (minimum + maximum) * 0.5
    span = max(maximum - minimum)

    ground_material = principled("RetargetReviewGround", (0.055, 0.068, 0.09))
    bpy.ops.mesh.primitive_plane_add(
        size=8.0,
        location=(0.0, 0.0, minimum.z - 0.004),
    )
    bpy.context.object.data.materials.append(ground_material)

    camera_data = bpy.data.cameras.new("RetargetReviewCamera")
    camera = bpy.data.objects.new("RetargetReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = (maximum.z - minimum.z) * 1.22
    camera.location = center + Vector((1.0, -1.0, 0.18)).normalized() * span * 2.6
    look_at(camera, center)
    scene.camera = camera

    add_area(
        "Key",
        (-span * 2.0, -span * 2.0, center.z + span * 2.0),
        420.0,
        2.4,
        (0.78, 0.88, 1.0),
        center,
    )
    add_area(
        "Fill",
        (span * 2.0, -span, center.z + span),
        200.0,
        2.8,
        (0.46, 0.67, 1.0),
        center,
    )
    add_area(
        "Rim",
        (span, span * 2.0, center.z + span * 2.0),
        360.0,
        2.0,
        (1.0, 0.43, 0.20),
        center,
    )

    start, end = (float(value) for value in action.frame_range)
    frames = sorted({
        int(round(start)),
        int(round(start + (end - start) * 0.25)),
        int(round(start + (end - start) * 0.50)),
        int(round(start + (end - start) * 0.75)),
        int(round(end)),
    })
    rendered = []
    for index, frame in enumerate(frames):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        filename = f"vanguard-retarget-{index + 1:02d}-frame-{frame:04d}.png"
        scene.render.filepath = str(output_dir / filename)
        bpy.ops.render.render(write_still=True)
        rendered.append(
            {
                "file": filename,
                "frame": frame,
                "bytes": (output_dir / filename).stat().st_size,
            }
        )

    manifest = {
        "asset_id": ASSET_ID,
        "render_profile": PROFILE_ID,
        "blender_version": bpy.app.version_string,
        "staging_glb": str(glb),
        "staging_glb_bytes": glb.stat().st_size,
        "action": action_name,
        "action_frame_range": [start, end],
        "sampled_frames": rendered,
        "camera": {
            "type": "orthographic",
            "direction": "front-right three-quarter",
            "resolution": [640, 640],
        },
    }
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("SPACE_ADVENTURE_RESULT=" + json.dumps(manifest, sort_keys=True))


if __name__ == "__main__":
    main()
