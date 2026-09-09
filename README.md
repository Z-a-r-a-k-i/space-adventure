# SpaceAdventure

A single-player science-fiction party RPG built with Godot 4.7.1 .NET and C#.
The current local prototype joins a solo tactical-pause fight, Protector
recruitment, and a two-character arena. See the [roadmap](docs/ROADMAP.md) for
the current milestone and next gate.

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
| Left-click crew / portrait | Select a character |
| Left-drag around crew | Select a group; Shift-drag adds crew |
| Tab / Shift + Tab | Cycle ability focus forward/backward within the group; with one selected, cycle living crew |
| Shift + left-click crew / portrait | Add or remove a character from the selection |
| Space | Tactical pause; newest pending order replaces the previous one |
| 1, then left-click | Vanguard: aim Interrupt on the floor. Protector: place Barrier with one click, facing from him toward the placement point. Escape/right-click cancels |
| 2 | Vanguard: click an enemy for Burst. Protector: Taunt nearby enemies |
| X or Stop | Cancel orders; follows pause/readying rules |
| T, then left-click; Alt + right-click | Turn selected crew in place |
| Dialogue 1/2, Enter, or buttons | Choose a response; Enter chooses the first |
| Defeat Enter/keypad Enter or Retry | Restart the fight, preserving route progress |
| Mouse at window edge; WASD/arrows | Camera-relative pan; edge scrolling stops over HUD, during selection drags, or outside the active game window |
| Q/E or middle drag | Yaw; vertical middle drag changes pitch |
| Page Up/Down; wheel | Pitch; zoom |
| Home/R; F | Reset orientation; focus selected crew |

Stop is unavailable during dialogue, defeat, and victory securing. Combat
pauses when readying starts; resume to play the draw. Crew only attack assigned
targets. Basic fire resumes after an ability; movement, interaction, facing,
and Stop clear the target. Manual facing holds the heading; move or attack
orders release it. Move, face, attack, and Stop apply to the selected living
crew. Abilities belong to the focused portrait. The arena ends when every enemy or every crew
member is down; retry restores the encounter while preserving route progress.

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
