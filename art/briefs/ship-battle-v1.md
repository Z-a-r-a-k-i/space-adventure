# Ship battle presentation

Owner authorization: implement the agreed ship battle with Codex handling art
and assets, 2026-09-24. Asset owner: Codex; gameplay/scene integration: Opus 5.5.
This is an initial playable presentation, not owner visual acceptance.

| Asset | Purpose |
| --- | --- |
| `ship.escape_cutter.combat.v1` | Roof-off combat derivative of the existing cutter |
| `ship.interceptor.v1` | Unmanned hostile, roof-off, three visible targetable system rooms |
| `fx.ship_fire.v1` / `fx.ship_breach.v1` | Readable flame and torn-deck hazard markers |

Preserve the [existing cutter](escape-cutter-v1.md), human crew proportions,
navy/warm-gray industrial materials, short paired nacelles, cyan identity and
purposeful amber warnings. Use the existing exterior concept and the retained
Fable room-readability study as visual references. Enemy: narrow angular spine,
red threat accents, and (v2, ADR 0034) a roof-off hull whose weapons, shields and
engines rooms match `ship-battle.json` so the player targets rooms, not dots.
v2 look for both ships: dark gunmetal decks cut into plates, emissive light
lines along the hull edges (cyan cutter, red interceptor) and brighter consoles,
matching the approved separated concept rather than a pale greybox.
Do not alter the accepted station exterior publication.

Five crew spaces fit inside the approximately four-metre pressure-hull beam:
forward weapons/cockpit, central passage, port shields, starboard life support,
aft engines. An aft starboard vent hatch connects the cross-passage to vacuum.
It is non-traversable, avoiding an extra room or a false path through machinery.
Floor is Godot Y=1.12; bow is -Z. Blender uses metres, +Y bow, +Z up.
The generated manifest owns all room, work, effect, door and muzzle anchors.
Floor hazard anchors (`hazard_fire_<room>` and `hazard_breach_<room>`) sit above
deck inserts and clear of machinery; the battle adapter uses these placements.

Low cutaway walls retain door openings. Console masses stay clear of work
positions; a 0.6m crew clearance disc must fit at every work anchor. No runtime
physics/collision/rules inside GLB. Door leaf visibility is adapter-owned.
Roof-off hull and machinery are original dimensional Blender geometry. Reuse
the existing cutter builder for nacelle identity. No external provider inputs,
textures, or licensing dependencies. Keep authored source and GLB in LFS.

Budget per ship: 60,000 triangles, 100 mesh nodes, 12 materials. Static objects
are grouped by function; keep door leaves and system groups separate for view
state. Use broad forms and bevels, no tiny greeble-dependent meaning. No letters
or gameplay values baked into geometry. Technical export does not approve
balance, crew handling, readability, or the physical-input experience.

Builder: `tools/blender/build_ship_battle.py`. Sound comes from the shared CC0
library built by `tools/audio/build_game_audio.py` ([game audio](../../game/audio/README.md)).
Hazards use `tools/blender/build_ship_hazards.py`: floor origin, +Y up in Godot,
at most 0.7m footprint, 0.9m height, 1,000 triangles each. Fire has translucent
orange tongues and gold cores; breach has raised torn steel around a dark
aperture. Presentation may animate these from simulation time; they own no rules.
Inspect fresh-imported exports in Blender and the actual split Godot view.
Review images/logs are ignored under `artifacts/reviews/ship-battle/v02/`.
