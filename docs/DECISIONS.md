# Architecture decision log

This log replaces the Godot 4.6 and GDScript decisions made by the superseded foundation spike.

## ADR 0001 — Godot 4.7.1 .NET desktop foundation

Status: accepted.

Use the official Godot `4.7.1.stable.mono` build, Forward+ rendering, and a Windows desktop target for the POC. Use the engine's supported default 3D physics configuration until a concrete requirement justifies changing it. Mobile, web, console, and multiplayer constraints do not guide the initial architecture.

The automation scripts resolve the console executable from `SPACE_ADVENTURE_GODOT`, an explicit argument, or a small documented candidate list. The verified local installation is under `C:\Program Files\Godot_v4.7.1-stable_mono_win64`.

## ADR 0002 — C# with a pure gameplay core

Status: accepted; supersedes the former GDScript decision.

Use C# for game-owned code. `SpaceAdventure.Core` targets `net8.0` and does not reference Godot. The Godot .NET project, simulation CLI, and tests reference the core. This gives gameplay rules compile-time refactoring, fast tests, a direct CLI path, and a clear boundary for parallel agents.

Godot addons may contain GDScript, but project gameplay does not grow parallel C# and GDScript implementations. Enable nullable reference types and built-in analyzers. Add third-party frameworks only for current evidence-backed needs.

## ADR 0003 — Authored demo before roguelite structure

Status: accepted.

The first playable POC is an 8–12 minute authored journey from a start to a destination. The working setting is a disabled frontier transfer station. Procedural levels, run generation, and metaprogression are later experiments.

This establishes a stable case for testing exploration, recruitment, dialogue, inventory use, active-pause combat, and completion before generating content at scale.

## ADR 0004 — Typed commands and an explicit simulation clock

Status: accepted.

Human input, automation, scenarios, and future replays dispatch the same typed C# commands. JSON DTOs exist only at external boundaries. `GameSession` owns a fixed 30 Hz gameplay clock. Tactical pause stops gameplay ticks without stopping UI, input, camera, observation, or command entry. Presentation never mutates authoritative state.

Every command is fully validated before mutation and produces a typed acknowledgement or stable rejection. Each party member has at most one pending primary action in the POC; a new accepted order replaces it.

## ADR 0005 — No runtime LLM dependency in the POC

Status: accepted.

The POC critical path uses authored dialogue. An optional local development provider may automate schema-constrained requests through the official Codex CLI authenticated with the developer's ChatGPT subscription. Manual inbox and recorded providers remain available. We do not scrape ChatGPT, automate its website, use unofficial session endpoints, or distribute the developer's credentials. All generated proposals remain outside authoritative state validation.

## ADR 0006 — Controlled GLB publication pipeline

Status: accepted in direction; implementation deferred to the art milestone.

Use Blender 5.2 LTS as the current editable-source and automation baseline, GLB as the published runtime model format, and standardized Blender plus Godot renders for review. Record provenance and tool versions, validate mechanically, and require structured visual review. Prominent assets also require human approval.

The official Blender MCP is a development control surface, not the pipeline's source of truth. Pipeline operations must ultimately be reproducible by versioned Blender scripts or explicit profiles.

## ADR 0007 — Worktrees for parallel implementation

Status: accepted.

Concurrent agents use separate branches and Git worktrees with explicit ownership. Worktrunk is the preferred convenience layer on Windows, but ordinary Git worktrees remain valid. Generated Godot state, user-data roots, running editors, logs, and Godot MCP ports are isolated per worktree.

Shared scenes, `project.godot`, command schemas, and binary assets have one integration owner. Task assignment never implies authorization to commit or push.

## ADR 0008 — Separate rule, engine, and visual verification

Status: accepted.

Use four complementary layers:

1. .NET unit tests for pure rules.
2. A simulation CLI for deterministic command scenarios.
3. Godot headless tests for engine integration.
4. Real graphical playtests and screenshots for interaction and visual quality.

Passing a lower layer does not replace a relevant higher-layer check. The optional Godot MCP accelerates graphical control and inspection but is not a test dependency.

## ADR 0009 — Text gameplay definitions and Godot spatial scenes

Status: provisional for the POC.

Store small gameplay definitions in versioned, validated text data that loads into core C# records. Use Godot scenes for level layout, navigation, collision, spawn markers, and presentation references. Connect them with stable IDs.

