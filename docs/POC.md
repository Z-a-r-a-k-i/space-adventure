# First playable proof of concept

## Goal

Build an 8–12 minute authored greybox demo that proves exploration, party control, active-pause combat, simple inventory use, and conversation. It is a small adventure from a start to a destination, not a roguelite run and not a systems showcase.

## Working scenario: a disabled frontier station

The setting is a provisional production constraint, not final lore.

The player begins alone in a maintenance berth on a disabled frontier transfer station. A lockdown blocks the direct route to an evacuation airlock. The player learns what happened from a survivor, recruits one stranded specialist, crosses a short service section, uses information or access gained through conversation, defeats a group of malfunctioning security machines, and reaches the airlock.

This setting keeps the first level compact, supports primitive modular art, introduces the wider spacefaring fantasy, provides plausible conversations, and gives us a non-sapient mandatory combat encounter without deciding whether every future hostile person can be negotiated with.

Names, factions, final lore, and visual tone remain provisional until the control loop works.

## Player journey

1. Choose one of two fixed protagonist kits.
2. Begin alone and learn movement, camera, interaction, and inspection.
3. Speak with a survivor who establishes the destination and immediate obstacle.
4. Find and recruit one fixed companion through dialogue.
5. Cross a short traversal area with one optional terminal, container, or environmental observation.
6. Use one healing item or preserve it for the encounter.
7. Resolve one tactical encounter using both characters.
8. Reach the evacuation airlock and receive an explicit completion summary.

The critical path always demonstrates recruitment and combat. A dialogue choice changes an observable detail such as route access, available information, starting position, support machinery, or the number and placement of enemies.

## Control acceptance

- An elevated three-quarter camera supports pan, constrained zoom and pitch, free yaw, and an orientation-reset command.
- World clicks and portraits select either character; additive selection and select-all are available.
- Right-click or its documented equivalent issues contextual move, attack, or interact orders.
- Ability controls expose valid targets, range, cooldown, and rejection reasons.
- Selection, health, current target, destination, cooldowns, and pending primary action are legible.
- Space toggles tactical pause.
- Entering combat automatically pauses once; after that, only the player toggles pause.
- While paused, movement, AI, attacks, damage-over-time, ability execution, and cooldowns do not advance.
- Camera, UI, selection, inspection, observations, and command entry remain responsive while paused.
- Each character may hold one pending primary action. A new primary action replaces the previous pending action; the POC has no arbitrary action queue.
- Invalid orders return visible and structured rejections without partial state changes.

## Character and inventory acceptance

- The player chooses between two simple protagonist kits before entering the level.
- Each possible protagonist has one fixed weapon, a repeatable basic attack, and one distinctive active ability.
- The fixed companion supplies a complementary weapon and active ability.
- Character information displays role, health, weapon, ability, and carried healing item.
- One healing item can be inspected and used on a valid party member.
- The POC does not include loot generation, equipment comparison, encumbrance, crafting, vendor screens, or a general-purpose inventory grid.

## Combat acceptance

- Hostiles perceive, acquire targets, move into range, attack, and can be defeated.
- At least one party ability targets a position, hostile, or ally so coordinated pause is materially useful.
- Hostile intent or dangerous actions are telegraphed clearly enough to support an interrupt, reposition, focus-fire, or defensive response.
- Basic attacks repeat against an explicitly assigned target; abilities remain player-directed.
- Movement and action replacement behave consistently before and during tactical pause.
- Victory and defeat are unambiguous; defeat offers immediate scenario restart.
- Damage may use a seeded random source, but the encounter must not be decided mainly by random variance.

## Dialogue acceptance

- At least two authored NPC interactions offer player responses.
- Conversation is offered whenever an eligible NPC is present, aware, reachable, and not blocked by explicit scenario state.
- One choice changes observable authoritative state and a later part of the same playthrough.
- Dialogue UI clearly distinguishes spoken text, available responses, unavailable responses, and the end of the exchange.
- Authored dialogue and future generated proposals enter the same validated interaction boundary.
- The POC has no runtime LLM dependency. A separate local experiment may use the official Codex CLI with ChatGPT sign-in; it never scrapes or automates the ChatGPT website.

## Automation acceptance

- A stable automation adapter can observe state, list relevant entities, read events after a sequence number, submit a typed command through a JSON boundary, advance fixed ticks, advance until a condition or event, pause, and resume.
- Human input, automation, scenarios, and future replays reach the same gameplay dispatcher.
- A fast simulation scenario exercises rules without launching the graphical editor.
- A Godot integration scenario loads the real level and completes the critical path without mutating private scene state.
- Tests prove pause invariants, command rejection atomicity, recruitment, combat completion, item use, and the authored critical path.
- A graphical check proves real input, rendering, navigation, camera, UI, and screenshot capture.
- Scenarios record seeds when randomness exists. We require reproducible rule outcomes under the same build, not bit-identical animation or navigation across engine versions and hardware.

## Content budget

- One authored greybox station level with a start, service section, encounter space, and destination.
- Two selectable protagonist kits and one fixed companion.
- Two meaningful NPC interactions.
- One hostile machine group with at most two enemy behaviors.
- One fixed weapon per character, one active ability per character, and one healing-item type.
- One observable choice consequence.
- Primitive or explicitly reviewed low-poly assets only.
- Minimal sound and effects only after interaction readability works.

## Explicit non-goals

No procedural level, roguelite run structure, metaprogression, save migration, runtime model call, generalized quest engine, ship simulation, vehicle movement, boarding, crafting, economy, reputation simulation, multiplayer, or production-scale asset generation belongs in this POC.

## Exit gate

- Five consecutive full manual playthroughs complete without a blocker.
- The core unit suite, deterministic scenario suite, Godot headless smoke suite, pause suite, and critical-path scenario pass.
- Both protagonist kits have completed at least one manual playthrough.
- The player can understand selection, pending actions, combat threats, item use, dialogue choices, victory, defeat, and completion from the game itself.
- A fresh agent can restore, build, run, inspect, and reproduce the scenario using only the repository documentation.
