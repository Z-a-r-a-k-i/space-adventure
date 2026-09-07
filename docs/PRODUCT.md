# Product and POC scope

SpaceAdventure is a single-player 3D science-fiction party RPG about guiding a
small crew through exploration, conversation, and deliberate real-time combat
with tactical pause. Aarklash: Legacy is the reference for direct party control
and readable tactics. Decisions and positioning should matter more than fast
clicking. Authored, coherent low-poly presentation supports that clarity.

## First playable experience

Build one authored 8–12 minute journey through a disabled frontier station:

Vanguard start → survivor conversation → ordinary service door → solo tutorial
fight → Protector recruitment → two-character encounter → evacuation airlock
opening and completion summary.

Vanguard is the only protagonist; Protector is the fixed recruit. Operator and
its pistol are deferred. The optional terminal supplies an inspectable detail;
one authored dialogue choice must have an observable consequence. Conversation
is available to eligible sapient NPCs, without guaranteeing persuasion or a
peaceful route through every encounter. Setting, names, factions, and final
lore remain provisional.

The [roadmap](ROADMAP.md) distinguishes the implemented slice from this target.

## Content budget

- One authored level: start, solo arena, recruitment room, main encounter,
  final approach, and evacuation airlock. Reserve the airlock assembly for
  the destination; use ordinary service doors elsewhere.
- Exactly two controllable characters, deployed solo until recruitment, plus
  one noncontrollable survivor. Keep control architecture extensible to four
  without implementing a larger party now.
- One solo tutorial and one party encounter; at most two hostile behaviors:
  mobile humanoid Security Enforcer melee and stationary integrated-gun sentry.
- Each party character has one fixed weapon, repeatable basic attack, and
  active ability. Vanguard uses a carbine and position-targeted Suppressive
  Fire; Protector's shotgun/Guard Ally kit is finalized with party combat.
- One healing-item type. Weapons are separate presentation assets; each human
  has one fixed outfit. No ammunition, reload, loot, or generalized inventory.
- Two authored NPC exchanges with selectable responses, one meaningful choice
  consequence, and one optional inspection.
- Reviewed production models for every visible environment, NPC, and combatant
  on the active route. Hidden spatial wrappers may be primitives. The bounded
  approved asset inventory is in [POC-ASSET-ROSTER.md](POC-ASSET-ROSTER.md).

## Acceptance

The player can select either crew member, use additive/group selection and
compact automatic formation, and issue contextual move, attack, or interact
orders. Camera pan, free yaw, constrained pitch/zoom, reset, and focus are usable
without fighting the player. Health, targets, destinations, pending orders,
cooldowns, charges, valid ability targets, and rejection reasons are legible.

Pause allows deliberate order entry. Attacks communicate source, facing,
wind-up, release/contact, and recovery. A threat creates a useful interrupt,
repositioning, defensive, or coordinated targeting decision. Defeat offers
immediate encounter retry; victory and final completion are unambiguous.
The [architecture](ARCHITECTURE.md) owns exact command and pause semantics.

The finished POC passes five consecutive blocker-free owner-operated
playthroughs plus the relevant automated checks. These runs exercise Vanguard
throughout and Protector after recruitment, using production presentation.
Controls, dialogue, threats, item use, and outcomes must be understandable from
the game itself. Another contributor must be able to build, reproduce a bug,
and add a small authored encounter using the repository workflow.

## Deferred

No procedural generation, metaprogression, general quest framework, save
migration, crafting/economy, runtime LLM calls, vehicles, ship simulation,
boarding, or multiplayer belongs in the station POC. Bulk asset generation and
unbounded variants are also outside scope.

The approved [escape-cutter experiment](future/ship-combat-poc.md) follows POC
hardening; [generated dialogue](future/dialogue-ai.md) is a separate optional
experiment. Model output may propose dialogue but never establish world facts
or mutate rules directly. Larger adventures, progression, relationships, and
procedural runs depend on the tactical loop proving enjoyable first.

No unresolved product decision currently blocks the active slice. Record a
new blocking question beside its milestone; do not maintain another list of
already-decided features.