This keeps rule content usable by the CLI and friendly to diffs and agents while retaining Godot's strengths for spatial editing. Revisit after one full encounter is authored; if duplicated wiring or poor editor ergonomics dominate, add a dedicated authoring adapter rather than moving authority into scene nodes.

## ADR 0010 — Minimal POC action semantics

Status: accepted.

Each character owns a current action and at most one pending primary action. New accepted primary orders replace pending ones. The POC does not implement arbitrary queues, programmable AI, behavior scripting, or a timeline editor.

Basic attacks repeat against an explicitly assigned target while active abilities require direct orders. Playtesting may revise this if it fails to create the intended Aarklash-like control rhythm.

## ADR 0011 — Quality-first, selectable dialogue profiles

Status: accepted for the dialogue experiment.

The development dialogue panel selects model, reasoning effort, and Fast mode independently. The initial baseline is `gpt-5.6-sol`, medium reasoning, and Fast mode off. Profiles can change without a rebuild, apply only to new requests, and are recorded with every result. Coherence, factual consistency, relevance, and voice rank ahead of latency; performance and credit use remain measured constraints.

Product-specific settings live in the local provider and experiment configuration, never authoritative gameplay state or save data. The UI exposes only values actually available through the signed-in Codex installation and rejects unsupported combinations instead of silently substituting them.

## ADR 0012 — Combat pauses automatically only when combat starts

Status: accepted for the POC.

Entering combat automatically pauses the simulation once so the player can assess the situation and issue orders. After that, pause is manual. The POC does not auto-pause for low health, ability readiness, target loss, incapacitation, or telegraphed attacks.

## ADR 0013 — Free-text input is literal protagonist speech

Status: accepted for the dialogue experiment.

When the player types dialogue, the submitted text is the protagonist's exact spoken utterance. The game does not rewrite it into another line or silently change its meaning. Authored response suggestions provide a convenient canonical voice without limiting free-form input.

Typed text remains untrusted content. It cannot establish world facts, grant authority, or directly cause gameplay effects; NPCs may reject or challenge unsupported claims, and only validated deterministic transitions can change authoritative state.

## ADR 0014 — Attack rules are independent from weapon presentation

Status: accepted for combat and asset planning; offline source work follows
ADR 0016 and gameplay implementation remains in Phases 3 and 4.

Every production combatant declares at least one credible attack source before
its model, rig, or animation is approved. The presentation source is
`handheld`, `integrated`, or `body`; these are art and Godot profiles, not
different gameplay architectures.

The pure core owns stable attack identity, target validity, range, fixed-tick
timing, movement, resolution, damage, interruption, and repetition. Godot
consumes observations and events to attach weapons and play rigs, animations,
telegraphs, effects, and audio. Animation callbacks, mesh collisions, sockets,
and asset node paths never mutate authoritative gameplay.

Humanoid handheld weapons are separate presentation assets even when their POC
loadout is fixed. Integrated machine weapons may remain permanent parts of the
machine, and a body attacker needs no weapon when its striking surface and
motion are clear. These choices do not add weapon switching, ammunition,
reload, equipment slots, or generalized inventory. Detailed production gates
are defined in `ATTACK-PRESENTATION.md`; the approved POC presentation profiles
and phase-scoped target inventory are recorded in `POC-ASSET-ROSTER.md`.

## ADR 0015 — Fixed POC outfits retain modular editable sources

Status: accepted for the POC art pipeline; offline source work follows ADR
0016 and runtime integration remains in Phase 3.

Each human is presented and reviewed in one fixed complete outfit. The POC
does not equip separate chest, leg, boot, glove, or other armor items and does
not add armor compatibility rules or generalized equipment UI.

The Blender source retains a close-fitting technical undersuit and major armor,
footwear, glove, and accessory pieces as named objects on the normalized shared
humanoid skeleton when practical. The published GLB may keep multiple skinned
meshes, but Godot treats the approved combination as one outfit. Hidden
undersuit or body polygons may be omitted beneath rigid armor to prevent
clipping; a complete unclothed body is not a requirement. Handheld weapons
remain separate under ADR 0014.

