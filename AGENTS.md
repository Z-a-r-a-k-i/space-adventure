# SpaceAdventure agent instructions

Godot 4.7.1 .NET, C#, Windows. Work in the owner's existing repository folder
for the current solo repair, including art edits (ADR 0027).

## Read only what the task needs

Start with [README.md](README.md) and [ROADMAP.md](docs/ROADMAP.md).

| Work | Additional reading |
| --- | --- |
| Gameplay or scope | [Product](docs/PRODUCT.md), [Architecture](docs/ARCHITECTURE.md) |
| A consequential boundary change | Relevant entry in [Decisions](docs/DECISIONS.md) |
| Verification or reproduction | [Testing](docs/testing.md) |
| Animation or combat effects | [Attack presentation](docs/ATTACK-PRESENTATION.md) |
| Asset production | [Art pipeline](docs/ART-PIPELINE.md), roster entry, asset brief and its approved reference |
| New paths | [Path conventions](docs/PATH-CONVENTIONS.md) |

Future specs, historical reports, workstation setup, and unrelated asset
provenance are reference material, not default task context.

## Working rules

- Deliver the smallest playable slice in the active milestone. Do not pull
  deferred systems into it or restore the superseded GDScript spike.
- Game code is C#. GDScript is allowed only in external addons unless a new
  decision records an exception. Keep nullable types and .NET analyzers on.
- Rules belong to the pure C# core. Input and automation use the same typed
  commands; validate completely before mutation. Use stable gameplay IDs.
- Simulation advances by explicit fixed ticks. Pause stops gameplay while
  input, camera, UI, observation, and command entry stay responsive.
- Presentation reads state/events; it never resolves damage, advances rules,
  or supplies authoritative movement. Keep Godot types at the adapter boundary.
- The active route uses reviewed production presentation. Primitive collision,
  navigation, interaction, and lighting wrappers must remain invisible.
- Add dependencies only for a demonstrated need. Keep fixtures out of production
  rules; do not add an ECS, DI container, broker, or generic quest framework.
- Never commit or push without the user's explicit request.

## Verify and use tools

Use `scripts/dev.ps1 help` for the canonical command surface. Run the relevant
rule, CLI, engine, and graphical checks from [Testing](docs/testing.md). Never
report an unrun command as passing. Visual changes require direct inspection
in Godot or the owning art tool; headless results alone are insufficient.
Use one representative capture per checkpoint when useful; additional captures
need a named defect. Keep review media ignored under `artifacts/`.

Resolve the Godot Mono console from `SPACE_ADVENTURE_GODOT`, `-Godot`, or the
documented local candidate; do not depend on `godot` being on PATH. If a tool
fails in a sandbox, rerun the exact command outside it when permitted before
calling it a project defect. Do not start Blender for non-art tasks.

The external `godot-ai-plugin` is optional. Use `scripts/dev.ps1 plugin-link` when
needed; keep local `[editor_plugins]` and `[autoload]` entries out of commits.
Never vendor the addon or recursively delete its junction.

If parallel implementation is requested, use one branch/worktree per agent
with explicit ownership. Isolate `.godot`, user data, editors, logs, and MCP
ports (default range 6550–6569). Shared scenes, command contracts,
`project.godot`, and binary assets have one integration owner. One agent does
not require another worktree.

Before committing, run `pwsh -NoProfile -File scripts/dev.ps1 path-check` and
`git diff --cached --check`.

## Keep documentation small

Give each fact one home and link to it. Code/content own tunable values;
tests own executable assertions. Update decisions only when the reason or
scope changes. Keep completed work in Git/PR history and a compact milestone
record, not repeated narratives in active guides. Asset provenance retains
source identity, rights, accepted output, and essential reproduction data.
