"""Export and structurally validate the migrated Vanguard Phase 3 sources.

Run with Blender 5.2 LTS:

    blender --background --factory-startup \
      --python tools/blender/export_validate_vanguard_sources_v1.py -- \
      <vanguard.blend> <carbine.blend> \
      <vanguard-staging.glb> <carbine-staging.glb> <report.json>

The script refuses to overwrite outputs. It records paths and byte sizes but
does not content-hash any Blend or GLB.
"""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


CHARACTER_ID = "character.crew.vanguard.v1"
CHARACTER_RUN_ID = (
    "prod-tripo-v31bq-20260723-01"
)
CHARACTER_TASK_ID = "c889d05a-90fe-4186-85eb-12d4eceafb35"
WEAPON_ID = "weapon.crew.vanguard_carbine.v1"
WEAPON_RUN_ID = (
    "prod-tripo-v31bq-20260723-01"
)
WEAPON_TASK_ID = "01bb9aea-6b10-419d-bbeb-9648c9867a97"

CHARACTER_MATERIALS = {
    "mat.vanguard.surface.pbr",
    "mat.vanguard.undersuit.navy",
}
WEAPON_MATERIALS = {
    "mat.vanguard_carbine.mechanism.dark",
    "mat.vanguard_carbine.armor.warm_gray",
    "mat.vanguard_carbine.accent.cyan",
}
WEAPON_MARKERS = {
    "socket.grip.primary",
    "socket.grip.support",
    "socket.attack.muzzle.primary",
}
CHARACTER_SOCKETS = {
    "socket.weapon.hand_primary",
    "socket.weapon.holster_primary",
}
ACTION_NAMES = {
    "anim.humanoid.idle_holstered",
    "anim.humanoid.locomotion_holstered",
    "anim.humanoid.draw",
    "anim.humanoid.idle_armed",
    "anim.humanoid.locomotion_armed",
    "anim.humanoid.raise_aim",
    "anim.humanoid.fire_recoil",
    "anim.humanoid.recovery",
    "anim.humanoid.holster",
    "anim.humanoid.dialogue_idle",
    "anim.humanoid.dialogue_speak",
    "anim.humanoid.dialogue_listen",
    "anim.humanoid.interact_terminal",
    "anim.humanoid.use_healing",
    "anim.humanoid.hit_reaction",
    "anim.humanoid.down",
}


def parse_paths() -> tuple[Path, Path, Path, Path, Path]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError(
            "Expected -- <vanguard.blend> <carbine.blend> "
            "<vanguard.glb> <carbine.glb> <report.json>"
        ) from exc
    values = [Path(value).resolve() for value in sys.argv[separator + 1 :]]
    if len(values) != 5:
        raise RuntimeError("Expected five paths after --")
    for source in values[:2]:
        if not source.is_file():
            raise FileNotFoundError(source)
    collisions = [path for path in values[2:] if path.exists()]
    if collisions:
        raise FileExistsError(
            "Refusing to overwrite: " + ", ".join(str(path) for path in collisions)
        )
    for output in values[2:]:
        output.parent.mkdir(parents=True, exist_ok=True)
    return values[0], values[1], values[2], values[3], values[4]


def rounded(vector: Vector) -> list[float]:
    return [round(float(component), 8) for component in vector]


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


def triangles(objects: list[bpy.types.Object]) -> int:
    return sum(
        max(0, len(polygon.vertices) - 2)
        for obj in objects
        for polygon in obj.data.polygons
    )


def material_names(objects: list[bpy.types.Object]) -> set[str]:
    return {
        material.name
        for obj in objects
        for material in obj.data.materials
        if material is not None
    }


def custom_hash_fields() -> list[str]:
    allowed_small_file_fields = {"reference_sha256"}
    owners = [
        ("scene", bpy.context.scene),
        *((f"object:{obj.name}", obj) for obj in bpy.data.objects),
        *((f"mesh:{mesh.name}", mesh) for mesh in bpy.data.meshes),
        *((f"material:{material.name}", material) for material in bpy.data.materials),
    ]
    return sorted(
        f"{owner_name}.{key}"
        for owner_name, owner in owners
        for key in owner.keys()
        if (
            ("sha" in key.lower() or "hash" in key.lower())
            and key.lower() not in allowed_small_file_fields
        )
    )


