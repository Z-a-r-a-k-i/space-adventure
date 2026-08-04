"""Render one isolated Blender checkpoint image without modifying the source."""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PROFILE_SETTINGS = {
    "structure": {
        "required_meshes": {
            "Floor_StartRoom",
            "Floor_SoloCombatArena",
            "Floor_ProtectorRoom",
            "Floor_MainPartyArena",
            "Floor_FinalAirlockApproach",
        },
        "target": (-1.5, -4.5, 0.6),
        "camera_offset": (28, -32, 31),
        "ortho_scale": 35.5,
        "lights": (
            ("Key", (-10, -4, 38), 3600, 14),
            ("Fill", (24, -18, 18), 2600, 12),
            ("Rim", (-28, 12, 14), 2100, 10),
        ),
    },
    "service-door": {
        "required_meshes": {
            "Frame",
            "Door_Left",
            "Door_Right",
            "Status_Strip",
            "Control_Panel",
        },
        "target": (0, 0, 1.25),
        "camera_offset": (4.2, 6.5, 3.8),
        "ortho_scale": 4.15,
        "lights": (
            ("Key", (-3, 4, 6), 1050, 4),
            ("Fill", (4, 1, 3), 750, 3),
            ("Rim", (0, -4, 4), 900, 3),
        ),
    },
}


def parse_arguments() -> argparse.Namespace:
    arguments = []
    if "--" in sys.argv:
        arguments = sys.argv[sys.argv.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--profile", required=True, choices=("structure", "service-door"))
    return parser.parse_args(arguments)


def point_camera(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def add_area_light(name: str, location: tuple[float, float, float], energy: float, size: float) -> None:
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    point_camera(light, Vector((0, 0, 0.8)))


def main() -> None:
    arguments = parse_arguments()
    settings = PROFILE_SETTINGS[arguments.profile]
    required_meshes = settings["required_meshes"]
    present_meshes = {
        obj.name for obj in bpy.context.scene.objects if obj.type == "MESH"
    }
    missing_meshes = sorted(required_meshes - present_meshes)
    if missing_meshes:
        raise RuntimeError(
            f"Checkpoint profile '{arguments.profile}' is missing mesh objects: "
            f"{missing_meshes}"
        )

    output = Path(arguments.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("CheckpointWorld")
    scene.world.color = (0.012, 0.018, 0.028)

    camera_data = bpy.data.cameras.new("CheckpointCamera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("CheckpointCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera

    target = Vector(settings["target"])
    camera.location = target + Vector(settings["camera_offset"])
    camera_data.ortho_scale = settings["ortho_scale"]
    for name, location, energy, size in settings["lights"]:
        add_area_light(name, location, energy, size)

    point_camera(camera, target)
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.image_settings.color_mode = "RGBA"
    bpy.ops.render.render(write_still=True)
    if not output.exists() or output.stat().st_size == 0:
        raise RuntimeError(f"Checkpoint render was not written: {output}")
    print(f"SPACEADVENTURE_BLENDER_CHECKPOINT {output}")


if __name__ == "__main__":
    main()