Provider part generation, segmentation, part completion, retopology,
low-poly conversion, remeshing, and fitting occur before final rigging because
mesh reconstruction invalidates skeletons and weights. Blender owns the final
topology, fit, rig, weights, attachments, and export. Whole-outfit variants are
the preferred future extension. Individual equipment slots require a separate
product and architecture decision.

## ADR 0016 — current 3D character production pipeline

Status: accepted by the project owner on 2026-08-02; production defaults
amended after the conforming Vanguard T-pose pilot on 2026-08-03 and the
operator-autonomy clarification on 2026-08-04.

Offline art production requires an approved roster asset, reference, brief,
human approver, licensing/privacy state, and dedicated art worktree. It does not
authorize live gameplay replacement, ability work, or attack-timing decisions.

Humanoids use one approved front-view T-pose seed and Tripo's direct
single-image HD workflow: v3.1 Best Quality, Ultra Mesh Quality, Triangle 2M,
4K PBR, AI Complete off, Generate in Parts off, and 8K Texture off. Generate
Multi-Views is reserved for a named coverage defect rather than required by
default. The unrigged result is retopologized with Smart Low-Poly v2, Quad, and
a 10,000 target before any rigging.

Mixamo receives the geometry-only FBX while the 4K ZIP remains the material
master. The assigned art operator uses a front orientation, symmetry, Standard
Skeleton (65), and visually verified chin, wrist, elbow, knee, and groin/hip
markers. The operator validates the complete marker placement and Auto-Rigger
preview without intermediate human confirmation, escalating only a named
defect or scope change. Mixamo provides the with-skin neutral rig baseline and
existing animation library; production animation donors are downloaded without
skin by default. `Standard Walk` with In Place on, Overdrive 50, Character Arm-Space
50, 30 fps, and no keyframe reduction is the default exploration walk. An
untouched with-skin locomotion FBX must first pass a sustained direct Godot
baseline. Blender owns weight repair, sockets, animation cleanup, and GLB
export. Animated Mixamo armature transforms are preserved; normalization
requires retargeting and evaluated world-space baking onto a separate rig.
Full-cycle world-space validation and comparison with the untouched Godot
baseline are publication gates. A character-specific exception may retain a
matching with-skin donor when the no-skin export changes the accepted rest pose
or armature transform. Tripo Auto Rig and AI-generated humanoid motion are not
production defaults.

Successful provider ingestion or rigging does not waive the unrigged T-pose
source contract. Any grandfathered non-T-pose source, Tripo Auto Rig use, or
AI-generated humanoid motion requires a named project-owner-approved exception
recorded in this decision and in `POC-ASSET-ROSTER.md` before it can advance.

Vanguard uses a conforming T-pose source. Its with-skin `Standard Walk` donor
is an approved character-specific export exception because the tested no-skin
baseline export did not preserve the accepted rest pose. The untouched
with-skin FBX and the final GLB both passed direct Godot locomotion review on
2026-08-03.

Skeletal animation is limited to humanoids. Non-humanoids must be simple
floating or stationary rigid machines with only a few authored pivots.

Untouched provider exports remain in the ignored run-local cache. A tracked
manifest records provider/version, path, settings, byte size, and presence.
Large 3D binaries are not content-hashed. Temporary screenshots and review
renders remain ignored; one representative image per checkpoint is enough when
a frozen handoff is useful. Rejected models, scripts, and animation attempts are
removed from the active baseline instead of retained as workflow instructions.

## ADR 0021 — Ship combat is a separate bounded post-POC slice

Status: accepted by the project owner on 2026-07-29.

Phase 7 may test one authored ship battle only after the station POC and its
production-hardening gate. It reuses the pure C# core, typed commands, fixed
tick, atomic validation, observations, and tactical pause. The bounded slice
contains two controllable party members aboard one escape cutter, one hostile
ship, weapons, engines, shields, a fixed reactor budget, one weapon per ship,
and authored room movement or repair. Enemy crew are not simulated.

The approved composition reference is
`art/concepts/station-escape-ship-combat-v1/ship-combat-separated-clean-direction-v4.png`:
strict overhead, player ship left, enemy ship right, bows upward, central
divider, and no trajectory lines. It authorizes visual direction only. Final
rooms, 3D assets, UI, balance, and integration wait for a deterministic
greybox. Free flight, procedural encounters, progression, upgrades, oxygen,
fire, breaches, missiles, drones, boarding, enemy crews, and multiplayer remain
deferred. See `docs/SHIP-COMBAT-POC.md`.