def max_influences(meshes: list[bpy.types.Object]) -> tuple[int, int]:
    maximum = 0
    unweighted = 0
    for obj in meshes:
        for vertex in obj.data.vertices:
            count = sum(1 for group in vertex.groups if group.weight > 1.0e-6)
            maximum = max(maximum, count)
            if count == 0:
                unweighted += 1
    return maximum, unweighted


def export_character(source: Path, output: Path) -> dict[str, object]:
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    scene = bpy.context.scene
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    if scene.get("asset_id") != CHARACTER_ID:
        raise RuntimeError("Vanguard source asset ID mismatch")
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one Vanguard armature, got {len(armatures)}")

    armature = armatures[0]
    bones = {bone.name for bone in armature.data.bones}
    materials = material_names(meshes)
    actions = set(bpy.data.actions.keys())
    unexpected_hash_fields = custom_hash_fields()
    influence_count, unweighted = max_influences(meshes)
    minimum, maximum = world_bounds(meshes)
    dimensions = maximum - minimum

    checks = {
        "height_within_two_percent": abs(dimensions.z - 1.82) <= 1.82 * 0.02,
        "grounded": abs(minimum.z) <= 0.005,
        "triangles_within_hard_limit": triangles(meshes) <= 35_000,
        "materials_exact": materials == CHARACTER_MATERIALS,
        "bones_within_limit": len(bones) <= 64,
        "sockets_present": CHARACTER_SOCKETS.issubset(bones),
        "actions_exact": actions == ACTION_NAMES,
        "skin_influences_within_limit": influence_count <= 4,
        "no_unweighted_vertices": unweighted == 0,
        "no_binary_hash_custom_fields": not unexpected_hash_fields,
        "firearm_separate": not any("carbine" in obj.name.lower() for obj in meshes),
    }
    if not all(checks.values()):
        raise RuntimeError(f"Vanguard source validation failed: {checks}")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in [*meshes, armature]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        export_extras=True,
        export_yup=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_merge_animation="NONE",
        export_optimize_animation_size=True,
        export_optimize_animation_keep_anim_armature=True,
        export_skins=True,
        export_all_influences=False,
        export_def_bones=False,
        export_cameras=False,
        export_lights=False,
    )
    if "FINISHED" not in result or not output.is_file():
        raise RuntimeError(f"Vanguard GLB export failed: {result}")

    return {
        "asset_id": CHARACTER_ID,
        "asset_revision": CHARACTER_RUN_ID,
        "provider_task_id": CHARACTER_TASK_ID,
        "source": str(source),
        "source_bytes": source.stat().st_size,
        "staging_glb": str(output),
        "staging_glb_bytes": output.stat().st_size,
        "bounds_blender_z_up": {
            "minimum": rounded(minimum),
            "maximum": rounded(maximum),
            "dimensions": rounded(dimensions),
        },
        "triangles": triangles(meshes),
        "mesh_objects": sorted(obj.name for obj in meshes),
        "materials": sorted(materials),
        "bones": len(bones),
        "sockets": sorted(CHARACTER_SOCKETS),
        "actions": sorted(actions),
        "one_frame_interface_actions": sorted(
            action.name
            for action in bpy.data.actions
            if tuple(round(value, 6) for value in action.frame_range) == (1.0, 1.0)
        ),
        "max_skin_influences": influence_count,
        "unweighted_vertices": unweighted,
        "checks": checks,
    }


