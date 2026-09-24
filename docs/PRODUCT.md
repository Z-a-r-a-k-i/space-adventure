# Product and POC scope

SpaceAdventure is a single-player 3D science-fiction party RPG about guiding a
small crew through exploration, conversation, and deliberate real-time combat
with tactical pause. Aarklash: Legacy is the reference for direct party control
and readable tactics. Decisions and positioning should matter more than fast
clicking. Authored, coherent low-poly presentation supports that clarity.

## First playable experience

Build one authored journey through a disabled frontier station:

Vanguard start → survivor conversation → service door → solo tutorial fight →
Protector recruitment → two-person encounter → safe Medic recruitment junction
→ service access fight → security checkpoint fight → dock concourse fight →
launch bay fight → evacuation airlock → cutter boarding and departure →
completion summary.

Vanguard is the fixed protagonist. Protector joins after the solo fight;
Operator joins as the Medic after the two-person encounter through a short
authored conversation. The optional terminal supplies an inspectable detail;
one authored dialogue choice must have an observable consequence. Conversation
is available to eligible sapient NPCs, without guaranteeing persuasion or a
peaceful route through every encounter. Setting, names, factions, and final
lore remain provisional. [Roadmap](ROADMAP.md) owns implementation and gate status.

## Content budget

- One connected station route, with short passages, distinct area landmarks,
  and victory-controlled ordinary service doors. Reserve the evacuation
  airlock assembly for the final destination. The [tactical layout](station-layout.md)
  uses distinct movement lanes, open service recesses, and staggered entries.
- Exactly three controllable crew, recruited sequentially, plus one
  noncontrollable survivor. Keep control extensible to four without adding
  another recruit now.
- Six fights: preserve the solo tutorial and two-person encounter, then add
  the four encounters below. Hostiles are mobile melee Enforcers, mobile
  rifle-equipped ranged Enforcers, and stationary integrated-gun sentries.
- Each crew member has one separate fixed weapon, a repeatable basic attack,
  and two active skills. Each human has one fixed outfit. No ammunition,
  reload, loot, or generalized inventory.
- Three authored NPC exchanges with selectable responses, one meaningful
  choice consequence, and one optional inspection.
- Reviewed production presentation for every visible environment, NPC,
  combatant, and the cutter exterior. Hidden spatial wrappers may be
  primitives. [POC-ASSET-ROSTER.md](POC-ASSET-ROSTER.md) owns the asset inventory.

| New encounter | Enemies | Tactical purpose |
| --- | --- | --- |
| Service access | 2 melee Enforcers, 1 ranged Enforcer | Introduce healing and protecting Medic |
| Security checkpoint | 1 melee Enforcer, 2 ranged Enforcers, 1 sentry | Combine Barrier, Interrupt, and target priority |
| Dock concourse | 2 melee Enforcers, 2 ranged Enforcers | Coordinate positioning and field placement |
| Launch bay | 2 melee Enforcers, 2 ranged Enforcers, 1 sentry | Combine all three kits before escape |

| Crew | Weapon | Active skills |
| --- | --- | --- |
| Vanguard | Carbine | Area Interrupt; targeted Burst damage |
| Protector | Shotgun | Stationary directional Barrier that blocks projectiles; nearby-enemy Taunt |
| Medic (Operator) | One-handed pistol | Heal one living ally or herself; ground-placed Healing Field |

Crew attack only explicitly assigned targets. Healing is player-commanded,
available during combat, and cannot resurrect fallen crew. Healing Field
periodically heals living crew inside its fixed radius and survives Medic
moving or falling; one field exists at a time. Ranged Enforcers approach rifle
range, stop, telegraph, and fire without automatic retreat. Content owns tuning;
[Architecture](ARCHITECTURE.md) owns exact rules.

Exploration can reveal dormant enemies before the crew enters combat.
[Shared crew vision](ARCHITECTURE.md#shared-crew-vision) governs enemy visibility
and targeting; moving the camera does not discover enemies. The authored
all-crew entry zones still control encounter activation and tactical pause.

## Acceptance

The player can select any crew member, drag a selection box, use additive
selection and compact automatic formation, and issue contextual move, attack,
or interact orders. Tab changes ability focus within the selected group.
Heal targeting works through world characters and portraits; field previews
show radius and affected crew. Camera pan, yaw, pitch/zoom, reset, and focus
remain usable around every area. Health, targets, pending orders, cooldowns,
valid ability targets, and rejection reasons are legible with crowded fights.

Each encounter starts in tactical pause once its recruited crew reach the
entry zone. Pause supports deliberate order entry; attacks communicate source,
facing, wind-up, release/contact, and recovery. Threats create useful interrupt,
positioning, healing, defensive, and coordinated targeting decisions. After
securing victory, restore health, fallen crew, and cooldowns and clear combat
orders/effects before travel. Defeat retries only the current encounter,
preserving recruitment, dialogue consequences, inspection, and prior victories.

The final victory opens access to the evacuation airlock. Boarding requires
all three crew in the boarding zone, including when approach movement
completes. Completion occurs once, followed by boarding, entrance closure,
takeoff, and the summary. This is an authored departure without ship interiors
or ship-combat mechanics.

The finished POC requires five consecutive blocker-free owner-operated
full-route playthroughs plus relevant automated and graphical checks. Controls,
dialogue, threats, skills, and outcomes must be understandable from the game.
Another contributor must be able to build, reproduce an accepted asset and a
bug, and author/verify a small encounter using the repository workflow.

## Deferred

No procedural generation, metaprogression, general quest framework, save
migration, crafting/economy, runtime LLM calls, ship simulation, generalized
boarding, or multiplayer belongs in the station POC. Bulk asset generation and
unbounded variants are also outside scope.

The approved [escape-cutter combat experiment](future/ship-combat-poc.md) is
the next gameplay milestone and carries Vanguard, Protector, and Medic forward.
[Generated dialogue](future/dialogue-ai.md) remains a separate optional
experiment. Model output may propose dialogue but never establish world facts
or mutate rules directly. Larger adventures, progression, relationships, and
procedural runs depend on the tactical loop proving enjoyable first.
