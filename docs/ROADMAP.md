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

Status: active. The Phase 2 human-playthrough gate passed on 2026-07-24.

On 2026-07-24 the owner explicitly authorized production and provisional
integration of the approved roster assets on the dedicated art branch. This
scope change permits Blender-owned source work, provisional GLB publication,
and isolated asset-gallery integration while retaining an immediate greybox
fallback. It does not define attacks or abilities, approve final visuals,
replace the live station-route actors, or bypass the shared-rig and complete-
assembly gates.

Vanguard production art is active. The previous rigged character and animation
experiments were removed. The retained neutral-pose Tripo Smart Low-Poly v2
Quad mesh is comparison material only; a conforming unrigged T-pose source and
Quad-10k retopology are required before human Mixamo marker approval and
Blender weight repair. Keep the existing Vanguard greybox in the live game
until the new character, carbine assembly, animation, and exact Godot import
receive direct human approval.

- Add protagonist-kit selection. The presentation weapons are already selected
  as Vanguard carbine, Operator pistol, and Protector shotgun; define their
  stable gameplay attacks and the three active abilities before producing
  ability-specific art.
- Integrate and approve each human as one fixed runtime outfit on the shared
  skeleton, using any accepted offline source work. Before Vanguard runtime
  publication, complete
  source revision or reselection, segmentation, remeshing, fitting, shared-rig,
  character-plus-carbine grip and holster assembly, animation review, direct
  tactical Godot inspection, and final human approval. Do not add runtime armor
  slots.
- Add the recruitable companion, portraits, individual and group selection, simple formation movement, and party observations.
- Add the two authored conversations and one observable choice consequence.
- Verify dialogue state validation, recruitment, pause behavior, and both kit paths.

Exit: the player starts alone, recruits the companion through conversation, and controls both characters through the traversal section.

## Phase 4 — active-pause combat slice

- Add basic attacks, one active ability per character, cooldowns, health, damage, healing-item use, and readable pending actions.
- Add the approved humanoid Security Enforcer and integrated-gun sentry as at
  most two hostile behaviors, with target acquisition, telegraphs, victory,
  defeat, and restart.
- Tune action replacement, basic-attack repetition, target visibility, and pause rhythm through repeated play.
- Add core, CLI, Godot headless, and graphical combat checks.

Exit: active pause materially helps coordinate both characters; the encounter is readable and enjoyable enough to merit iteration; victory and defeat work through human and automated control.

## Phase 5 — authored POC completion

- Join the tutorial, conversations, traversal, optional interaction, item use,
  encounter, choice consequence, visibly opening evacuation airlock, and
  completion summary into one 8–12 minute flow.
- Add only the sound, effects, and UI polish needed for comprehension.
- Test both protagonist kits and tune the critical path.
- Record manual playtests and address blockers before adding breadth.

Exit: five consecutive full manual playthroughs complete without a blocker; both protagonist kits have been played; and all documented automated suites pass.

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
