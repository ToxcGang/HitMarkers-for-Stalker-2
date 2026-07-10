[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$PackagePath = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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
    $packageRoot = $packageItem.FullName
    $packageBase = Join-Path $packageRoot 'HitMarkersStalker2-Windows'
} else {
    $packageRoot = $packageItem.DirectoryName
    $packageBase = Join-Path $packageRoot $packageItem.BaseName
}

$packageFiles = foreach ($extension in '.pak', '.ucas', '.utoc') {
    $file = "$packageBase$extension"
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "The package trio is incomplete; missing $file"
    }
    Get-Item -LiteralPath $file
}

$modsDir = Join-Path $paksDir '~mods'
New-Item -ItemType Directory -Force -Path $modsDir | Out-Null

foreach ($file in $packageFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination $modsDir -Force
    Write-Host "Installed $($file.Name) to $modsDir"
}
