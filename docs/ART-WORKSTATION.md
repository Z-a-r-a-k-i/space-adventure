# Dedicated Windows art workstation

Status: approved machine-bootstrap checklist

Revision: 2026-07-23

## Purpose

This checklist reproduces the development, Tripo, Blender, publication, and
Godot-review environment on a dedicated Windows x64 machine. It distinguishes
project requirements from moving desktop tools and records known-good external
control revisions without making either MCP a shipped-game dependency.

Completing this setup or connecting its tools does not authorize asset
generation. `ROADMAP.md` and `POC-ASSET-ROSTER.md` govern the current executable
scope. After setup, the generation agent follows
`TRIPO-PRODUCTION-HANDOFF.md` for the signed-in Studio, Blender, animation, and
Godot-review procedure.

`godot-ai-plugin` is the **Godot AI Control** integration used by this project.
Do not install a second Godot-control addon.

## Required build and test baseline

| Tool | Version policy | Verification |
|---|---|---|
| Windows x64 | Current supported release and graphics driver | Graphical desktop session available |
| [Git for Windows](https://git-scm.com/install/windows.html) | Current stable; patch not project-pinned | `git --version` |
| [PowerShell](https://learn.microsoft.com/powershell/scripting/install/install-powershell-on-windows) | 7.x; patch not project-pinned | `pwsh --version` |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | `8.0.319` exactly | `dotnet --version` |
| [Godot Mono](https://godotengine.org/download/archive/) | `4.7.1.stable.mono.official` | Console `--version` starts with that value |

The default Godot console path is:

```text
C:\Program Files\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe
```

Another location is valid through `SPACE_ADVENTURE_GODOT` or the canonical
script's `-Godot` argument. The standard non-Mono Godot build is not suitable
for this C# project.

## Required agent-driven art tools

| Tool or service | Version policy | Role |
|---|---|---|
| [Blender](https://www.blender.org/download/) | `5.2` LTS line | Editable source, cleanup, retopology, UVs, shared rigs, animation, markers, controlled rendering, and GLB export |
| Codex desktop | Current | Agent session and tool coordination |
| [Chrome](https://www.google.com/chrome/) plus Codex Chrome control | Current | Signed-in Tripo Studio operation and browser evidence |
| [Tripo Studio](https://www.tripo3d.ai/) account | Live browser service | Candidate generation; no API installation or repository secret is required |
| [`uv`](https://docs.astral.sh/uv/getting-started/installation/) | Current stable | Provides the Blender MCP Python environment; a separate global Python installation is unnecessary |
| Official Blender MCP | Package `1.0.0`; known-good commit `03004fd0216bfe5e0a3d9ac9b47d5efadc3d78c4` | Interactive Blender inspection and control |
| Existing `godot-ai-plugin` | Known-good commit below | Godot AI Control, graphical inspection, input, runtime state, and viewport capture |
| [Node.js](https://nodejs.org/en/download/) | `22` or newer | Runs the Godot MCP server |
| pnpm | `10.30.0` | Reproducible Godot MCP source build |

Known-good Godot plugin checkout:

```text
repository: git@github.com:Z-a-r-a-k-i/godot-ai-plugin.git
branch: feat/godot-4-7-1-multi-editor-routing
commit: c26e4fb6582213324ef367760ba4414176ee472a
```

The official Blender MCP source is:

```text
https://projects.blender.org/lab/blender_mcp.git
```

In Blender, open Preferences → Get Extensions, choose **Install from Disk**, and
install a ZIP whose archive root contains the files from
`addon/blender_mcp_addon`. Enable the **MCP** extension, keep its host on the
local machine, and start its server or enable its local auto-start option. Run
the matching MCP process through `uv`; do not expose either socket as a public
network service.

For the Godot integration, build the pinned source checkout when needed, then
link its `addons/godot_ai` directory:

```powershell
npm install --global pnpm@10.30.0

$GodotAiRepository = "D:\Tools\godot-ai-plugin"
$SpaceAdventureRepository = "D:\Work\space-adventure"

Set-Location -LiteralPath (Join-Path $GodotAiRepository "mcp-server")
pnpm install --frozen-lockfile
pnpm bundle

Set-Location -LiteralPath $SpaceAdventureRepository
pwsh -NoProfile -File scripts/dev.ps1 plugin-link `
  -GodotAiPlugin (Join-Path $GodotAiRepository "addons\godot_ai")
```

Replace the example paths above with the local paths. Open the Godot editor and
enable **Godot AI Control** in Project Settings → Plugins. A released standalone
Godot MCP bundle needs Node.js but does not need pnpm at runtime.

## Register the MCP servers with Codex

Merge entries like the following into `%USERPROFILE%\.codex\config.toml` after
replacing the example paths. Do not overwrite unrelated Codex configuration.

```toml
[mcp_servers.godot]
command = "node"
args = ['D:\Tools\godot-ai-plugin\mcp-server\dist\godot-mcp.js']

[mcp_servers.godot.env]
GODOT_AI_TOOL_PROFILE = "workbench"

[mcp_servers.blender]
command = "uv"
args = ["--directory", 'D:\Tools\blender_mcp\mcp', "run", "blender-mcp"]
```

Restart Codex after changing the configuration. With Godot and Blender open and
their local extensions enabled, confirm that Codex exposes both tool sets and
can inspect each editor before beginning an art task.

## Install before normalizing or publishing assets

| Tool | Version policy | Role |
|---|---|---|
| [Git LFS client](https://git-lfs.com/) | Current stable | Available for accepted large binary sources; tracking is enabled only when repository size demonstrates the need |
| [Khronos glTF Validator](https://github.com/KhronosGroup/glTF-Validator/releases) | Current stable | Structural GLB validation |
| [glTF Transform CLI](https://gltf-transform.dev/cli) | Current stable | Inspection, reporting, optimization, and controlled publication transforms |
| [ImageMagick](https://imagemagick.org/script/download.php#windows) | Version 7, Q16-HDRI Windows build | Labels, contact sheets, and visual comparisons |
| [FFmpeg](https://ffmpeg.org/download.html) | Current stable Windows build | Turntables and animation previews |

Install the glTF Transform CLI after Node:

```powershell
npm install --global @gltf-transform/cli
```

These tools are not prerequisites for C# bootstrap or primitive greyboxing.
They are required on this machine before it begins normalizing, reviewing, or
publishing generated candidates.

## Optional conveniences

- GitHub CLI for private-repository authentication and remote operations.
- Worktrunk for convenient parallel Git worktrees.
- Computer Use as a fallback for desktop interactions that Chrome control
  cannot reach.
- An IDE or text editor.
- OpenDesign for reference-sheet preparation.
- Meshy only when a defined bake-off requests a comparison candidate.

Docker, Visual Studio, standalone Python, Cheat Engine, Ghidra, Substance
Painter, Photoshop, Mixamo, and Tripo API credits are not required by the
current pipeline.

## Verification checklist

Run the applicable commands after installation:

```powershell
git --version
git lfs version
pwsh --version
dotnet --version

& "C:\Program Files\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" --version
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --version

node --version
pnpm --version
uv --version
gltf-transform --version
Get-Command gltf_validator -ErrorAction Stop
magick -version
ffmpeg -version
```

Record the exact local paths to the two external repositories, then verify the
known-good revisions:

```powershell
$GodotAiRepository = "D:\Tools\godot-ai-plugin"
$BlenderMcpRepository = "D:\Tools\blender_mcp"

$ExpectedGodotAiCommit = "c26e4fb6582213324ef367760ba4414176ee472a"
$ExpectedBlenderMcpCommit = "03004fd0216bfe5e0a3d9ac9b47d5efadc3d78c4"

if ((git -C $GodotAiRepository rev-parse HEAD).Trim() -ne
    $ExpectedGodotAiCommit) {
    throw "Godot AI plugin revision does not match the documented pin."
}

if ((git -C $BlenderMcpRepository rev-parse HEAD).Trim() -ne
    $ExpectedBlenderMcpCommit) {
    throw "Blender MCP revision does not match the documented pin."
}

uv --directory "$BlenderMcpRepository\mcp" sync --frozen
uv --directory "$BlenderMcpRepository\mcp" run blender-mcp --help
```

Replace the example `D:\Tools` paths with the actual installation paths. From
the SpaceAdventure repository root, verify the complete project:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 doctor
pwsh -NoProfile -File scripts/dev.ps1 restore
pwsh -NoProfile -File scripts/dev.ps1 build
pwsh -NoProfile -File scripts/dev.ps1 test
pwsh -NoProfile -File scripts/dev.ps1 scenario -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 import
pwsh -NoProfile -File scripts/dev.ps1 headless -Name station-route
pwsh -NoProfile -File scripts/dev.ps1 capture -Name wall-cutaway
```

The final capture still requires visual inspection. Then link and enable Godot
AI Control locally, verify its doctor/connection surface, and confirm Blender
MCP can inspect a running Blender scene.

## Version and repository hygiene

Project-pin .NET, Godot, Blender, pnpm, and the known-good external MCP
revisions above. Do not turn moving Git, PowerShell, Node, `uv`, Chrome, Codex,
ImageMagick, or FFmpeg patch versions into project requirements. Record their
actual versions in each accepted asset's provenance.

Do not commit:

- the `game/addons/godot_ai` junction;
- plugin-created `[editor_plugins]` or `[autoload]` changes;
- `.godot`, `artifacts`, `node_modules`, `.blend1`, or `.blend2`;
- absolute machine configuration, browser or Codex profiles, cookies, Tripo
  credentials, or API keys; or
- Git LFS tracking rules before actual accepted binary sizes justify them.
