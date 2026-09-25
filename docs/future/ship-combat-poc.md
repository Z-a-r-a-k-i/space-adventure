# Escape-cutter ship-combat proof of concept

Status: Phase 6 gameplay milestone. The rule core, Godot battle, production
cutter/interceptor publications and station handoff are implemented; owner
handling, tuning, visual and audio acceptance are **open** (ADR 0033, 0034).
The 2026-09-25 FTL rework (ADR 0034) replaced the single volley with mounted
weapons, engines-as-evasion and room-damage rules, rebuilt the HUD and ship
presentation, and moved the whole game to sampled CC0 sound.

## Purpose and boundary

Test whether party control, the fixed tick and active pause stay enjoyable when
Vanguard, Protector and Medic escape the station aboard the cutter, with
decisions close to FTL: power, weapon timing and targeting, crew placement.
One fixed battle follows the station departure; it is not a campaign, map or
boarding system. The approved composition reference
([separated overhead v4](../../art/concepts/station-escape-ship-combat-v1/ship-combat-separated-clean-direction-v4.png))
sets two strict overhead views, player left and enemy right, bows up, a quiet
divider and no cross-divider shot or trajectory lines.

## Lifecycle

- The ordinary game captures the station party once at completion, plays the
  full 8 s departure, loads the battle on a thread and enters exactly once when
  both have finished. The battle starts paused at tick 0; loading never
  advances it. A load or entry failure discards any partial scene and stays
  retryable (R). Station review/smoke profiles keep station-only completion
  unless they opt in with `--review-continue=ship`.
- Entry gives fresh full health and the authored ship state while preserving
  crew identity; ground abilities and cooldowns do not carry over.
- Retry is battle-only: ships, crew, hazards, oxygen, doors, power, weapons,
  targets, ammunition, enemy repair reserve, shots and effects reset under a new
  attempt number with a new random stream.
- Player hull zero or all crew down is defeat, which wins over a simultaneous
  enemy destruction. Victory restores downed crew. Terminal outcomes freeze
  the simulation; a bounded presentation-only clock (4 s) lets the losing ship's
  destruction finish. There is no stalemate timeout.

## Rules

Rules live in `src/SpaceAdventure.Core/Ship/` (tick order is documented on
`ShipCombatSession`); every number lives in `game/content/ship-battle.json`
(schema 2).

- **Layout.** Weapons/cockpit, port shields, starboard life support, aft
  engines and a central passage (with aft cross-passage). Each system room
  connects only to the passage. The aft-starboard airlock vents the passage to
  vacuum and is never walkable. IDs match the art contract (`door_*`,
  `room_*`, `work_*`, `system_*`, `effect_*`, `muzzle`).
- **Travel.** Deterministic authored routes through doorways; retargeting
  finishes a doorway crossing first; crew pass without physics. A closed
  internal door opens for one fixed traversal window with normal gas exchange
  and fire spread. Manually opened doors stay open. Unsafe orders are legal
  and produce a warning, never an automatic evacuation.
- **Work.** Crew work their current room automatically: fire → breach →
  (Medic) the most injured settled ally in the room, including the Medic →
  repair → man. Explicit tasks (Extinguish, Seal, Repair, Man, Treat) persist in
  danger until complete, cancelled or invalid; finished emergency tasks return
  to Auto. Hold suppresses all automatic work. A second worker adds a reduced
  rate; a third adds nothing. Treatment never outheals sustained fire and never
  revives.
- **Power.** Four player systems on a reactor that cannot power all of them.
  Damage caps usable power while allocation stays visible. One healthy,
  hazard-free operator adds a nonstacking bonus (faster charge, shield
  recharge, oxygen, evasion). Power or pause toggling cannot create charge or
  refill shields.
- **Shields.** Layers, not percentages: one layer per usable shield bar. Each
  non-piercing shot that reaches the ship removes one layer instead of hitting.
  Layers regrow one at a time after a short post-hit delay while powered.
  Damage to the shield generator room removes layers from the maximum.
- **Weapons.** Each side mounts weapons that draw weapons-system power in
  mounting order: an armed weapon is powered if its cost still fits, so damage
  or a lower allocation unpowers the last ones first and repair restores them.
  Each weapon charges on its own, fires its volley at its own target and keeps
  that target. Missiles have finite ammunition, pierce shields and never miss.
  Hold keeps charged weapons from firing so the player can release one
  synchronized volley, FTL's shield-breaking move.
