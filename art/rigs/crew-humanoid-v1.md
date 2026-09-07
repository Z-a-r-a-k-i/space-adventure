# Humanoid rig contract

Mixamo supplies the baseline; Blender owns corrected weights, sockets, and
publication. [Art pipeline](../../docs/ART-PIPELINE.md) defines source,
retopology, rigging, donor transfer, and review steps. Each character keeps its
validated rest hierarchy; matching labels do not establish donor compatibility.

## Publication limits

- Maximum 64 published bones and four normalized influences per vertex.
- Preserve animated-armature transforms; normalize through retargeting/baking.
- Godot: `+Y` up, `-Z` forward; Blender: Z-up.
- No facial/cloth/ragdoll/root-motion gameplay rig in the POC.
- Check each full locomotion cycle in evaluated world space after exact GLB
  reimport: horizontal/vertical hip range ≤0.15 m, loop endpoint delta ≤0.01 m,
  and each foot lift ≥0.04 m. Standing actions also satisfy the asset's
  hip-height floor. Compare sustained Godot movement with the untouched FBX.

## Attachments and actions

Hand and holster attachments, weapon grips, contact frames, and transfer
landmarks follow [Attack presentation](../../docs/ATTACK-PRESENTATION.md).
Review the complete character/weapon assembly after deformation repair.

Canonical action vocabulary (publish only the role's required subset):

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
anim.humanoid.down
anim.humanoid.melee_idle
anim.humanoid.melee_windup
anim.humanoid.melee_strike
anim.humanoid.melee_recovery
```

Published aliases and actual selected clips belong in each asset's production
record and Godot profile. Combat is in-place and follows authoritative timing;
damage uses effects/numbers, never a hit-reaction animation.
