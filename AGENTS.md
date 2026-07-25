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

For AI-assisted art, optimize visual generation and inspection rather than the
user's chosen Codex model, reasoning effort, fast mode, or agent concurrency.
Follow the visual-efficiency policy in `docs/ART-PIPELINE.md`: generate one
useful concept or reference sheet by default and preserve approved artwork
that will actually feed asset production. Review 3D candidates, meshes,
materials, rigs, and animation directly in Tripo Studio, Blender, or Godot.
Do not create or commit Studio screenshots, viewport screenshots, render
turnarounds, contact sheets, animation-frame dumps, or gallery captures as
routine asset evidence. Any temporary diagnostic capture belongs under the
ignored `artifacts/` tree and is opened through model vision only for a named
problem that cannot be judged efficiently in the live tool.

Version textual decisions, defects, provider task or version IDs, settings,
and structural validation metrics. A `visual-review.md` may summarize a live
review decision, but it is not a screenshot inventory or image-hash ledger.
Do not ask multiple agents to repeat the same live review unless the asset
changed, a named unanswered question remains, or the project owner explicitly
requests an independent review.

`scripts/dev.ps1` is the canonical command surface. Use its `help` command to see implemented subcommands, and never report a command as passing unless it was actually run.

When a Godot, .NET, Git, browser, network, cache, or installed-tool command fails in the sandbox, rerun the exact command outside it when permitted before classifying the failure as a project defect.

## Godot and MCP

Use the Godot 4.7.1 Mono console executable for automation. Resolve it from `SPACE_ADVENTURE_GODOT`, an explicit script argument, or a documented local candidate; do not rely on `godot` being on `PATH`.

The local `godot-ai-plugin` is an optional external development addon. It may inspect scenes and runtime state, inject real input, invoke explicit automation methods, and capture the viewport. Core work and all automated checks must work without it. Use `scripts/dev.ps1 plugin-link` in each worktree that needs it, then enable it locally in the Godot editor. Enabling it may add `[editor_plugins]` and `[autoload]` entries to `game/project.godot`; treat those as temporary local state and do not commit them. Never vendor the addon or recursively delete its junction.

Blender and its MCP are used only for an active art task. Do not start Blender for gameplay, documentation, or ordinary C# work.

Before an active Tripo or asset-production task, read
`docs/ART-PIPELINE.md`, `docs/TRIPO-PRODUCTION-HANDOFF.md`,
`docs/POC-ASSET-ROSTER.md`, `docs/ATTACK-PRESENTATION.md`, the frontier-station
visual bible, the active asset brief, and the approved reference-sheet
provenance. Tripo Studio uses the signed-in browser subscription workflow; do
not introduce an API key or provider-to-Godot publication path. Preserve raw
outputs unchanged in the ignored run-local workstation cache, commit their
provider/task references, paths, sizes, and provenance, and do not content-hash
raw or other large 3D binary cache files. Tracked binary sources and outputs
use their normal Git/LFS revision identity without a second pipeline hash.
Complete mesh reconstruction before rigging, and treat generated rigs and
animations as Blender-retargeted donor inputs rather than production
authority. Tripo is a best-effort recovery source, not the project's durable
archive.

Offline source production is authorized independently of gameplay-phase
activation only for an approved roster asset with an approved reference,
accepted production-ready art brief, assigned ownership, and resolved
licensing and privacy prerequisites. Brief acceptance must record the project
owner or explicitly delegated art approver; authorship or assignment is not
self-approval. Use a dedicated art branch/worktree, owned asset IDs and paths,
isolated Godot user data and MCP port, and never touch the active gameplay
worktree, live scenes, `project.godot`, or shared registries during offline
review. Select a promising source early and use subscription credits
productively on completion, topology, textures, rig diagnostics, and animation
donors; do not stop merely at the first viable candidate or generate variants
without a named purpose. Record the provider operation, task or version ID,
result, and keep/reject decision. A displayed operation cost may be captured
opportunistically, but do not track account balances, reconcile historical
credit totals, or use cost as an acceptance criterion. Live greybox
replacement, gameplay wiring, ability-specific work, and final attack-timing
integration remain roadmap-phase scoped.
The bounded provider bake-off keeps its separate hard caps and scorecard and
must be frozen before production work begins for any of its three asset IDs.

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
