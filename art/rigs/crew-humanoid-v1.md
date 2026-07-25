# Shared rig profile — crew humanoid v1

Status: accepted for offline source production; Vanguard validation required
before reuse

Profile ID: `rig.crew.humanoid.v1`

Revision: 2026-07-23

Accepted by the project owner on 2026-07-24 for Vanguard-first offline source,
retarget, export, and isolated Godot validation. Gameplay attack definitions,
timings, abilities, VFX, and audio remain phase-blocked.

## Purpose

Vanguard, Operator, Protector, and the later station survivor use this one
Blender-owned hierarchy and shared presentation animation library. Provider
rigs and clips are donor inputs only. Vanguard proves the hierarchy, weights,
attachments, retargeting, export, Godot import, and pause-safe playback before
Operator or Protector receives final weapon-specific animation.

The profile defines art and presentation interfaces. It does not define a
gameplay actor, attack, timing, damage, range, ability, or equipment slot.

Vanguard weapon-handling concept reference:
`art/reference-sheets/frontier-station-v1/poc-animation/vanguard-weapon-handling-key-poses-v1.png`
(`F618192F0F52AEC1F1AEBA4AA2658DEB7EBD1F18D97B695D870E89DB459A2C22`).
It guides pose readability only and is not animation timing or gameplay
authority.

## Authoring and publication coordinates

- Blender source: meters, Z-up, character facing local `-Y`.
- Neutral pose: symmetrical A-pose, palms approximately inward, straight
  fingers, feet parallel and shoulder-width.
- Reference bind height: 1.82 m from ground to crown.
- Origin: ground-plane center between the feet.
- Root: unit scale, no shear, zero object rotation.
- glTF publication: `+Y` up and `-Z` forward through Blender's glTF conversion;
  do not rotate the source armature by hand.
- Maximum published bones: 64, including attachment bones.
- Maximum skin influences: four per vertex; normalized weights.
- Root motion: none in published POC clips.

Character-specific height and bulk are fitted without changing bone names,
parenting, axes, or semantic rest pose. Uniform scale is applied before export.
Animation reuse is validated after each fit rather than assumed.

## Deform hierarchy

The published deform hierarchy uses these exact names:

```text
root
└── pelvis
    ├── spine_01
    │   └── spine_02
    │       └── spine_03
    │           ├── neck_01
    │           │   └── head
    │           ├── clavicle_l
    │           │   └── upperarm_l
    │           │       ├── upperarm_twist_l
    │           │       └── lowerarm_l
    │           │           ├── lowerarm_twist_l
    │           │           └── hand_l
    │           │               ├── thumb_01_l → thumb_02_l → thumb_03_l
    │           │               ├── index_01_l → index_02_l → index_03_l
    │           │               ├── middle_01_l → middle_02_l → middle_03_l
    │           │               ├── ring_01_l → ring_02_l → ring_03_l
    │           │               └── pinky_01_l → pinky_02_l → pinky_03_l
    │           └── clavicle_r
    │               └── upperarm_r
    │                   ├── upperarm_twist_r
    │                   └── lowerarm_r
    │                       ├── lowerarm_twist_r
    │                       └── hand_r
    │                           ├── thumb_01_r → thumb_02_r → thumb_03_r
    │                           ├── index_01_r → index_02_r → index_03_r
    │                           ├── middle_01_r → middle_02_r → middle_03_r
    │                           ├── ring_01_r → ring_02_r → ring_03_r
    │                           └── pinky_01_r → pinky_02_r → pinky_03_r
    ├── thigh_l
    │   └── calf_l
    │       └── foot_l
    │           └── toe_l
    └── thigh_r
        └── calf_r
            └── foot_r
                └── toe_r
```

Control bones may exist in the editable `.blend` but are excluded from the
published deform set and GLB unless a reviewed export test demonstrates a need.
No facial rig, lip sync, cloth rig, physics rig, or ragdoll belongs to this
profile.

## Attachment bones

These non-deforming bones are published:

