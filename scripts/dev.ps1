[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("help", "doctor", "path-check", "restore", "build", "test", "scenario", "plugin-link", "import", "headless", "capture", "editor", "run")]
    [string]$Command = "help",

    [string]$Godot,

    [string]$GodotAiPlugin,

    [string]$Name = "bootstrap",

    [ValidateRange(0, 3600)]
    [int]$AutoQuitSeconds = 0,

    [ValidateRange(1, 600)]
    [int]$TimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "SpaceAdventure.sln"
$gameProject = Join-Path $repoRoot "game"
$simProject = Join-Path $repoRoot "tools\SpaceAdventure.SimCli\SpaceAdventure.SimCli.csproj"
$userDataRoot = Join-Path $repoRoot "artifacts\godot-user"
$visualCaptureRoot = Join-Path $repoRoot "artifacts\visual\captures"
$pluginLink = Join-Path $gameProject "addons\godot_ai"

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,

        [Parameter(Mandatory)]
        [string]$Description
    )

    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Resolve-GodotConsole {
    $candidates = [System.Collections.Generic.List[string]]::new()

    foreach ($configured in @($Godot, $env:SPACE_ADVENTURE_GODOT)) {
        if ([string]::IsNullOrWhiteSpace($configured)) {
            continue
        }

        if (Test-Path -LiteralPath $configured -PathType Container) {
            $candidates.Add((Join-Path $configured "Godot_v4.7.1-stable_mono_win64_console.exe"))
        }
        else {
            $candidates.Add($configured)
        }
    }

    $candidates.Add("C:\Program Files\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe")

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Godot 4.7.1 Mono console executable was not found. Pass -Godot or set SPACE_ADVENTURE_GODOT."
}

