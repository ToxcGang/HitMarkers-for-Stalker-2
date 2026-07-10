[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnrealPakPath,

    [Parameter(Mandatory = $true)]
    [string]$CookedContentPath,

    [string]$OutputDir = (Join-Path $PSScriptRoot '..\dist'),

    [string]$PackageName = 'pakchunk999-HitMarkers-Windows'
)

$ErrorActionPreference = 'Stop'

function Resolve-RequiredFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-RequiredDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Name was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

$resolvedUnrealPakPath = Resolve-RequiredFile -Path $UnrealPakPath -Name 'UnrealPak.exe'
$resolvedCookedContentPath = Resolve-RequiredDirectory -Path $CookedContentPath -Name 'Cooked content directory'
$resolvedOutputDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDir)

$cookedFiles = Get-ChildItem -LiteralPath $resolvedCookedContentPath -Recurse -File |
    Where-Object { $_.Name -ne '.gitkeep' }

if (-not $cookedFiles) {
    throw "Cooked content directory contains no files to package: $resolvedCookedContentPath"
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

$pakPath = Join-Path $resolvedOutputDir "$PackageName.pak"
$responseFile = Join-Path ([System.IO.Path]::GetTempPath()) "$PackageName-UnrealPak.txt"
$contentRoot = $resolvedCookedContentPath.TrimEnd('\', '/')

$responseLines = foreach ($file in $cookedFiles) {
    $relativePath = $file.FullName.Substring($contentRoot.Length).TrimStart('\', '/').Replace('\', '/')
    $mountPath = "../../../Stalker2/Content/HitMarkers/$relativePath"
    "`"$($file.FullName)`" `"$mountPath`""
}

Set-Content -LiteralPath $responseFile -Value $responseLines -Encoding ASCII

& $resolvedUnrealPakPath $pakPath "-Create=$responseFile" -compress

if ($LASTEXITCODE -ne 0) {
    throw "UnrealPak failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $pakPath -PathType Leaf)) {
    throw "UnrealPak completed but did not create the expected package: $pakPath"
}

Write-Host "Created $pakPath"
