# Asset gallery

`game/scenes/asset_gallery.tscn` is an isolated review scene, not gameplay.

The gallery currently shows only the separate Vanguard carbine. The approved
Vanguard body, rig, idle, and walk are reviewed in the live station route and
are not duplicated in the isolated gallery.

`game/scenes/humanoid_gallery.tscn` reviews the production Survivor and
Protector. `game/scenes/hostile_gallery.tscn` reviews the Security Enforcer idle
and walk plus neutral, aimed, and recoil-test Gun Sentry poses. Run the latter
with `scripts/dev.ps1 headless -Name hostile-gallery`; it validates the hostile
skeleton, actions, grounding, three distinct materials, sockets, rigid
hierarchy, published pivot-limit metadata, recoil travel, reset behavior, and
tactical-pause freeze. Blender validates the semantic material roles and rigid
pivot contracts at publication.

Review live in Godot using the fixed 7.5 m, 14.5 m, and 20 m cameras. Use the
sibling greybox fallback to compare scale when needed. In the hostile gallery,
press `1`, `2`, or `3` to switch between those review distances. Do not add automated
frame dumps or committed gallery screenshots; one ignored diagnostic capture
is enough for a named issue.