function Resolve-GodotAiPluginSource {
    $candidates = [System.Collections.Generic.List[string]]::new()

    foreach ($configured in @($GodotAiPlugin, $env:SPACE_ADVENTURE_GODOT_AI_PLUGIN)) {
        if (-not [string]::IsNullOrWhiteSpace($configured)) {
            $candidates.Add($configured)
        }
    }

    $candidates.Add((Join-Path $repoRoot "..\..\godot-ai-plugin\addons\godot_ai"))

    foreach ($candidate in $candidates) {
        $pluginConfig = Join-Path $candidate "plugin.cfg"
        if (Test-Path -LiteralPath $pluginConfig -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Godot AI Control source was not found. Pass -GodotAiPlugin or set SPACE_ADVENTURE_GODOT_AI_PLUGIN to its addons/godot_ai directory."
}

function Invoke-Godot {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $godotConsole = Resolve-GodotConsole
    $previousAppData = $env:APPDATA
    $previousLocalAppData = $env:LOCALAPPDATA
    $isolatedAppData = Join-Path $userDataRoot "AppData\Roaming"
    $isolatedLocalAppData = Join-Path $userDataRoot "AppData\Local"
    New-Item -ItemType Directory -Force -Path $isolatedAppData, $isolatedLocalAppData | Out-Null

    try {
        $env:APPDATA = $isolatedAppData
        $env:LOCALAPPDATA = $isolatedLocalAppData
        Invoke-Checked -Description "Godot" -Action { & $godotConsole @Arguments }
    }
    finally {
        $env:APPDATA = $previousAppData
        $env:LOCALAPPDATA = $previousLocalAppData
    }
}

function Invoke-GodotAutomated {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [string]$UserDataScope
    )

    $godotConsole = Resolve-GodotConsole
    $isolatedUserDataRoot = $userDataRoot
    if ($PSBoundParameters.ContainsKey("UserDataScope")) {
        if ([string]::IsNullOrWhiteSpace($UserDataScope) -or $UserDataScope -notmatch '^[a-z0-9][a-z0-9._-]{0,63}$') {
            throw "Godot user-data scope must contain 1-64 lowercase letters, digits, dots, underscores, or hyphens and must begin with a letter or digit."
        }

        $scopeParent = Join-Path $userDataRoot "scopes"
        $isolatedUserDataRoot = Join-Path $scopeParent $UserDataScope
        $resolvedScopeParent = [System.IO.Path]::GetFullPath($scopeParent).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $resolvedScope = [System.IO.Path]::GetFullPath($isolatedUserDataRoot)
        if (-not $resolvedScope.StartsWith($resolvedScopeParent, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Godot user-data scope resolved outside the scoped artifact directory."
        }

        foreach ($scopePath in @($userDataRoot, $scopeParent, $resolvedScope)) {
            Assert-NotReparsePoint -Path $scopePath -Description "Godot user-data scope"
        }
    }

    $isolatedAppData = Join-Path $isolatedUserDataRoot "AppData\Roaming"
    $isolatedLocalAppData = Join-Path $isolatedUserDataRoot "AppData\Local"
    New-Item -ItemType Directory -Force -Path $isolatedAppData, $isolatedLocalAppData | Out-Null
    if ($PSBoundParameters.ContainsKey("UserDataScope")) {
        Assert-NotReparsePoint -Path $isolatedUserDataRoot -Description "Godot user-data scope"
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $godotConsole
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment["APPDATA"] = $isolatedAppData
    $startInfo.Environment["LOCALAPPDATA"] = $isolatedLocalAppData
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Godot failed to start."
        }

        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            $process.Kill($true)
            $process.WaitForExit()
        }

        $output = $standardOutput.GetAwaiter().GetResult()
        $errorOutput = $standardError.GetAwaiter().GetResult()
        if (-not [string]::IsNullOrWhiteSpace($output)) {
            Write-Output $output.TrimEnd()
        }
        if (-not [string]::IsNullOrWhiteSpace($errorOutput)) {
            Write-Error $errorOutput.TrimEnd()
        }
        if ($timedOut) {
            throw "Godot timed out after $TimeoutSeconds seconds and was terminated."
        }
        if ($process.ExitCode -ne 0) {
            throw "Godot failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-GodotAiPortSetting {
    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_AI_PORT)) {
        $configuredPort = 0
        if (-not [int]::TryParse($env:GODOT_AI_PORT, [ref]$configuredPort) -or $configuredPort -lt 1024 -or $configuredPort -gt 65535) {
            throw "GODOT_AI_PORT must be an integer between 1024 and 65535."
        }
        return "Exact port $configuredPort"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_AI_PORT_RANGE)) {
        $match = [regex]::Match($env:GODOT_AI_PORT_RANGE.Trim(), '^(\d+)-(\d+)$')
        if (-not $match.Success) {
            throw "GODOT_AI_PORT_RANGE must use start-end syntax."
        }

        $rangeStart = [int]$match.Groups[1].Value
        $rangeEnd = [int]$match.Groups[2].Value
        if ($rangeStart -lt 1024 -or $rangeEnd -gt 65535 -or $rangeStart -gt $rangeEnd -or ($rangeEnd - $rangeStart + 1) -gt 256) {
            throw "GODOT_AI_PORT_RANGE must contain at most 256 ports between 1024 and 65535."
        }

        return "First free port in $rangeStart-$rangeEnd"
    }

    return "First free port in 6550-6569"
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory)][string]$Name)

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' is not available."
    }
}

function Assert-NotReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Description '$Path' is a reparse point and was left untouched."
    }
}

