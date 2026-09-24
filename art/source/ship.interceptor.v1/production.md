# Unmanned interceptor

Original dimensional Blender geometry/materials under the
[ship brief](../../briefs/ship-battle-v1.md). No external models or textures.
v2 (ADR 0034): a low roof-off hull whose weapons (cannon breech, missile tubes),
shields (emitter column, capacitors) and engines (reactor, fins) rooms match
the content's enemy room rectangles, with red light lines and outboard engine
pods. No implied crew or interior/atmosphere simulation.

Build, stage, fresh-import and validate alongside the cutter with
`tools/blender/build_ship_battle.py -- --replace --render` using Blender 5.2.
`manifest.json` owns geometry metrics, bounds, room rectangles and room/effect/muzzle anchors.

Blender overhead render inspected; exact publication loads in the Godot asset
review and its complete silhouette fits the inspected 1280x720 capture. Owner
visual acceptance remains open. Integrated targeting, repair feedback and full
framing were inspected at 720p/1080p. Review media stays ignored under
`artifacts/reviews/ship-battle/v02/` and `artifacts/ship-review/`.
