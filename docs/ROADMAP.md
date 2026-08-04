# Roadmap

Milestones use playable outcomes and exit gates rather than speculative dates.
Each phase should leave the project runnable and should avoid building the next
phase's gameplay systems early. Offline source production for an approved
roster asset may proceed ahead of its gameplay phase under ADR 0016 when its
reference, production-ready art brief, ownership, licensing, and privacy gates
pass. Staged art and isolated asset-gallery review do not activate the owning
gameplay phase or authorize live content replacement and integration.

## Phase 0 — document and baseline agreement

Status: completed.

- Align the vision, POC, architecture, technology, automation, dialogue, art, and agent rules.
- Record unresolved product choices with recommended defaults and decision deadlines.
- Confirm the Godot 4.7.1 Mono and .NET installation paths.
- Classify the previous GDScript spike as superseded reference material.

Exit: the user accepts the documents or identifies the specific changes required before bootstrap.

## Phase 1 — C# technical bootstrap

Status: completed. The typed `set_pause` path is proven through core tests, the CLI, and the Godot host. Restore, build, tests, import, bounded headless launch, graphical launch, live runtime control, input injection, clean shutdown, and viewport inspection have passed. A separate plugin-free copy also built and passed its headless smoke.

- Create the root solution, pure core project, Godot .NET project, test project, simulation CLI, and canonical PowerShell command surface.
- Pin the intended SDK behavior and enable nullable references and analyzers.
- Prove restore, build, one core test, one CLI scenario, Godot import, headless launch, graphical launch, and clean shutdown.
- Link the external Godot addon locally and prove runtime inspection, input injection, and viewport capture without making it a dependency.
- Isolate Godot user data, logs, and MCP ports for worktrees.

Exit: a clean checkout can run the same minimal typed command through a core test, the CLI, and the Godot host; the result is structurally observable; and a graphical frame can be captured and inspected.

## Phase 2 — start-to-destination walking skeleton

Status: completed on 2026-07-24. Automated and graphical verification and the required owner-operated physical-input playthrough have passed.

- Greybox the compact station route with a start and evacuation-airlock destination.
- Add one controllable protagonist, elevated tactical camera, point movement, navigation, interaction, objective state, and completion.
- Add one minimal authored NPC exchange and one optional environmental interaction.
- Add a Godot critical-path scenario and real-input graphical playtest.

Implemented evidence includes the authored `station_route.tscn`, versioned `station-route.json` content, pure-core route tests and CLI scenario, real-Godot `station-route` headless smoke, runtime observation/input helpers, the graphical control path, the opaque cached-AABB wall-cutaway POC, and its deterministic 1280×720 PNG/JSON capture command. The automated route performs the mandatory survivor exchange, optional terminal inspection, and airlock completion through the same typed commands used by human input. The capture keeps gameplay paused while exercising settled cut → clear-view restore → re-cut states through live camera yaw, then records the original final view. It provides repeatable lifecycle and final-frame evidence, not proof of perceived smoothness or absence of flicker and not a replacement for human judgment.

Exit passed: a human and an agent independently moved from the start, interacted, and reached the destination through the real Godot level. On 2026-07-24 the owner completed the documented fresh-process physical-input protocol, confirmed readable wall cutaway and restoration across normal camera movement, and reported no usability blocker.

## Phase 3 — party and conversation slice

Status: active. The Phase 2 human-playthrough gate passed on 2026-07-24. The
route is being resequenced before combat: Vanguard now reaches a solo tutorial
fight before recruiting Protector; Operator is deferred.

On 2026-07-24 the owner explicitly authorized production and provisional
integration of the approved roster assets on the dedicated art branch. This
scope change permits Blender-owned source work, provisional GLB publication,
and isolated asset-gallery integration while retaining an immediate greybox
fallback. That original authorization did not define attacks or abilities,
approve final visuals, replace live station-route actors, or bypass the shared-
rig and complete-assembly gates; later owner instructions advanced Vanguard.

Vanguard production art is active. The conforming direct single-image 4K
T-pose source, Smart Low-Poly v2 Quad-10k result, human-approved Mixamo rig,
Unarmed Idle, and in-place Standard Walk now replace the protagonist greybox
in the live station route. Blender validation and a fresh GLB
reimport confirm a grounded 1.82 m character at the gameplay origin; direct
Godot inspection confirms visible idle and locomotion playback.

On 2026-08-04 the station presentation advanced to route revision
`station-route-v5`. `kit.station.structure.v2` now covers the complete five-area
serpentine route, and two instances of `assembly.station.service_door.v1`
bound the solo-combat arena. Deterministic Blender sources and GLBs replace
presentation geometry while Godot keeps navigation, collision, lighting,
interaction identity, and route state. The entry link opens only after the
survivor choice; moving through it automatically completes and opens the entry
door before Vanguard reaches it. The far link remains locked.
The evacuation-airlock assembly remains unchanged at the final destination.
Automated route verification intentionally stops at the first-combat threshold.

- Keep Vanguard as the only protagonist. Produce Protector as the fixed recruit
  encountered after the solo tutorial fight; do not restore a character-choice
  screen. Operator and its pistol are deferred. The active party weapons are
  Vanguard's carbine and Protector's shotgun; define gameplay attacks before
  producing ability-specific art.