- `socket.weapon.hand_primary`, parented to `hand_r`;
- `socket.weapon.holster_primary`, parented to the character-specific carry
  bone: `pelvis` for Vanguard and Operator, `spine_03` for Protector.

Vanguard's exact holster transform is provisional. Compare the character
sheet's thigh/hip hardware with the key-pose sheet's rear-right/back carry rail
in the normalized complete assembly and record the chosen transform without
inventing a gameplay equipment slot.

The attachment frames use local `+Y` up and local `-Z` as the weapon forward
direction after glTF publication. They encode the complete weapon-root
transform. The weapon root already coincides with its primary-grip frame, so
runtime attachment adds no second grip offset.

## Shared presentation action names

These names are presentation contracts, not gameplay event or command names:

```text
anim.humanoid.idle_holstered
anim.humanoid.locomotion_holstered
anim.humanoid.draw
anim.humanoid.idle_armed
anim.humanoid.locomotion_armed
anim.humanoid.raise_aim
anim.humanoid.fire_recoil
anim.humanoid.recovery
anim.humanoid.holster
anim.humanoid.dialogue_idle
anim.humanoid.dialogue_speak
anim.humanoid.dialogue_listen
anim.humanoid.interact_terminal
anim.humanoid.use_healing
anim.humanoid.hit_reaction
anim.humanoid.down
```

Weapon-specific corrections may use a suffix:
`.vanguard_carbine`, `.operator_pistol`, or `.protector_shotgun`. Shared
locomotion and dialogue clips remain unsuffixed when the exact same action is
reused.

Draw and holster actions contain one Blender timeline marker respectively:

- `event.weapon.transfer_to_hand`;
- `event.weapon.transfer_to_holster`.

Those are deterministic presentation landmarks. Godot performs attachment
transfer from observed presentation state and must reproduce the correct
attachment after pause, resume, reset, seek, or resynchronization. Animation
callbacks never mutate authoritative gameplay.

Final clip durations and mappings to wind-up, release, recovery, and attack
events remain pending the gameplay combat contract. Ability-specific actions
are prohibited until the matching ability definitions exist.

## Vanguard-first acceptance

Before this profile is reused:

1. fit Vanguard's continuous deforming base and fixed armor assembly;
2. pass shoulder, elbow, wrist, hip, knee, ankle, neck, and armor-clearance
   stress poses;
3. fit the separate carbine at the primary and support hands and holster;
4. retarget and clean at least idle and locomotion donor clips;
5. author or correct draw, transfer, raise, recoil, recovery, and holster
   landmarks without root translation;
6. export and re-import the exact GLB to confirm axes, hierarchy, weights,
   sockets, action names, and unit scale; and
7. validate the complete assembly in Godot at 14.5 m and 20 m.

Operator and Protector reuse is provisional until each character passes the
same deformation and assembly checks.

## Current Vanguard checkpoint

On 2026-07-24 the first Tripo humanoid donor idle, locomotion, dialogue-idle,
and dialogue-listen clips passed the Blender-owned retarget, cleaned in-place
export, fresh GLB re-import, and Godot gallery playback path:

- donor rig model: `v1.0 - Good for Humanoid`;
- donor task/model ID: `c889d05a-90fe-4186-85eb-12d4eceafb35`;
- 26 semantic donor joints mapped to this exact 59-bone published hierarchy;
- `anim.humanoid.idle_holstered` is 12.3 seconds and has no root translation;
- `anim.humanoid.locomotion_holstered` is 2.4 seconds and has no root
  translation;
- `anim.humanoid.dialogue_idle` is 17.6 seconds and
  `anim.humanoid.dialogue_listen` is 6.0 seconds, both without root
  translation;
- the other 12 action names remain present; and
- Godot imports all four actions with underscores, which the presentation
  adapter maps back to their dotted contract names.

This proves the Vanguard-first shared animation-retarget workflow and permits
provisional reuse work on the later humanoids. It is not full profile
acceptance: the visible Vanguard glove/grip revision, deformation stress
review, and remaining weapon-handling presentation coverage are still
required before Operator or Protector receives final shared-rig binding.
