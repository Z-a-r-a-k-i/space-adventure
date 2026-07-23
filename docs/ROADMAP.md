# Roadmap

Milestones use playable outcomes and exit gates rather than speculative dates. Each phase should leave the project runnable and should avoid building the next phase's systems early.

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

Status: implemented; automated and graphical verification are implemented, but the required human playthrough is pending. Phase 2 has not passed its exit gate.

- Greybox the compact station route with a start and evacuation-airlock destination.
- Add one controllable protagonist, elevated tactical camera, point movement, navigation, interaction, objective state, and completion.
- Add one minimal authored NPC exchange and one optional environmental interaction.
- Add a Godot critical-path scenario and real-input graphical playtest.

Implemented evidence includes the authored `station_route.tscn`, versioned `station-route.json` content, pure-core route tests and CLI scenario, real-Godot `station-route` headless smoke, runtime observation/input helpers, the graphical control path, the opaque cached-AABB wall-cutaway POC, and its deterministic 1280×720 PNG/JSON capture command. The automated route performs the mandatory survivor exchange, optional terminal inspection, and airlock completion through the same typed commands used by human input. The capture keeps gameplay paused while exercising settled cut → clear-view restore → re-cut states through live camera yaw, then records the original final view. It provides repeatable lifecycle and final-frame evidence, not proof of perceived smoothness or absence of flicker and not a replacement for human judgment.

Exit remains: a human and an agent can independently move from the start, interact, and reach the destination through the real Godot level. The human must also confirm that walls cut away and restore readably across normal camera movement. The agent path is implemented; record a human playthrough and resolve any usability blocker before changing this phase to completed or beginning Phase 3.

### Bounded visual preproduction spike

Status: planned and non-gating.

Before the Phase 2 human gate closes, visual preproduction may run one controlled three-asset bake-off covering a service terminal, wall utility, and compact security-drone body. The experiment is defined in `art/experiments/3d-generator-bakeoff-2026-07.md` and is capped at disposable candidates plus Blender baselines. It may establish art direction and measure whether a generator saves cleanup time; it does not replace greybox gameplay, alter the live route, select a permanent provider, begin Phase 3 or Phase 6, or authorize production-scale asset generation.

The complete approved POC target inventory may be maintained in
`POC-ASSET-ROSTER.md` as planning documentation during this period. Roster
approval does not expand the experiment or authorize execution on asset IDs
beyond the three listed above.

Exit: each of the three briefs has bounded Tripo, Meshy, and Blender evidence or a documented provider failure; the experiment records provenance, tactical-camera captures, measurements, and a result of `tripo`, `meshy`, `blender`, or `none`. This exit does not affect the Phase 2 status or human-playthrough requirement.

## Phase 3 — party and conversation slice

Status: deferred until the Phase 2 human-playthrough gate passes.

- Add protagonist-kit selection. The presentation weapons are already selected
  as Vanguard carbine, Operator pistol, and Protector shotgun; define their
  stable gameplay attacks and the three active abilities before producing
  ability-specific art.
- Add the recruitable companion, portraits, individual and group selection, simple formation movement, and party observations.
- Add the two authored conversations and one observable choice consequence.
- Verify dialogue state validation, recruitment, pause behavior, and both kit paths.

Exit: the player starts alone, recruits the companion through conversation, and controls both characters through the traversal section.

## Phase 4 — active-pause combat slice

- Add basic attacks, one active ability per character, cooldowns, health, damage, healing-item use, and readable pending actions.
- Add the approved body-ram drone and integrated-gun sentry as at most two
  hostile machine behaviors, with target acquisition, telegraphs, victory,
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

## Separately gated post-POC experiments

1. Deeper tactics, more abilities, progression, equipment, and a larger party.
2. A larger static adventure with saving and authored quest structure.
3. A procedural-run prototype and only then possible metaprogression.
4. Controlled dialogue generation, beginning with authored/scripted and recorded providers plus an optional local Codex CLI experiment.
5. Ship command and vehicle combat, with boarding as one possible resolution.
6. Scaled AI-assisted environment, character, animation, and content production.

If two-character active-pause combat is not clear and enjoyable in greybox, improve or reconsider it before adding progression, procedural generation, vehicles, ships, or live model dialogue.