- Integrate and approve each active human as one fixed runtime outfit on the
  shared skeleton. Vanguard's body, idle, and walk are integrated. Before
  combat implementation, replace the survivor and Protector greyboxes and
  prepare reviewed non-greybox base presentations for the first-combat
  Security Enforcer and sentry. Complete weapon fit and combat animation with
  the Phase 4 rules and timings. Do not add runtime armor slots.
- Preserve the implemented party cards, individual and group selection,
  formation movement, and observations, but move Protector recruitment after
  the solo tutorial fight. Portraits remain presentation work.
- Preserve the survivor route choice, observable consequence, and Protector
  recruitment exchange, gating that exchange in the post-fight room.
- Retain the integrated production station kit and service doors across the
  start, first-combat, recruitment, main-encounter, and destination rooms.
  Keep all primitive collision, navigation, lighting, and interaction wrappers
  invisible and keep the final airlock publication unchanged.
- Verify dialogue state validation, door progression, pause behavior, and the
  Vanguard path to the first-combat threshold. Repeated full manual play
  remains open.

Exit: the complete authored route uses production-presented environment with no
visible environment, NPC, or combatant greybox. The player starts as Vanguard,
talks to the production survivor, crosses the ordinary service door, and
reaches the production-presented first-combat room. Protector and both hostile
archetypes have approved production bases ready for their Phase 4 scenes.

## Phase 4 — active-pause combat slice

- Add the short solo tutorial fight, then unlock the Protector recruitment
  exchange and the later two-character encounter.
- Add basic attacks, one active ability per active party character, cooldowns,
  health, damage, healing-item use, and readable pending actions.
- Add the approved humanoid Security Enforcer and integrated-gun sentry as at
  most two hostile behaviors, with target acquisition, telegraphs, victory,
  defeat, and restart.
- Select, clean, and integrate draw, attack, contact, recoil, recovery, and
  holster animations alongside the authoritative combat timings rather than
  finalizing fight animation in advance.
- Tune action replacement, basic-attack repetition, target visibility, and
  pause rhythm first in the solo tutorial and then with Vanguard plus Protector.
- Add core, CLI, Godot headless, and graphical combat checks.

Exit: the solo fight teaches the combat controls, Protector is recruited only
after it, active pause materially helps coordinate both characters in the main
encounter, and victory and defeat work through human and automated control.

## Phase 5 — authored POC completion

- Join the tutorial, conversations, traversal, optional interaction, item use,
  encounters, choice consequence, visibly opening evacuation airlock, and
  completion summary into one 8–12 minute flow.
- Add only the sound, effects, and UI polish needed for comprehension.
- Test Vanguard throughout the critical path and Protector from recruitment
  through the main encounter; there is no multi-kit protagonist choice.
- Record manual playtests and address blockers before adding breadth.

Exit: five consecutive full manual playthroughs complete without a blocker;
Vanguard's production presentation has been exercised throughout, Protector's
from recruitment onward, and all documented automated suites pass.

## Phase 6 — POC production hardening

- Re-run and audit one accepted representative character or environment asset
  through the hardened Blender, GLB, multi-angle, Godot import, and
  structured-review pipeline from a clean checkout.
- Document encounter, dialogue, scenario, and asset authoring from a clean checkout.
- Establish startup, frame-time, memory, test-duration, and artifact-size baselines.
- Exercise parallel-agent work on genuinely separate subsystems and fix observed coordination problems.

Exit: another agent can add a small authored encounter and one reviewed asset using only the repository workflow.

## Phase 7 — escape-cutter ship-combat proof of concept

Status: direction approved on 2026-07-29; implementation is gated until the
Phase 6 exit passes unless the project owner explicitly reprioritizes it in a
later decision.

- Keep the station POC ending at its evacuation-airlock completion summary.
- Begin a separate authored scenario with a short launch-bay transition,
  entering the cutter, and one fixed hostile encounter.
- Reuse the pure C# gameplay core, typed command boundary, explicit fixed tick,
  structured observations, and active-pause semantics.
- Add exactly two player crew, one player cutter, one deterministic hostile
  ship, weapons, engines, shields, a fixed reactor budget, crew room movement,
  system targeting, damage, repair, victory, defeat, and restart.
- Present two separate strict-overhead ship views with the player ship on the
  left, the enemy ship on the right, both pointing upward, a central divider,
  and no movement or trajectory lines.
- Prove the complete deterministic greybox before authorizing final ship
  models, production UI, or integration with the station scenario.

The complete content budget, rules, non-goals, and verification layers are
defined in `SHIP-COMBAT-POC.md`.

Exit: pure-core tests, the deterministic CLI encounter, Godot headless smoke,
direct graphical inspection, and an owner-operated physical-input playthrough
all pass; pausing to reassign crew, power, and target is readable and
tactically useful.

## Separately gated post-POC experiments

1. Deeper tactics, more abilities, progression, equipment, and a larger party.
2. A larger static adventure with saving and authored quest structure.
3. A procedural-run prototype and only then possible metaprogression.
4. Controlled dialogue generation, beginning with authored/scripted and recorded providers plus an optional local Codex CLI experiment.
5. Deeper ship and vehicle command beyond Phase 7, with boarding as one
   possible later resolution.
6. Scaled AI-assisted environment, character, animation, and content
   production.

If two-character active-pause ground combat is not clear and enjoyable in
greybox, improve or reconsider it before Phase 7 or before adding progression,
procedural generation, vehicles, deeper ship systems, or live model dialogue.