- **Evasion.** Powered engines give a percentage chance for each evadable shot
  to miss (a manned station boosts it). Rolls, and weapon fire/breach chances,
  come from one seeded stream per attempt, so a seed, attempt and command log
  replay exactly (ADR 0034).
- **Hits.** A hit removes hull equal to the weapon's damage, damages the target
  system by the same amount and injures every crew member in that room (15 per
  damage point). Incendiary/breach payloads and chance rolls start fires and
  breaches.
- **Atmosphere and hazards.** Per-room oxygen with bounded exchange computed
  from the previous tick: open doors equalize, breaches and the open airlock
  leak to vacuum, crew and fires consume, powered life support restores. Fires
  damage crew and the room's system, spread through open doors at fixed
  intervals and die below a minimum oxygen level. Low oxygen suffocates crew.
  Sealing a breach stops the leak and never refunds hull.
- **Enemy.** An unmanned interceptor (weapons, shields, engines) with a pulse
  laser and a hazard missile, each on an authored target/payload sequence that
  the HUD telegraphs with a countdown. It has no crew, atmosphere or boarding,
  and a finite visible repair reserve worked one system at a time in the order
  weapons → shields → engines.

The scripted pilots (suppress weapons; synchronize volleys into shields) win in
roughly 100–135 unpaused seconds and passive play loses; `ship-balance` reports
the spread across 40 seeds.

## Presentation

`scenes/ship_battle.tscn` (`ShipBattleHost`) is also runnable directly for
development. Each side is an independent overhead SubViewport over a starfield,
with bounded zoom/pan and a shared Frame Both. HUD panels sit in the corners of
the two full-height views, and each camera frames its ship and shield bubble
inside the HUD-safe area.

- **Status.** Hull as one segment per point; shields as hexagon pips with a
  recharge sliver (dim = unpowered, red = generator damage); evasion and ship
  oxygen. The only percentages left are evasion and oxygen.
- **Crew and power.** Crew cards (portrait, health, activity icon, room).
  Reactor and system power are pip columns: left click adds a bar, right click
  removes one. In-room system badges show usable/damaged bars, manning and
  repair progress; oxygen shows as an FTL-style pink wash; doors change colour
  and can be clicked; Open all / Close all and the airlock sit under the crew.
- **Weapons.** One card per weapon (hotkeys 1-2) with power pips, a charge bar,
  ammunition and target; click a card, then an enemy room. Right-click toggles a
  weapon's power; right-click while aiming clears its target. H holds a volley.
  The enemy column telegraphs each enemy weapon's charge, target, payload and
  countdown, mirrored as reticles on the targeted player rooms.
- **Combat feedback.** Projectiles leave the firing frame at the divider edge
  and re-enter the other frame (never a line across the divider). Shots stop at
  a shield bubble ripple, miss with a MISS marker, or strike the room with an
  explosion, sparks, a scorch decal and (player ship) a brief shake. Damaged
  systems spark; fires and breaches have particles and light. The losing ship
  breaks up in a chain of explosions.
- **Crew.** The existing full-size models, sampled from the battle tick with
  small procedural work poses, over a role-colour ring (strict overhead camera).
  Right-click a room to move selected crew (F1–F3, Tab or cards select).
- **Audio.** Sampled CC0 cues through the shared `GameAudio` library
  ([audio](../../game/audio/README.md)): weapons, impacts, shield hits, misses,
  hazards, crew, doors, power and interface cues, panned by side, plus ship
  ambience and hazard loops that pause with the battle. M mutes and -/+ step
  the session Master volume the station manual also controls. No music yet.

Only contracted publications are used; missing ones are reported, never
replaced by greybox. Ground abilities are hidden.

## Non-goals

Sector map, travel, free flight, ship physics, procedural encounters,
progression, loot, saving, drones, beams, boarding, enemy crew, sensors,
piloting as a separate system, permanent crew death, music and runtime model
calls.

## Exit gate

Rule, CLI, headless, handoff, graphical capture/input/resize and performance
profiles pass ([Testing](../testing.md)); the owner then plays the battle with
physical input and accepts handling, tuning, readability, art and audio. The
owner gate has not been run.
