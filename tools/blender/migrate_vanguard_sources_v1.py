"""Migrate Vanguard source metadata to the Phase 3 no-binary-hash contract.

Run with Blender 5.2 LTS:

    blender --background --factory-startup \
      --python tools/blender/migrate_vanguard_sources_v1.py -- \
      <vanguard.blend> <carbine.blend>

The inputs are the tracked editable sources. The script updates them in place.
The predecessor copies remain intact in the dedicated machine's older
worktree, and Blender backup files are disabled for this bounded migration.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


CHARACTER_ID = "character.crew.vanguard.v1"
CHARACTER_RUN_ID = (
    "prod-tripo-v31bq-20260723-01"
)
CHARACTER_TASK_ID = "c889d05a-90fe-4186-85eb-12d4eceafb35"
CHARACTER_RAW_BYTES = 58_610_072

WEAPON_ID = "weapon.crew.vanguard_carbine.v1"
WEAPON_RUN_ID = (
    "prod-tripo-v31bq-20260723-01"
)
WEAPON_TASK_ID = "01bb9aea-6b10-419d-bbeb-9648c9867a97"
WEAPON_RAW_BYTES = 58_235_208


def parse_paths() -> tuple[Path, Path]:
    try:
        separator = sys.argv.index("--")
    except ValueError as exc:
        raise RuntimeError("Expected -- <vanguard.blend> <carbine.blend>") from exc
    values = [Path(value).resolve() for value in sys.argv[separator + 1 :]]
    if len(values) != 2:
        raise RuntimeError("Expected Vanguard and carbine source Blend paths")
    for path in values:
        if not path.is_file():
            raise FileNotFoundError(path)
    return values[0], values[1]


def delete_property(owner, name: str) -> None:
    if name in owner:
        del owner[name]


def save_without_backup(path: Path) -> None:
    bpy.context.preferences.filepaths.save_version = 0
    result = bpy.ops.wm.save_as_mainfile(
        filepath=str(path),
        check_existing=False,
        compress=True,
        relative_remap=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Could not save {path}: {result}")


def migrate_character(path: Path) -> dict[str, object]:
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    scene = bpy.context.scene
    if scene.get("asset_id") != CHARACTER_ID:
        raise RuntimeError(f"Unexpected character asset ID in {path}")

    delete_property(scene, "source_sha256")
    scene["source_run_id"] = CHARACTER_RUN_ID
    scene["source_task_id"] = CHARACTER_TASK_ID
    scene["source_bytes"] = CHARACTER_RAW_BYTES
    scene["collision_owner"] = "godot_gameplay_wrapper"

    body = bpy.data.objects.get("VanguardBody")
    base = bpy.data.objects.get("VanguardDeformingBase")
    armature = bpy.data.objects.get("VanguardRig")
    if body is None or base is None or armature is None:
        raise RuntimeError("Vanguard source is missing body, base, or rig")

    delete_property(body, "source_sha256")
    body["source_run_id"] = CHARACTER_RUN_ID
    body["source_task_id"] = CHARACTER_TASK_ID
    body["source_bytes"] = CHARACTER_RAW_BYTES
    body["collision_owner"] = "godot_gameplay_wrapper"
    base["collision_owner"] = "godot_gameplay_wrapper"
    armature["source_run_id"] = CHARACTER_RUN_ID
    armature["source_task_id"] = CHARACTER_TASK_ID

    material_renames = {
        "tripo_material_0d21d938-973d-4847-b97b-0e10cf706e2c":
            "mat.vanguard.surface.pbr",
        "mat.crew.undersuit.dark_navy": "mat.vanguard.undersuit.navy",
    }
    for old_name, new_name in material_renames.items():
        material = bpy.data.materials.get(old_name)
        if material is not None:
            material.name = new_name

    surface = bpy.data.materials.get("mat.vanguard.surface.pbr")
    undersuit = bpy.data.materials.get("mat.vanguard.undersuit.navy")
    if surface is None or undersuit is None:
        raise RuntimeError("Vanguard material migration did not resolve both slots")
    surface["palette_regions"] = (
        "warm_gray_armor,dark_mechanism,cyan_accent,skin,hair"
    )
    surface["texture_set"] = "provider_pbr_cleaned_2048"
    undersuit["palette_role"] = "continuous_deforming_base"

    save_without_backup(path)
    return {
        "asset_id": CHARACTER_ID,
        "path": str(path),
        "bytes_after_save": path.stat().st_size,
        "materials": sorted(material.name for material in bpy.data.materials),
        "objects": sorted(obj.name for obj in bpy.data.objects),
        "actions": sorted(action.name for action in bpy.data.actions),
        "binary_hash_fields_removed": True,
    }


def migrate_weapon(path: Path) -> dict[str, object]:
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    root = bpy.data.objects.get(WEAPON_ID)
    if root is None or root.get("asset_id") != WEAPON_ID:
        raise RuntimeError(f"Unexpected weapon asset ID in {path}")

    delete_property(root, "raw_sha256")
    delete_property(root, "staging_sha256")
    root["source_run_id"] = WEAPON_RUN_ID
    root["source_task_id"] = WEAPON_TASK_ID
    root["source_bytes"] = WEAPON_RAW_BYTES
    root["collision_owner"] = "godot_gameplay_wrapper"

    body = bpy.data.objects.get("weapon.vanguard_carbine.body")
    if body is None:
        raise RuntimeError("Carbine source is missing its rigid body")
    body["source_run_id"] = WEAPON_RUN_ID
    body["source_task_id"] = WEAPON_TASK_ID
    body["collision_owner"] = "godot_gameplay_wrapper"

    material_renames = {
        "mat.weapon.vanguard_carbine.mechanism":
            "mat.vanguard_carbine.mechanism.dark",
        "mat.weapon.vanguard_carbine.armor":
            "mat.vanguard_carbine.armor.warm_gray",
        "mat.weapon.vanguard_carbine.accent":
            "mat.vanguard_carbine.accent.cyan",
    }
    for old_name, new_name in material_renames.items():
        material = bpy.data.materials.get(old_name)
        if material is not None:
            material.name = new_name

    expected_materials = set(material_renames.values())
    actual_materials = {material.name for material in bpy.data.materials}
    if not expected_materials.issubset(actual_materials):
        raise RuntimeError(
            f"Carbine materials do not match the brief: {sorted(actual_materials)}"
        )

    save_without_backup(path)
    return {
        "asset_id": WEAPON_ID,
        "path": str(path),
        "bytes_after_save": path.stat().st_size,
        "materials": sorted(actual_materials),
        "objects": sorted(obj.name for obj in bpy.data.objects),
        "binary_hash_fields_removed": True,
    }


character_path, weapon_path = parse_paths()
result = {
    "blender_version": bpy.app.version_string,
    "character": migrate_character(character_path),
    "weapon": migrate_weapon(weapon_path),
}
print("SPACE_ADVENTURE_RESULT=" + json.dumps(result, sort_keys=True))
