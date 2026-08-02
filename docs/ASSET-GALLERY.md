# Asset gallery

`game/scenes/asset_gallery.tscn` is an isolated review scene, not gameplay.

The gallery currently shows only the separate Vanguard carbine. The abandoned
rigged Vanguard and its published GLB were removed. The character returns to
the gallery only after the selected Quad 10k source passes human Mixamo marker
approval, Blender weight repair, and exact GLB validation.

Review live in Godot using the fixed 7.5 m, 14.5 m, and 20 m cameras. Use the
sibling greybox fallback to compare scale when needed. Do not add automated
frame dumps or committed gallery screenshots; one ignored diagnostic capture
is enough for a named issue.