function Invoke-RepositoryPathCheck {
    $portableRelativePathLimit = 180
    $normalWindowsAbsolutePathLimit = 259
    $exceptionsPath = Join-Path $PSScriptRoot "path-length-exceptions.txt"
    $exceptionSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    if (Test-Path -LiteralPath $exceptionsPath -PathType Leaf) {
        foreach ($line in Get-Content -LiteralPath $exceptionsPath) {
            $entry = $line.Trim()
            if ($entry.Length -eq 0 -or $entry.StartsWith("#", [System.StringComparison]::Ordinal)) {
                continue
            }

            [void]$exceptionSet.Add($entry.Replace("\", "/"))
        }
    }

    $repositoryPaths = @(& git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed with exit code $LASTEXITCODE."
    }

    $violations = @()
    $grandfathered = @()
    foreach ($repositoryPath in $repositoryPaths) {
        if ([string]::IsNullOrWhiteSpace($repositoryPath)) {
            continue
        }

        $portablePath = $repositoryPath.Replace("\", "/")
        $absolutePath = Join-Path $repoRoot $portablePath
        $relativeLength = $portablePath.Length
        $absoluteLength = $absolutePath.Length

        if ($relativeLength -le $portableRelativePathLimit -and
            $absoluteLength -le $normalWindowsAbsolutePathLimit) {
            continue
        }

        $finding = [pscustomobject]@{
            RelativeLength = $relativeLength
            AbsoluteLength = $absoluteLength
            Path = $portablePath
        }
        if ($exceptionSet.Contains($portablePath)) {
            $grandfathered += $finding
        }
        else {
            $violations += $finding
        }
    }

    foreach ($finding in $grandfathered) {
        Write-Warning "Grandfathered long path ($($finding.RelativeLength) relative, $($finding.AbsoluteLength) absolute): $($finding.Path)"
    }

    if ($violations.Count -gt 0) {
        $table = $violations |
            Sort-Object RelativeLength -Descending |
            Format-Table RelativeLength, AbsoluteLength, Path -AutoSize |
            Out-String -Width 4096
        Write-Output $table.TrimEnd()
        throw "Repository path budget exceeded. Keep repository-relative paths at or below $portableRelativePathLimit characters and normal Windows absolute paths at or below $normalWindowsAbsolutePathLimit characters."
    }

    Write-Output "Path length check passed for $($repositoryPaths.Count) tracked and unignored paths."
    Write-Output "Budget: relative <= $portableRelativePathLimit; normal Windows absolute <= $normalWindowsAbsolutePathLimit."
    if ($grandfathered.Count -gt 0) {
        Write-Output "Grandfathered historical paths: $($grandfathered.Count). Do not add new exceptions."
    }
}

Push-Location $repoRoot
try {
    switch ($Command) {
        "help" {
            @"
SpaceAdventure development commands

  doctor                  Report required tool and project paths
  path-check              Enforce portable repository path-length budgets
  restore                 Restore .NET dependencies
  build                   Build the complete solution
  test                    Run pure core tests
  scenario [-Name name]   Run a deterministic core scenario as JSON Lines
  plugin-link             Create the ignored local Godot AI Control junction
  import                  Import the Godot project headlessly
  headless [-Name name]   Run a bounded Godot smoke (bootstrap, station-route, station-combat-defeat, humanoid-gallery, or hostile-gallery)
  capture -Name name      Create and verify a deterministic graphical capture (wall-cutaway)
  editor                  Open the Godot editor with the project
  run                     Launch the graphical bootstrap

Options:
  -Godot <path>            Godot console executable or installation directory
  -GodotAiPlugin <path>    External addons/godot_ai directory
  -AutoQuitSeconds <n>     Close a graphical run after n seconds
  -TimeoutSeconds <n>      Automated Godot timeout (default: 60)
"@ | Write-Output
        }
        "doctor" {
            Assert-CommandAvailable "dotnet"
            Assert-CommandAvailable "git"
            Assert-CommandAvailable "pwsh"
            $godotConsole = Resolve-GodotConsole
            $dotnetVersion = & dotnet --version
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet --version failed with exit code $LASTEXITCODE."
            }

            $godotVersion = & $godotConsole --version
            if ($LASTEXITCODE -ne 0) {
                throw "Godot --version failed with exit code $LASTEXITCODE."
            }
            if ($dotnetVersion -ne "8.0.319") {
                throw "Expected .NET SDK 8.0.319 from global.json, found '$dotnetVersion'."
            }
            if (-not $godotVersion.StartsWith("4.7.1.stable.mono.official", [System.StringComparison]::Ordinal)) {
                throw "Expected Godot 4.7.1 stable Mono, found '$godotVersion'."
            }

            $requiredProjectFiles = @(
                $solution,
                (Join-Path $repoRoot "global.json"),
                (Join-Path $repoRoot "src\SpaceAdventure.Core\SpaceAdventure.Core.csproj"),
                (Join-Path $repoRoot "tests\SpaceAdventure.Core.Tests\SpaceAdventure.Core.Tests.csproj"),
                $simProject,
                (Join-Path $gameProject "SpaceAdventure.Game.csproj"),
                (Join-Path $gameProject "project.godot")
            )
            $missingProjectFiles = @($requiredProjectFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
            if ($missingProjectFiles.Count -gt 0) {
                throw "Required project files are missing: $($missingProjectFiles -join ', ')"
            }

            [pscustomobject]@{
                Repository = $repoRoot
                DotNet = $dotnetVersion
                PowerShell = $PSVersionTable.PSVersion.ToString()
                Git = (& git --version)
                Godot = $godotVersion
                GodotPath = $godotConsole
                GodotAiPort = Get-GodotAiPortSetting
                GodotAiPluginLinked = (Test-Path -LiteralPath (Join-Path $pluginLink "plugin.cfg") -PathType Leaf)
                RequiredProjectFiles = $requiredProjectFiles.Count
            } | Format-List
        }
        "path-check" {
            Assert-CommandAvailable "git"
            Invoke-RepositoryPathCheck
        }
        "restore" {
            Invoke-Checked -Description ".NET restore" -Action { dotnet restore $solution --nologo }
        }
        "build" {
            Invoke-Checked -Description ".NET build" -Action { dotnet build $solution --nologo }
        }
        "test" {
            Invoke-Checked -Description ".NET test" -Action { dotnet test $solution --nologo }
        }
        "scenario" {
            Invoke-Checked -Description "Simulation scenario" -Action {
                dotnet run --project $simProject --configuration Debug -- $Name
            }
        }
        "plugin-link" {
            $source = Resolve-GodotAiPluginSource
            if (Test-Path -LiteralPath $pluginLink) {
                $existing = Get-Item -LiteralPath $pluginLink -Force
                if ($existing.LinkType -ne "Junction") {
                    throw "'$pluginLink' already exists and is not a junction. It was left untouched."
                }

                $actualTarget = [System.IO.Path]::GetFullPath([string]$existing.Target)
                if (-not $actualTarget.Equals($source, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "'$pluginLink' points to '$actualTarget', not '$source'. It was left untouched."
                }

                Write-Output "Godot AI Control junction already points to $source"
                break
            }

            $addonsDirectory = Split-Path -Parent $pluginLink
            New-Item -ItemType Directory -Force -Path $addonsDirectory | Out-Null
            New-Item -ItemType Junction -Path $pluginLink -Target $source | Out-Null
            Write-Output "Linked $pluginLink -> $source"
        }
        "import" {
            Invoke-GodotAutomated -Arguments @("--headless", "--import", "--path", $gameProject)
        }
        "headless" {
            $smoke = switch ($Name) {
                "bootstrap" { @{ Argument = "--bootstrap-smoke"; Scene = $null } }
                "station-route" { @{ Argument = "--station-route-smoke"; Scene = $null } }
                "station-combat-defeat" { @{ Argument = "--station-combat-defeat-smoke"; Scene = $null } }
                "humanoid-gallery" {
                    @{
                        Argument = "--humanoid-gallery-smoke"
                        Scene = "res://scenes/humanoid_gallery.tscn"
                    }
                }
                "hostile-gallery" {
                    @{
                        Argument = "--hostile-gallery-smoke"
                        Scene = "res://scenes/hostile_gallery.tscn"
                    }
                }
                default { throw "Unknown Godot headless scenario '$Name'. Expected 'bootstrap', 'station-route', 'station-combat-defeat', 'humanoid-gallery', or 'hostile-gallery'." }
            }
            $arguments = @("--headless", "--path", $gameProject)
            if ($null -ne $smoke.Scene) {
                $arguments += $smoke.Scene
            }
            $arguments += "--", $smoke.Argument
            Invoke-GodotAutomated -Arguments $arguments
        }
        "capture" {
            if ($Name -ne "wall-cutaway") {
                throw "Unknown visual capture '$Name'. Expected 'wall-cutaway'."
            }

            $expectedVisualCaptureRoot = [System.IO.Path]::GetFullPath(
                (Join-Path $repoRoot "artifacts\visual\captures"))
            $canonicalVisualCaptureRoot = [System.IO.Path]::GetFullPath($visualCaptureRoot)
            if (-not $canonicalVisualCaptureRoot.Equals(
                    $expectedVisualCaptureRoot,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Visual capture root resolved outside the fixed repository artifact directory."
            }

            $captureAncestors = @(
                (Join-Path $repoRoot "artifacts"),
                (Join-Path $repoRoot "artifacts\visual"),
                $canonicalVisualCaptureRoot
            )
            foreach ($captureAncestor in $captureAncestors) {
                Assert-NotReparsePoint -Path $captureAncestor -Description "Visual capture directory"
            }
            New-Item -ItemType Directory -Force -Path $canonicalVisualCaptureRoot | Out-Null
            Assert-NotReparsePoint -Path $canonicalVisualCaptureRoot -Description "Visual capture directory"
            $capturePng = Join-Path $canonicalVisualCaptureRoot "wall-cutaway.png"
            $captureManifest = Join-Path $canonicalVisualCaptureRoot "wall-cutaway.json"
            foreach ($staleOutput in @($capturePng, $captureManifest)) {
                if (Test-Path -LiteralPath $staleOutput) {
                    Assert-NotReparsePoint -Path $staleOutput -Description "Expected capture output"
                    if (-not (Test-Path -LiteralPath $staleOutput -PathType Leaf)) {
                        throw "Expected capture output '$staleOutput' is not a file and was left untouched."
                    }
                    Remove-Item -LiteralPath $staleOutput -Force
                }
            }

            Invoke-GodotAutomated -UserDataScope "capture-wall-cutaway" -Arguments @(
                "--path", $gameProject,
                "--windowed",
                "--resolution", "1280x720",
                "--",
                "--visual-capture=wall-cutaway"
            )

            foreach ($expectedOutput in @($capturePng, $captureManifest)) {
                if (-not (Test-Path -LiteralPath $expectedOutput -PathType Leaf)) {
                    throw "Godot reported success without creating expected capture output '$expectedOutput'."
                }
                Assert-NotReparsePoint -Path $expectedOutput -Description "Generated capture output"
            }

            $manifest = Get-Content -LiteralPath $captureManifest -Raw | ConvertFrom-Json -AsHashtable
            if ($manifest -isnot [System.Collections.IDictionary]) {
                throw "Visual capture manifest root must be a JSON object."
            }
            foreach ($requiredKey in @(
                    "schema_version",
                    "capture_id",
                    "passed",
                    "cutaway",
                    "cutaway_lifecycle",
                    "image")) {
                if ($manifest.Keys -cnotcontains $requiredKey) {
                    throw "Visual capture manifest is missing required property '$requiredKey'."
                }
            }
            if ($manifest["schema_version"] -ne 1) {
                throw "Expected visual capture schema_version 1, found '$($manifest["schema_version"])'."
            }
            if ($manifest["capture_id"] -cne "wall-cutaway") {
                throw "Expected capture_id 'wall-cutaway', found '$($manifest["capture_id"])'."
            }
            if ($manifest["passed"] -isnot [bool] -or -not $manifest["passed"]) {
                throw "Visual capture manifest did not report passed=true."
            }

            $expectedOccluderId = "presentation.wall.start.west"
            $cutaway = $manifest["cutaway"]
            if ($cutaway -isnot [System.Collections.IDictionary]) {
                throw "Visual capture manifest cutaway property must be an object."
            }
            foreach ($requiredCutawayKey in @("desired_cutaway_ids", "all_settled")) {
                if ($cutaway.Keys -cnotcontains $requiredCutawayKey) {
                    throw "Visual capture manifest cutaway is missing required property '$requiredCutawayKey'."
                }
            }
            $finalDesiredIds = @($cutaway["desired_cutaway_ids"])
            if ($finalDesiredIds.Count -ne 1 -or $finalDesiredIds[0] -cne $expectedOccluderId) {
                throw "Final capture cutaway must contain only '$expectedOccluderId'."
            }
            if ($cutaway["all_settled"] -isnot [bool] -or -not $cutaway["all_settled"]) {
                throw "Final capture cutaway did not report all_settled=true."
            }

            $lifecycle = $manifest["cutaway_lifecycle"]
            if ($lifecycle -isnot [System.Collections.IDictionary]) {
                throw "Visual capture manifest cutaway_lifecycle property must be an object."
            }
            foreach ($requiredLifecycleKey in @(
                    "schema_version",
                    "algorithm",
                    "maximum_process_frames_per_phase",
                    "gameplay_remained_paused",
                    "phases")) {
                if ($lifecycle.Keys -cnotcontains $requiredLifecycleKey) {
                    throw "Visual capture lifecycle is missing required property '$requiredLifecycleKey'."
                }
            }
            if ($lifecycle["schema_version"] -ne 1) {
                throw "Expected visual capture lifecycle schema_version 1, found '$($lifecycle["schema_version"])'."
            }
            if ($lifecycle["algorithm"] -cne "process_frame_animation_v1") {
                throw "Visual capture lifecycle algorithm is unsupported."
            }
            if ($lifecycle["maximum_process_frames_per_phase"] -ne 600) {
                throw "Expected maximum_process_frames_per_phase 600, found '$($lifecycle["maximum_process_frames_per_phase"])'."
            }
            if ($lifecycle["gameplay_remained_paused"] -isnot [bool] -or -not $lifecycle["gameplay_remained_paused"]) {
                throw "Visual capture lifecycle did not report gameplay_remained_paused=true."
            }

            $lifecyclePhases = @($lifecycle["phases"])
            $expectedLifecyclePhases = @(
                [pscustomobject]@{ PhaseId = "initial_cut"; YawRadians = -[Math]::PI / 2.0; DesiredIds = @($expectedOccluderId) },
                [pscustomobject]@{ PhaseId = "clear_view_restore"; YawRadians = [Math]::PI / 2.0; DesiredIds = @() },
                [pscustomobject]@{ PhaseId = "recut"; YawRadians = -[Math]::PI / 2.0; DesiredIds = @($expectedOccluderId) }
            )
            if ($lifecyclePhases.Count -ne $expectedLifecyclePhases.Count) {
                throw "Visual capture lifecycle must contain exactly three phases."
            }
            for ($phaseIndex = 0; $phaseIndex -lt $expectedLifecyclePhases.Count; $phaseIndex++) {
                $phase = $lifecyclePhases[$phaseIndex]
                $expectedPhase = $expectedLifecyclePhases[$phaseIndex]
                if ($phase -isnot [System.Collections.IDictionary]) {
                    throw "Visual capture lifecycle phase $phaseIndex must be an object."
                }
                foreach ($requiredPhaseKey in @(
                        "phase_id",
                        "yaw_radians",
                        "expected_desired_cutaway_ids",
                        "process_frames_waited",
                        "before",
                        "after")) {
                    if ($phase.Keys -cnotcontains $requiredPhaseKey) {
                        throw "Visual capture lifecycle phase $phaseIndex is missing required property '$requiredPhaseKey'."
                    }
                }
                if ($phase["phase_id"] -cne $expectedPhase.PhaseId) {
                    throw "Visual capture lifecycle phase $phaseIndex expected phase_id '$($expectedPhase.PhaseId)', found '$($phase["phase_id"])'."
                }
                if ($phase["yaw_radians"] -isnot [double] -or [Math]::Abs($phase["yaw_radians"] - $expectedPhase.YawRadians) -gt 0.0001) {
                    throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' has an unexpected yaw_radians value."
                }
                $processFramesWaited = $phase["process_frames_waited"]
                if (($processFramesWaited -isnot [int] -and $processFramesWaited -isnot [long]) -or
                    $processFramesWaited -lt 1 -or
                    $processFramesWaited -gt 600) {
                    throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' has an invalid process_frames_waited value."
                }

                $declaredDesiredIds = @($phase["expected_desired_cutaway_ids"])
                if ($declaredDesiredIds.Count -ne $expectedPhase.DesiredIds.Count) {
                    throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' declares the wrong desired wall count."
                }
                for ($idIndex = 0; $idIndex -lt $expectedPhase.DesiredIds.Count; $idIndex++) {
                    if ($declaredDesiredIds[$idIndex] -cne $expectedPhase.DesiredIds[$idIndex]) {
                        throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' declares an unexpected desired wall."
                    }
                }

                foreach ($observationName in @("before", "after")) {
                    $observation = $phase[$observationName]
                    if ($observation -isnot [System.Collections.IDictionary]) {
                        throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' $observationName observation must be an object."
                    }
                    foreach ($requiredObservationKey in @("desired_cutaway_ids", "all_settled")) {
                        if ($observation.Keys -cnotcontains $requiredObservationKey) {
                            throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' $observationName observation is missing required property '$requiredObservationKey'."
                        }
                    }

                    $actualDesiredIds = @($observation["desired_cutaway_ids"])
                    if ($actualDesiredIds.Count -ne $expectedPhase.DesiredIds.Count) {
                        throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' $observationName observation has the wrong desired wall count."
                    }
                    for ($idIndex = 0; $idIndex -lt $expectedPhase.DesiredIds.Count; $idIndex++) {
                        if ($actualDesiredIds[$idIndex] -cne $expectedPhase.DesiredIds[$idIndex]) {
                            throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' $observationName observation has an unexpected desired wall."
                        }
                    }
                }

                $beforeSettled = $phase["before"]["all_settled"]
                if ($beforeSettled -isnot [bool] -or $beforeSettled) {
                    throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' did not begin with all_settled=false."
                }
                $afterSettled = $phase["after"]["all_settled"]
                if ($afterSettled -isnot [bool] -or -not $afterSettled) {
                    throw "Visual capture lifecycle phase '$($expectedPhase.PhaseId)' did not finish with all_settled=true."
                }
            }

            $image = $manifest["image"]
            if ($image -isnot [System.Collections.IDictionary]) {
                throw "Visual capture manifest image property must be an object."
            }
            foreach ($requiredImageKey in @("path", "width", "height", "byte_length", "sha256")) {
                if ($image.Keys -cnotcontains $requiredImageKey) {
                    throw "Visual capture manifest image is missing required property '$requiredImageKey'."
                }
            }

            $expectedPortablePath = "artifacts/visual/captures/wall-cutaway.png"
            if ($image["path"] -cne $expectedPortablePath) {
                throw "Expected manifest image path '$expectedPortablePath', found '$($image["path"])'."
            }
            if ($image["width"] -ne 1280 -or $image["height"] -ne 720) {
                throw "Expected manifest image dimensions 1280x720, found $($image["width"])x$($image["height"])."
            }

            $pngInfo = Get-Item -LiteralPath $capturePng
            if ($pngInfo.Length -le 0 -or $image["byte_length"] -ne $pngInfo.Length) {
                throw "Manifest byte_length '$($image["byte_length"])' does not match PNG length '$($pngInfo.Length)'."
            }
            $manifestSha256 = [string]$image["sha256"]
            if ($manifestSha256 -cnotmatch '^[0-9a-f]{64}$') {
                throw "Manifest image sha256 must be 64 lowercase hexadecimal characters."
            }
            $actualSha256 = (Get-FileHash -LiteralPath $capturePng -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($manifestSha256 -cne $actualSha256) {
                throw "Manifest image sha256 does not match the generated PNG."
            }

            $pngHeader = [byte[]]::new(24)
            $pngStream = [System.IO.File]::OpenRead($capturePng)
            try {
                if ($pngStream.Read($pngHeader, 0, $pngHeader.Length) -ne $pngHeader.Length) {
                    throw "Generated PNG is too short to contain an IHDR chunk."
                }
            }
            finally {
                $pngStream.Dispose()
            }
            $expectedPngPrefix = [byte[]](137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82)
            for ($index = 0; $index -lt $expectedPngPrefix.Length; $index++) {
                if ($pngHeader[$index] -ne $expectedPngPrefix[$index]) {
                    throw "Generated image does not have a valid PNG signature and IHDR prefix."
                }
            }
            $pngWidth = (([int]$pngHeader[16]) -shl 24) `
                -bor (([int]$pngHeader[17]) -shl 16) `
                -bor (([int]$pngHeader[18]) -shl 8) `
                -bor ([int]$pngHeader[19])
            $pngHeight = (([int]$pngHeader[20]) -shl 24) `
                -bor (([int]$pngHeader[21]) -shl 16) `
                -bor (([int]$pngHeader[22]) -shl 8) `
                -bor ([int]$pngHeader[23])
            if ($pngWidth -ne 1280 -or $pngHeight -ne 720) {
                throw "Expected generated PNG dimensions 1280x720, found ${pngWidth}x${pngHeight}."
            }

            Write-Output "Visual capture passed: $capturePng"
            Write-Output "Manifest: $captureManifest"
            Write-Output "SHA-256: $actualSha256"
        }
        "editor" {
            Get-GodotAiPortSetting | Out-Null
            Invoke-Godot -Arguments @("--editor", "--path", $gameProject)
        }
        "run" {
            $arguments = [System.Collections.Generic.List[string]]@("--path", $gameProject)
            if ($AutoQuitSeconds -gt 0) {
                $arguments.Add("--")
                $arguments.Add("--auto-quit-seconds=$AutoQuitSeconds")
            }
            if ($AutoQuitSeconds -gt 0) {
                Invoke-GodotAutomated -Arguments $arguments.ToArray()
            }
            else {
                Invoke-Godot -Arguments $arguments.ToArray()
            }
        }
    }
}
finally {
    Pop-Location
}