## ADR 0022 — A humanoid Security Enforcer replaces the ram drone

Status: accepted by the project owner on 2026-08-02 for Phase 4 presentation
and asset planning; visual reference approved the same day.

The mobile close-range hostile is a non-sapient humanoid Security Enforcer with
a reinforced-forearm body attack. It uses the shared T-pose, Tripo Quad-10k,
validated Mixamo, and Blender pipeline. It carries no weapon and publishes
one reviewed contact socket. The ranged hostile remains a stationary rigid gun
sentry with aim and recoil pivots.

This pair preserves mobile melee pressure versus stationary ranged pressure
without adding custom non-humanoid locomotion or skeletal-machine animation.
Gameplay still owns attack range, timing, target validity, movement, contact,
damage, and interruption. The approved Enforcer sheet still requires separate
production-brief acceptance before 3D generation begins.

## ADR 0023 — Static station presentation advances into Phase 3

Status: accepted by the project owner on 2026-08-02.

The station structure kit, service terminal, and evacuation airlock advance
from Phase 5 into the active Phase 3 route. They are authored dimensionally in
Blender from the approved references and published as static or rigid GLBs.
They do not use Tripo retopology, Mixamo, skinning, or the humanoid animation
pipeline.

Godot remains authoritative for navigation, collision, lighting, stable
interaction IDs, and door presentation timing. The pure C# core remains
authoritative for route state, progression, completion, and gameplay rules.
The imported structure exposes independent named walls for camera cutaway; the
airlock exposes two named rigid leaves driven from observed completion state.
Existing greybox presentation is hidden rather than used as a second visible
layer, while its gameplay wrappers remain intact.

This reprioritization does not advance combat, inventory, character
replacement, wall-utility integration, or other Phase 5 polish. The integrated
candidates require a live graphical playthrough and final project-owner visual
approval before their briefs may be marked fully accepted.

## ADR 0024 — Solo combat precedes Protector recruitment

Status: accepted by the project owner on 2026-08-04; supersedes the encounter
ordering that recruited Protector before combat.

Vanguard is the only protagonist. The compact station route now proceeds from
the start room and survivor interaction through an ordinary service door to a
short solo tutorial fight. Protector is recruited in the following room, after
which Vanguard and Protector face the main two-character encounter. Operator
and its pistol are deferred. The evacuation-airlock assembly is reserved for
the final destination.

Before Phase 4 gameplay implementation begins, all visible environment, NPC,
and combatant presentation across the active station route must use reviewed
production assets. Godot may retain invisible authored primitives for
collision, navigation, interaction, lighting, and gameplay wrappers. Base
models, rigs, idle, and locomotion may be prepared during Phase 3, but draw,
attack, contact, recoil, recovery, and holster animation is selected and
finalized alongside the authoritative Phase 4 rules and timing. Animation
callbacks and physical presentation remain non-authoritative under ADR 0014.

## ADR 0025 — Station route v2 ends at an authoritative service-door gate

Status: accepted by the project owner on 2026-08-04 for PR 14.

The station uses a five-area serpentine 1 m-grid layout published as
`kit.station.structure.v2`. Two instances of the rigid
`assembly.station.service_door.v1` assembly bound the solo-combat arena. The
entry door becomes available after either survivor response and completes
atomically when Vanguard approaches it on an accepted movement path, before
the current objective advances to the first-combat threshold. Its Godot
navigation link derives from the available-or-completed authoritative state;
its collision blocker and leaf presentation derive from completion. The far
door remains observable but unavailable until Phase 4 supplies combat victory;
Protector recruitment and the final airlock therefore remain unavailable and
unreachable. Direct interaction remains valid for an available door, but is
not required to cross it.

Service-door leaf motion is a 0.25-second presentation synchronized from
observations. Animation callbacks never mutate gameplay. Navigation is split
at each doorway, and no development bypass preserves the superseded full-route
completion. The CLI and Godot station-route scenarios now pass by entering the
solo arena, verifying the far lock, and stopping with the scenario still in
progress. Static structure metadata supplies stable wall-occluder IDs; Godot
discovers them recursively from the imported GLB instead of maintaining a wall
name map. This decision adds two explicit door effects, not a generic quest or
gate framework.
