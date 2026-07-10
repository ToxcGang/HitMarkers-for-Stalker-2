[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $GameRoot -PathType Container)) {
    throw "Game root was not found: $GameRoot"
}

$paksDir = Join-Path $GameRoot 'Stalker2\Content\Paks'
if (-not (Test-Path -LiteralPath $paksDir -PathType Container)) {
    throw "S.T.A.L.K.E.R. 2 Paks directory was not found: $paksDir"
}

if (-not (Test-Path -LiteralPath $PackagePath)) {
    throw "Package path was not found: $PackagePath"
}

$packageItem = Get-Item -LiteralPath $PackagePath

if ($packageItem.PSIsContainer) {
    $packageFiles = Get-ChildItem -LiteralPath $packageItem.FullName -File |
        Where-Object { $_.Extension -in '.pak', '.ucas', '.utoc' }
} else {
    $packageFiles = @($packageItem) |
        Where-Object { $_.Extension -in '.pak', '.ucas', '.utoc' }
}

if (-not $packageFiles) {
    throw "No .pak, .ucas, or .utoc package files were found in: $PackagePath"
}

$modsDir = Join-Path $paksDir '~mods'
New-Item -ItemType Directory -Force -Path $modsDir | Out-Null

foreach ($file in $packageFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination $modsDir -Force
    Write-Host "Installed $($file.Name) to $modsDir"
}
