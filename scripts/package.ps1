[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RetocPath,

    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [string]$ReferenceArchive,

    [string]$PatcherPath,

    [string]$OutputDir = (Join-Path $PSScriptRoot '..\dist'),

    [string]$WorkDir = (Join-Path $PSScriptRoot '..\.build\HitMarkers'),

    [string]$PackageName = 'HitMarkersStalker2-Windows'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$referenceUrl = 'https://g-5761.modapi.io/v1/games/5761/mods/5256912/files/6800765/download'
$referenceSha256 = 'F5D37FD3D55BC3B79CB5BA564C9ABF7A3365647D40675D6F6985B624C800C82B'

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

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Description
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$resolvedRetoc = Resolve-RequiredFile -Path $RetocPath -Name 'retoc.exe'
$resolvedGameRoot = Resolve-RequiredDirectory -Path $GameRoot -Name 'S.T.A.L.K.E.R. 2 game root'
$paksDir = Resolve-RequiredDirectory -Path (Join-Path $resolvedGameRoot 'Stalker2\Content\Paks') -Name 'Game Paks directory'
$patcherProject = Resolve-RequiredFile -Path (Join-Path $repoRoot 'tools\HitMarkersPatcher\HitMarkersPatcher.csproj') -Name 'HitMarkers patcher project'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK was not found on PATH.'
}

$resolvedWorkDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($WorkDir)
$resolvedOutputDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDir)
$repoPrefix = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedWorkDir.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "WorkDir must stay inside the repository: $resolvedWorkDir"
}

if (Test-Path -LiteralPath $resolvedWorkDir) {
    Remove-Item -LiteralPath $resolvedWorkDir -Recurse -Force
}

$referenceDir = Join-Path $resolvedWorkDir 'reference'
$combinedDir = Join-Path $resolvedWorkDir 'combined-paks'
$legacyDir = Join-Path $resolvedWorkDir 'legacy-source'
$patchedDir = Join-Path $resolvedWorkDir 'legacy-patched'
New-Item -ItemType Directory -Force -Path $referenceDir, $combinedDir, $legacyDir, $patchedDir, $resolvedOutputDir | Out-Null

if ($ReferenceArchive) {
    $resolvedArchive = Resolve-RequiredFile -Path $ReferenceArchive -Name 'Damage Numbers 2.0 reference archive'
} else {
    $resolvedArchive = Join-Path $resolvedWorkDir 'DamageNumbers-2.0.zip'
    Invoke-WebRequest -Uri $referenceUrl -OutFile $resolvedArchive -UseBasicParsing
}

$actualReferenceHash = (Get-FileHash -LiteralPath $resolvedArchive -Algorithm SHA256).Hash
if ($actualReferenceHash -ne $referenceSha256) {
    throw "Damage Numbers reference archive hash mismatch. Expected $referenceSha256, got $actualReferenceHash."
}

Expand-Archive -LiteralPath $resolvedArchive -DestinationPath $referenceDir -Force
$referenceUtoc = Get-ChildItem -LiteralPath $referenceDir -Recurse -File -Filter 'ShowDMGStalker2-Windows.utoc' |
    Select-Object -First 1
if (-not $referenceUtoc) {
    throw 'The Damage Numbers archive does not contain ShowDMGStalker2-Windows.utoc.'
}

$referenceBase = Join-Path $referenceUtoc.DirectoryName 'ShowDMGStalker2-Windows'
foreach ($extension in '.pak', '.ucas', '.utoc') {
    Resolve-RequiredFile -Path "$referenceBase$extension" -Name "Damage Numbers $extension file" | Out-Null
}

Get-ChildItem -LiteralPath $paksDir -File -Filter '*.utoc' |
    Copy-Item -Destination $combinedDir -Force

Get-ChildItem -LiteralPath $paksDir -File -Filter '*.ucas' | ForEach-Object {
    $linkPath = Join-Path $combinedDir $_.Name
    New-Item -ItemType SymbolicLink -Path $linkPath -Target $_.FullName | Out-Null
}

foreach ($extension in '.pak', '.ucas', '.utoc') {
    Copy-Item -LiteralPath "$referenceBase$extension" -Destination $combinedDir -Force
}

Invoke-Checked -Executable $resolvedRetoc -Description 'ShowDMG legacy conversion' -Arguments @(
    'to-legacy', $combinedDir, $legacyDir, '--filter', 'ShowDMG', '--version', 'UE5_1'
)
Invoke-Checked -Executable $resolvedRetoc -Description 'World subsystem legacy conversion' -Arguments @(
    'to-legacy', $combinedDir, $legacyDir, '--filter', 'Autogenerated_1758836765_WorldSubsystemData', '--version', 'UE5_1'
)

if ($PatcherPath) {
    $resolvedPatcher = Resolve-RequiredFile -Path $PatcherPath -Name 'HitMarkers patcher'
} else {
    $patcherDirectory = Split-Path -Parent $patcherProject
    $patcherAssets = Join-Path $patcherDirectory 'obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $patcherAssets -PathType Leaf)) {
        Invoke-Checked -Executable 'dotnet' -Description 'HitMarkers patcher dependency restore' -Arguments @(
            'restore', $patcherProject
        )
    }
    Invoke-Checked -Executable 'dotnet' -Description 'HitMarkers patcher build' -Arguments @(
        'build', $patcherProject, '--configuration', 'Release', '--no-restore'
    )
    $resolvedPatcher = Resolve-RequiredFile -Path (Join-Path $patcherDirectory 'bin\Release\net8.0\HitMarkersPatcher.dll') -Name 'Built HitMarkers patcher'
}

if ([System.IO.Path]::GetExtension($resolvedPatcher) -eq '.dll') {
    Invoke-Checked -Executable 'dotnet' -Description 'HitMarkers cooked Blueprint patch' -Arguments @(
        $resolvedPatcher, $legacyDir, $patchedDir
    )
} else {
    Invoke-Checked -Executable $resolvedPatcher -Description 'HitMarkers cooked Blueprint patch' -Arguments @(
        $legacyDir, $patchedDir
    )
}

$outputUtoc = Join-Path $resolvedOutputDir "$PackageName.utoc"
foreach ($extension in '.pak', '.ucas', '.utoc') {
    $stalePackage = Join-Path $resolvedOutputDir "$PackageName$extension"
    if (Test-Path -LiteralPath $stalePackage) {
        Remove-Item -LiteralPath $stalePackage -Force
    }
}
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers IoStore packaging' -Arguments @(
    'to-zen', $patchedDir, $outputUtoc, '--version', 'UE5_1'
)
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers package verification' -Arguments @(
    'verify', $outputUtoc
)

$packageFiles = foreach ($extension in '.pak', '.ucas', '.utoc') {
    Resolve-RequiredFile -Path (Join-Path $resolvedOutputDir "$PackageName$extension") -Name "Built $extension package"
}

$releaseZip = Join-Path $resolvedOutputDir 'HitMarkers-1.0.0.zip'
if (Test-Path -LiteralPath $releaseZip) {
    Remove-Item -LiteralPath $releaseZip -Force
}
Compress-Archive -LiteralPath $packageFiles -DestinationPath $releaseZip -CompressionLevel Optimal

Write-Host "Created and verified $releaseZip"
