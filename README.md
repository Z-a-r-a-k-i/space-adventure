# SpaceAdventure

A single-player science-fiction party RPG built with Godot 4.7.1 .NET and C#.
The current prototype is a station route with a solo tactical-pause fight and
Protector recruitment. See the [roadmap](docs/ROADMAP.md) for the next gate.

## Run

Install PowerShell 7, Git/LFS, .NET SDK `8.0.319` (`global.json`), and Godot
`4.7.1.stable.mono`. From the repository root:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 doctor
pwsh -NoProfile -File scripts/dev.ps1 restore
pwsh -NoProfile -File scripts/dev.ps1 build
pwsh -NoProfile -File scripts/dev.ps1 import
pwsh -NoProfile -File scripts/dev.ps1 run
```

`dev.ps1` selects the pinned SDK for its process. Godot resolves from `-Godot`,
`SPACE_ADVENTURE_GODOT`, or the documented local candidate
`C:\Program Files\Godot_v4.7.1-stable_mono_win64`. Automation uses the Mono
console executable; an editor plugin is optional. `help` lists all commands.

## Controls

| Input | Action |
| --- | --- |
| Right-click floor / target | Move / interact / repeat basic attacks |
| Space | Tactical pause; newest pending order replaces the previous one |
| 1, then left-click | Suppressive Fire; Escape cancels targeting |
| 2 | Use the single Field Aid charge |
| X or Stop | Cancel orders and attack intent; follows pause/readying rules |
| Dialogue 1/2, Enter, or buttons | Choose a response; Enter chooses the first |
| Defeat Enter/keypad Enter or Retry | Restart the fight, preserving route progress |
| WASD/arrows | Camera-relative pan |
| Q/E or middle drag | Yaw; vertical middle drag changes pitch |
| Page Up/Down; wheel | Pitch; zoom |
| Home/R; F | Reset orientation; focus Vanguard |

Stop is unavailable during dialogue, defeat, and victory securing. Combat
pauses when readying starts; resume to play the draw. Basic fire resumes after
an ability or healing; movement, interaction, and Stop clear the target.

## Work on the prototype

- [Product](docs/PRODUCT.md): intended experience and POC limits.
- [Roadmap](docs/ROADMAP.md): current state and next work.
- [Testing](docs/testing.md): checks, graphical review, and manual playtest.
- [Architecture](docs/ARCHITECTURE.md): ownership and gameplay contracts.
- [Agent instructions](AGENTS.md): task-specific reading and working rules.
- [Art pipeline](docs/ART-PIPELINE.md): asset work only.

Rules live in `src/SpaceAdventure.Core/`; Godot adapters in `game/scripts/`;
tests in `tests/SpaceAdventure.Core.Tests/`; CLI scenarios in
`tools/SpaceAdventure.SimCli/`. Logs, captures, movies, and caches stay ignored
under `artifacts/` or each asset run's `raw/` directory.
