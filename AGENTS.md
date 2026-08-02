# SpaceAdventure agent instructions

SpaceAdventure is a single-player 3D science-fiction party RPG built with Godot 4.7.1 .NET and C#. Before changing gameplay or architecture boundaries, read `docs/PRODUCT.md`, `docs/POC.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, and `docs/OPEN-QUESTIONS.md`.

The earlier GDScript foundation spike is superseded and absent from the active baseline. Do not reintroduce or mechanically port it unless a recorded decision explicitly calls for that work.

## Working rules

1. Deliver the smallest playable slice that tests the current milestone. Do not pull deferred systems into the active milestone.
2. Game code is C#. GDScript is permitted only inside external or third-party Godot addons unless a new decision records an exception.
3. Authoritative gameplay rules live in a pure C# core without Godot node, scene, input, rendering, or serialization dependencies.
4. Human input, automation, scenarios, and future replays dispatch the same typed gameplay commands. JSON is an adapter format, not the internal command model.
5. Simulation advances through an explicit fixed tick. Tactical pause stops gameplay advancement while input, UI, camera, observation, and command entry remain available.
6. Presentation reads gameplay state and events; it does not directly mutate authoritative state.
7. Use stable game identifiers. Node paths, object instance IDs, and display names are not gameplay identifiers.
8. Validate a command completely before mutation. Rejections are structured, observable, and never leave partial effects.
9. Prefer authored greybox content until the POC exit gate. Runtime LLM calls, procedural worlds, vehicles, boarding, generalized inventory, multiplayer, and metagame systems are outside the current milestone.
10. Add dependencies only for a demonstrated current need and record consequential choices in `docs/DECISIONS.md`.
11. Never commit or push unless the user explicitly requests it.

## C# boundaries

- Enable nullable reference types and built-in .NET analyzers.
- Prefer small immutable command, event, and value types at the gameplay boundary.
- Keep Godot types in the Godot project. Convert them at an adapter boundary when the core needs spatial values.
- Do not introduce an ECS, dependency-injection container, message broker, or generic quest framework for the POC.
- Keep test fixtures and development adapters out of production gameplay rules.

## Verification

The intended verification layers are:

1. Fast .NET tests for pure gameplay rules.
2. Deterministic CLI scenarios for commands, ticks, events, and snapshots.
3. Godot headless smoke tests for scene and engine integration.
4. A graphical playtest for input, navigation, camera, UI, animation, and readability.
5. Direct graphical inspection in the owning tool for every visual change.

For AI-assisted art, follow `docs/ART-PIPELINE.md`. Review 3D work directly in
Tripo, Blender, or Godot. Use at most one representative screenshot per
checkpoint when a frozen handoff image is useful; additional captures require a
named defect. Keep captures ignored under `artifacts/` and version only concise
decisions, provider IDs/settings, and structural metrics.

`scripts/dev.ps1` is the canonical command surface. Use its `help` command to see implemented subcommands, and never report a command as passing unless it was actually run.

When a Godot, .NET, Git, browser, network, cache, or installed-tool command fails in the sandbox, rerun the exact command outside it when permitted before classifying the failure as a project defect.

## Godot and MCP

Use the Godot 4.7.1 Mono console executable for automation. Resolve it from `SPACE_ADVENTURE_GODOT`, an explicit script argument, or a documented local candidate; do not rely on `godot` being on `PATH`.

The local `godot-ai-plugin` is an optional external development addon. It may inspect scenes and runtime state, inject real input, invoke explicit automation methods, and capture the viewport. Core work and all automated checks must work without it. Use `scripts/dev.ps1 plugin-link` in each worktree that needs it, then enable it locally in the Godot editor. Enabling it may add `[editor_plugins]` and `[autoload]` entries to `game/project.godot`; treat those as temporary local state and do not commit them. Never vendor the addon or recursively delete its junction.

Blender and its MCP are used only for an active art task. Do not start Blender for gameplay, documentation, or ordinary C# work.

Before asset production, read `docs/ART-PIPELINE.md`,
`docs/TRIPO-PRODUCTION-HANDOFF.md`, `docs/POC-ASSET-ROSTER.md`,
`docs/ATTACK-PRESENTATION.md`, the visual bible, active brief, and approved
reference provenance. Work only on approved assets in the dedicated art
worktree; live replacement and gameplay integration remain roadmap-scoped.

Humanoids use this sequence: unrigged T-pose generation, Tripo Smart Low-Poly
v2 Quad retopology with a 10,000 target, human-approved Mixamo marker placement,
Mixamo rig download with skin, Blender weight repair, then Mixamo library clips
without skin. Skeletal animation is limited to humanoids; non-humanoids must be
simple floating or stationary rigid assemblies.

Static props and environment assemblies do not use the humanoid sequence. Give
them brief-specific topology and material budgets, normalize them in Blender,
and review the exact static GLB in Godot. Preserve a validated inanimate source
and output until it is rejected or superseded; changes to character rigging or
animation are not a reason to regenerate it. Structural kits and collision-
critical modules are authored dimensionally in Blender. Simple machine motion
uses only a few rigid pivots or gameplay-driven transforms, never Mixamo or
skinning.

Use signed-in Tripo Studio without an API key or direct Godot bridge. Preserve
untouched exports in the ignored run-local cache and commit provider/version,
path, size, and provenance only. Do not content-hash large 3D binaries.

## Path portability

Follow `docs/PATH-CONVENTIONS.md` for every new file. Keep repository-relative
paths at or below 180 characters and run
`pwsh -NoProfile -File scripts/dev.ps1 path-check` before committing. Stable
asset IDs appear once in the directory hierarchy; compact run IDs and local
artifact filenames must not repeat them or contain provider task UUIDs. Do not
add a long-path exception for new work without explicit project-owner approval.
Git `core.longpaths`, short worktree roots, and local application success do
not waive the portable-path budget.

## Parallel work

Use one branch and Git worktree per implementation agent, with explicit subsystem or path ownership. Worktrunk may manage those worktrees, but raw Git worktrees remain supported.

Each worktree has its own generated `.godot` state, isolated Godot user-data root, running editor, logs, and distinct Godot MCP port. The addon normally chooses the first free port in its configured range; use explicit per-worktree ports only when deterministic routing is needed. Shared scenes, `project.godot`, command contracts, imported-asset registries, and binary assets have one integration owner at a time. Communicate contract changes before dependent work begins. See `docs/AGENT-AUTOMATION.md`.