def export_weapon(source: Path, output: Path) -> dict[str, object]:
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    root = bpy.data.objects.get(WEAPON_ID)
    meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and not any(
            collection.name == "glTF_not_exported"
            for collection in obj.users_collection
        )
    ]
    markers = {
        obj.name
        for obj in bpy.context.scene.objects
        if obj.type == "EMPTY" and obj.name in WEAPON_MARKERS
    }
    materials = material_names(meshes)
    minimum, maximum = world_bounds(meshes)
    dimensions = maximum - minimum
    unexpected_hash_fields = custom_hash_fields()

    checks = {
        "root_present": root is not None,
        "envelope_exact": all(
            abs(actual - expected) <= 0.002
            for actual, expected in zip(dimensions, Vector((0.13, 0.82, 0.27)))
        ),
        "triangles_within_hard_limit": triangles(meshes) <= 12_000,
        "materials_exact": materials == WEAPON_MATERIALS,
        "markers_exact": markers == WEAPON_MARKERS,
        "no_armature": not bpy.data.armatures,
        "no_actions": not bpy.data.actions,
        "no_binary_hash_custom_fields": not unexpected_hash_fields,
        "character_separate": not any(
            "vanguardbody" in obj.name.lower() for obj in meshes
        ),
    }
    if not all(checks.values()):
        raise RuntimeError(f"Carbine source validation failed: {checks}")

    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [
        obj
        for obj in bpy.context.scene.objects
        if obj == root or obj in meshes or obj.name in WEAPON_MARKERS
    ]
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
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
    if "FINISHED" not in result or not output.is_file():
        raise RuntimeError(f"Carbine GLB export failed: {result}")

    return {
        "asset_id": WEAPON_ID,
        "asset_revision": WEAPON_RUN_ID,
        "provider_task_id": WEAPON_TASK_ID,
        "source": str(source),
        "source_bytes": source.stat().st_size,
        "staging_glb": str(output),
        "staging_glb_bytes": output.stat().st_size,
        "bounds_blender_z_up": {
            "minimum": rounded(minimum),
            "maximum": rounded(maximum),
            "dimensions": rounded(dimensions),
        },
        "triangles": triangles(meshes),
        "mesh_objects": sorted(obj.name for obj in meshes),
        "materials": sorted(materials),
        "markers": sorted(markers),
        "checks": checks,
    }


def fresh_import(output: Path, asset_id: str) -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(output))
    if "FINISHED" not in result:
        raise RuntimeError(f"Fresh import failed for {output}: {result}")
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
    minimum, maximum = world_bounds(meshes)
    return {
        "asset_id": asset_id,
        "path": str(output),
        "bytes": output.stat().st_size,
        "mesh_objects": sorted(obj.name for obj in meshes),
        "triangles": triangles(meshes),
        "materials": sorted(material_names(meshes)),
        "armatures": sorted(obj.name for obj in armatures),
        "bones": sum(len(obj.data.bones) for obj in armatures),
        "actions": sorted(bpy.data.actions.keys()),
        "empties": sorted(
            obj.name for obj in bpy.context.scene.objects if obj.type == "EMPTY"
        ),
        "bounds_blender_z_up": {
            "minimum": rounded(minimum),
            "maximum": rounded(maximum),
            "dimensions": rounded(maximum - minimum),
        },
    }


character_source, weapon_source, character_glb, weapon_glb, report_path = (
    parse_paths()
)
character_source_report = export_character(character_source, character_glb)
weapon_source_report = export_weapon(weapon_source, weapon_glb)
character_import_report = fresh_import(character_glb, CHARACTER_ID)
weapon_import_report = fresh_import(weapon_glb, WEAPON_ID)

report = {
    "generated_utc": datetime.now(timezone.utc).isoformat(),
    "blender_version": bpy.app.version_string,
    "status": "fresh staging exports passed structural validation",
    "publication_status": (
        "provisional runtime publication; Godot gallery review pending"
        if "game/Assets/Published" in character_glb.as_posix()
        and "game/Assets/Published" in weapon_glb.as_posix()
        else "ignored staging only; Godot assembly review pending"
    ),
    "character_source": character_source_report,
    "weapon_source": weapon_source_report,
    "character_fresh_import": character_import_report,
    "weapon_fresh_import": weapon_import_report,
}
report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("SPACE_ADVENTURE_RESULT=" + json.dumps(report, sort_keys=True))
