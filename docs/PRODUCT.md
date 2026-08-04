# Product vision

## Vision

SpaceAdventure is a single-player 3D science-fiction party RPG about guiding a small crew through dangerous places using deliberate real-time tactics, active pause, exploration, and conversation.

The initial game is character-scale. The same adventure may later expand into ship command, vehicle combat, and boarding, but those layers must complement rather than replace the party RPG.

## Player fantasy

- Lead capable but vulnerable individuals rather than an anonymous army.
- Read a dangerous situation, pause, assign precise orders, then watch the plan unfold.
- Build a crew whose abilities and perspectives create both tactical and narrative options.
- Solve problems through movement, combat, investigation, equipment, and conversation.
- Eventually command a ship and vehicles while remaining responsible for the people inside them.

## Product pillars

### Tactical clarity over reflexes

Good decisions, positioning, target priority, interrupts, and coordinated abilities matter more than fast clicking. Active pause is a primary control mode, not an accessibility afterthought. Combat direction takes its closest inspiration from *Aarklash: Legacy*: direct individual party control, readable threats, complementary abilities, and frequent tactical replanning.

### A party of distinct people

Companions have complementary tactical roles, personal perspectives, and relationships with the world. The player begins alone and earns a party through the adventure. Party members remain individually selectable and controllable rather than collapsing into a single squad unit.

### Conversation is gameplay

When a sapient NPC is present, aware, and fictionally able to communicate, conversation should be exposed as an interaction. Dialogue can reveal information, recruit a companion, change access, alter a later reaction, or fail. Offering dialogue does not guarantee a peaceful solution, and non-sapient threats do not require a dialogue path.

### Authored coherence before generated quantity

The early game uses small authored scenarios and deliberately simple low-poly art. Future AI generation must operate inside world rules, content schemas, review gates, and authoritative state validation. More content is useful only when it remains understandable and consistent.

### Systems that agents and humans can both operate

UI, automated playtests, live agent tools, and future replays share gameplay commands. Structured state and events answer logic questions; screenshots and live play answer visual and interaction questions.

### Replaceable production inputs

Models, textures, animations, dialogue providers, and procedural generators are inputs to controlled publication boundaries. Gameplay code and saved state must not depend on one generator, one model vendor, or one temporary art workflow.

## Core play loop

1. Explore and read the situation.
2. Talk, investigate, or position the party.
3. Encounter an obstacle or opportunity.
4. Pause and coordinate actions when tactics matter.
5. Resolve consequences through dialogue, combat, movement, or resource use.
6. Continue toward a destination with changed state.

The POC tests one short authored pass through this loop. A later roguelite structure may arrange authored and procedural situations into replayable runs, but it is not required to discover whether the minute-to-minute play is enjoyable.

## Permanent boundaries

- Single-player only. Do not build multiplayer, rollback, replication, accounts, or authoritative-server infrastructure.
- The game owns authoritative state. An LLM may propose expression or bounded intent but never directly mutates the world.
- Low-poly is an intentional readability and production choice, not permission for inconsistent art.
- Human and automated control use the same validated gameplay commands.
- Content providers are replaceable; game rules and saved state do not encode provider-specific output.

## Scope horizon

### Current POC

- One authored 8–12 minute journey from a start to a destination.
- Player begins alone as Vanguard, survives a short solo tutorial fight, and
  then recruits Protector as the fixed companion. Operator is deferred.
- Point-and-click 3D exploration with an elevated tactical camera.
- One short solo tutorial fight followed by a two-character active-pause
  encounter.
- Authored NPC interaction with one observable choice consequence.
- One fixed weapon per party character and one usable healing item; no
  generalized inventory system.
- Each hostile combatant archetype has an authored basic attack presented
  through a handheld or integrated weapon, or a readable body attack.
- Structured automation that can complete and inspect the real scenario.
- Reviewed low-poly presentation with no visible greybox environment, NPC, or
  combatant in the active route.

### Post-POC candidates

- The first bounded post-POC experiment is the authored escape-cutter battle
  defined in `SHIP-COMBAT-POC.md`: two party members, two separated overhead
  ship views, three ship systems, and one fixed encounter using the existing
  fixed tick and active pause.
- Deeper character progression, equipment, inventory, party relationships, and a larger authored adventure.
- A procedural-run experiment with metaprogression only after the tactical loop is fun.
- Controlled generative dialogue using recorded, validated provider results.
- Deeper ship and vehicle command, including boarding as a possible later
  combat resolution, only after the bounded escape-cutter experiment.
- Larger AI-assisted environment and content pipelines as generation quality improves.

### Still open

The campaign-versus-roguelite balance, seamless vehicle movement, breadth of generated locations, and final visual language remain long-term subjects for discussion or playtesting. No unresolved decision currently blocks the POC or bootstrap; accepted defaults are recorded in `OPEN-QUESTIONS.md` and `DECISIONS.md`.

## POC success signals

- Coordinating two characters through active pause is enjoyable.
- Selection, targets, pending orders, threats, cooldowns, and outcomes are understandable without external explanation.
- Movement and camera operation do not fight the player.
- Talking feels like a valid action even when it cannot bypass every conflict.
- The fixed weapon, abilities, and healing item create at least one meaningful tactical choice.
- A human or agent can reproduce a problem and rerun the relevant scenario.
- Another contributor can add a small authored encounter without changing engine-level systems.
