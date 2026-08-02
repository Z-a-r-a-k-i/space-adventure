# Shared humanoid rig v1

Status: Mixamo-based profile; Vanguard validation pending.

Vanguard, Operator, Protector, the station survivor, and the humanoid Security
Enforcer use the same rig contract. Mixamo provides the Auto-Rigger baseline
and stock motion library; Blender owns corrected weights, sockets, clip cleanup,
and exported assets. Character-specific briefs select only the clips and
sockets each role needs.

## Source and rig rules

- Generate humanoids unrigged in T-pose by default.
- Complete Tripo Smart Low-Poly v2 Quad 10k retopology before rigging.
- A human verifies all Mixamo markers before Auto-Rigger submission.
- Download the accepted neutral rig once with skin.
- Download later Mixamo clips without skin for that skeleton.
- Maximum 64 published bones and four normalized influences per vertex.
- No facial, cloth, physics, ragdoll, or root-motion gameplay rig.
- Godot publication is `+Y` up and `-Z` forward; Blender remains Z-up.

Character proportions may vary, but bone naming and hierarchy must remain
compatible with the accepted Mixamo baseline. Every character still receives
its own deformation and complete-assembly review.

## Attachments

- `socket.weapon.hand_primary`, parented to the right hand.
- `socket.weapon.holster_primary`, parented to the reviewed carry location.

Godot performs deterministic weapon transfer at named animation landmarks.
Animation callbacks never apply gameplay effects.

Body attackers additionally publish `socket.attack.contact.primary` at the
reviewed striking surface. The contact frame points local `-Z` outward with
local `+Y` up. It presents observed contact but never applies damage.

## Animation contracts

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
anim.humanoid.melee_idle
anim.humanoid.melee_windup
anim.humanoid.melee_strike
anim.humanoid.melee_recovery
```

Draw and holster use `event.weapon.transfer_to_hand` and
`event.weapon.transfer_to_holster`. Combat clips are in-place. Final timings
and ability-specific animation remain blocked until gameplay defines them.

## Acceptance

Before reuse, Vanguard must pass stress poses, corrected chin/neck and joint
weights, idle and locomotion, draw/holster transfer, weapon-ready and recoil
poses, fresh GLB re-import, and direct Godot playback with the separate carbine.
