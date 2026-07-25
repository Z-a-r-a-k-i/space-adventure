# Provisional asset gallery

`game/scenes/asset_gallery.tscn` is a presentation-only Phase 3 review scene.
It currently contains the normalized Vanguard and the separate Vanguard
carbine in two stable slots.

Each slot has sibling `ProductionPresentation` and `GreyboxFallback` nodes.
Reverse their `visible` values to restore the brief-sized greybox without
changing a stable asset ID or gameplay wrapper. The carbine is a separate
PackedScene instance; the gallery-only rotation is not an attachment offset.

Both assets passed Blender source/export validation and exact Godot import.
Vanguard now contains provisional donor-retarget proofs for the 12.3-second
`anim.humanoid.idle_holstered` and 2.4-second
`anim.humanoid.locomotion_holstered` contracts, plus a 17.6-second unarmed
`anim.humanoid.dialogue_idle` proof and a 6.0-second
`anim.humanoid.dialogue_listen` proof. Blender's dotted action names are
imported by Godot with underscores; the gallery validates and documents that
adapter spelling without changing the presentation contracts.

The complete two-hand assembly remains provisional: sockets, support-hand
distance, muzzle clearance, and rear-right carry clearance pass, but the
generated glove topology remains visibly open around both vertical grips in
close review. Twelve other actions remain one-frame interface landmarks and
are not presented as finished animation.

The scene includes fixed cameras at 7.5 m, 14.5 m, and 20 m from
`ReviewTarget`. All three use the visual bible's 48-degree tactical gameplay
field of view, so gallery readability matches the live tactical-camera lens.

## Efficient model inspection

Open the gallery graphically and inspect the asset directly at all three
cameras. Play the complete animation in real time, pause or scrub only around
a named defect, and use the live scene tree or runtime state for structural
questions. Do not generate or commit a complete screenshot set, contact sheet,
or animation-frame sample.

The gallery automation may still seek an exact animation position and report
the requested/actual timestamp for a diagnostic check. If a frozen capture is
temporarily necessary, write only the minimum useful image beneath ignored
`artifacts/`, inspect it once, and keep the decision in text. Do not promote
the image or an image hash into the asset run.

One review owner records the live-tool decision. Later agents reuse that
decision unless the asset changes, a named question remains unanswered, or an
independent review is explicitly requested.
