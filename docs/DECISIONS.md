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
