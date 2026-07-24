[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$magick = Get-Command magick -ErrorAction Stop
$assetId = 'prop.station.wall_utility.v1'
$runId = 'prop.station.wall_utility.v1__tripo__v3.1-best-quality__2026-07-24__01'
$sourceRelative =
    'art/reference-sheets/frontier-station-v1/poc-models/station-wall-utility-turnaround-v1.png'
$source = Join-Path $repoRoot $sourceRelative
$runRoot = Join-Path $repoRoot (Join-Path 'art/generated' (Join-Path $assetId $runId))
$inputRoot = Join-Path $runRoot 'input'

if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Approved source sheet is missing: $source"
}

New-Item -ItemType Directory -Force -Path $inputRoot | Out-Null

# The source is a 1672 x 941 two-by-two sheet. These crops exclude its central
# divider while retaining the approved panel pixels and consistent framing.
$crops = @(
    @{ Name = 'front.png'; Slot = 'front'; Geometry = '834x469+0+0' }
    @{ Name = 'right.png'; Slot = 'right'; Geometry = '834x469+838+0' }
    @{ Name = 'top.png'; Slot = 'validation-top'; Geometry = '834x469+0+472' }
    @{ Name = 'front-right-3q.png'; Slot = 'validation-three-quarter'; Geometry = '834x469+838+472' }
)

$manifestCrops = @()

foreach ($crop in $crops) {
    $destination = Join-Path $inputRoot $crop.Name
    & $magick.Source $source -crop $crop.Geometry '+repage' `
        -define 'png:compression-level=9' $destination
    if ($LASTEXITCODE -ne 0) {
        throw "ImageMagick crop failed: $destination"
    }

    $dimensions = (& $magick.Source identify -format '%wx%h' $destination).Trim()
    if ($dimensions -ne '834x469') {
        throw "Unexpected crop dimensions for ${destination}: $dimensions"
    }

    $item = Get-Item -LiteralPath $destination
    $manifestCrops += [ordered]@{
        file = "input/$($crop.Name)"
        intended_slot = $crop.Slot
        geometry = $crop.Geometry
        dimensions = $dimensions
        bytes = $item.Length
        sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
}

$sourceItem = Get-Item -LiteralPath $source
$manifest = [ordered]@{
    asset_id = $assetId
    run_id = $runId
    operation = 'lossless panel crop; no repaint or semantic edit'
    source = [ordered]@{
        file = $sourceRelative
        dimensions = '1672x941'
        bytes = $sourceItem.Length
        sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    }
    crops = $manifestCrops
}

$manifestPath = Join-Path $runRoot 'input-manifest.json'
$manifest | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Output $manifestPath
