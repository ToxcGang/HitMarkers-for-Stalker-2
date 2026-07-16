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

    [string]$PackageName = 'HitMarkersStalker2-Windows',

    [switch]$ReferenceControl,

    [switch]$BootstrapDiagnostics,

    [switch]$Diagnostics
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$referenceUrl = 'https://g-5761.modapi.io/v1/games/5761/mods/5256912/files/6800765/download'
$referenceSha256 = 'F5D37FD3D55BC3B79CB5BA564C9ABF7A3365647D40675D6F6985B624C800C82B'

if (@($ReferenceControl, $BootstrapDiagnostics, $Diagnostics).Where({ $_ }).Count -gt 1) {
    throw 'ReferenceControl, BootstrapDiagnostics, and Diagnostics are mutually exclusive.'
}

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

function Assert-RawManifestEquivalent {
    param(
        [Parameter(Mandatory = $true)][string]$ReferenceManifest,
        [Parameter(Mandatory = $true)][string]$CandidateManifest,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $reference = Get-Content -LiteralPath $ReferenceManifest -Raw | ConvertFrom-Json
    $candidate = Get-Content -LiteralPath $CandidateManifest -Raw | ConvertFrom-Json
    if ($candidate.version -ne $reference.version) {
        throw "$Description TOC version differs from the working reference."
    }

    $referenceChunks = @($reference.chunk_paths.psobject.Properties)
    $candidateChunks = @($candidate.chunk_paths.psobject.Properties)
    if ($candidateChunks.Count -ne $referenceChunks.Count) {
        throw "$Description chunk count differs from the working reference."
    }
    foreach ($chunk in $referenceChunks) {
        $candidateChunk = $candidate.chunk_paths.psobject.Properties[$chunk.Name]
        if (-not $candidateChunk -or $candidateChunk.Value -ne $chunk.Value) {
            throw "$Description changed chunk $($chunk.Name) ($($chunk.Value))."
        }
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
$zenDir = Join-Path $resolvedWorkDir 'zen-unmounted'
$rawDir = Join-Path $resolvedWorkDir 'raw-mounted'
$referenceRawDir = Join-Path $resolvedWorkDir 'reference-raw'
$packedDir = Join-Path $resolvedWorkDir 'packed-container'
$internalDir = Join-Path $resolvedWorkDir 'internal-container'
$roundTripRawDir = Join-Path $resolvedWorkDir 'roundtrip-raw'
$legacyRoundTripDir = Join-Path $resolvedWorkDir 'legacy-roundtrip'
New-Item -ItemType Directory -Force -Path $referenceDir, $combinedDir, $legacyDir, $patchedDir, $zenDir, `
    $packedDir, $internalDir, $legacyRoundTripDir, $resolvedOutputDir | Out-Null

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

if ($ReferenceControl) {
    $controlName = 'ShowDMGStalker2-Windows'
    $controlFiles = foreach ($extension in '.pak', '.ucas', '.utoc') {
        $source = Resolve-RequiredFile -Path "$referenceBase$extension" -Name "Damage Numbers control $extension file"
        $destination = Join-Path $resolvedOutputDir "$controlName$extension"
        Copy-Item -LiteralPath $source -Destination $destination -Force

        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $outputHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $outputHash) {
            throw "Reference control hash mismatch after copying $extension."
        }

        Get-Item -LiteralPath $destination
    }

    $controlUtoc = Join-Path $resolvedOutputDir "$controlName.utoc"
    Invoke-Checked -Executable $resolvedRetoc -Description 'Untouched Damage Numbers control verification' -Arguments @(
        'verify', $controlUtoc
    )

    $controlInfo = (& $resolvedRetoc 'info' $controlUtoc | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "Damage Numbers control inspection failed with exit code $LASTEXITCODE."
    }
    if ($controlInfo -notmatch '(?m)^ShowDMGStalker2-Windows\s*$') {
        throw "Damage Numbers control identity postcondition failed.`n$controlInfo"
    }
    if ($controlInfo -notmatch [regex]::Escape('mount_point: ../../../Stalker2/Content/')) {
        throw "Damage Numbers control mount point postcondition failed.`n$controlInfo"
    }
    if ($controlInfo -notmatch 'packages:\s+7') {
        throw "Damage Numbers control package count postcondition failed.`n$controlInfo"
    }

    $controlZip = Join-Path $resolvedOutputDir 'ShowDMG-Control.zip'
    if (Test-Path -LiteralPath $controlZip) {
        Remove-Item -LiteralPath $controlZip -Force
    }
    Compress-Archive -LiteralPath $controlFiles.FullName -DestinationPath $controlZip -CompressionLevel Optimal
    Write-Host "Created byte-identical reference control $controlZip"
    return
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
    $resolvedPatcher = Resolve-RequiredFile -Path (Join-Path $patcherDirectory 'bin\Release\net8.0\HitMarkersPatcher.exe') -Name 'Built HitMarkers patcher apphost'
}

if ([System.IO.Path]::GetExtension($resolvedPatcher) -ne '.exe') {
    throw "PatcherPath must be the HitMarkersPatcher.exe apphost: $resolvedPatcher"
}
$patcherArguments = @($legacyDir, $patchedDir)
if ($Diagnostics) { $patcherArguments = @('--diagnostics', $legacyDir, $patchedDir) }
if ($BootstrapDiagnostics) { $patcherArguments = @('--bootstrap-diagnostics', $legacyDir, $patchedDir) }
Invoke-Checked -Executable $resolvedPatcher -Description 'HitMarkers cooked Blueprint patch' -Arguments @(
    $patcherArguments
)
$verifyMode = if ($BootstrapDiagnostics) { '--verify-bootstrap' } else { '--verify' }
Invoke-Checked -Executable $resolvedPatcher -Description 'Independent cooked Blueprint verification' -Arguments @(
    $verifyMode, $patchedDir
)
foreach ($relativePath in @(
    'Stalker2\Content\bp_mwss_ShowDMG.uasset',
    'Stalker2\Content\Autogenerated_1758836765_WorldSubsystemData.uasset'
)) {
    $sourceSubsystem = Resolve-RequiredFile -Path (Join-Path $legacyDir $relativePath) -Name "Source bootstrap asset $relativePath"
    $patchedSubsystem = Resolve-RequiredFile -Path (Join-Path $patchedDir $relativePath) -Name "Patched bootstrap asset $relativePath"
    if ((Get-FileHash -LiteralPath $sourceSubsystem -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $patchedSubsystem -Algorithm SHA256).Hash) {
        throw "Bootstrap registration asset was unexpectedly modified: $relativePath"
    }
}

$outputUtoc = Join-Path $resolvedOutputDir "$PackageName.utoc"
$internalName = 'ShowDMGStalker2-Windows'
$unmountedUtoc = Join-Path $zenDir "$internalName.utoc"
$packedUtoc = Join-Path $packedDir "$internalName.utoc"
$internalUtoc = Join-Path $internalDir "$internalName.utoc"
foreach ($extension in '.pak', '.ucas', '.utoc') {
    $stalePackage = Join-Path $resolvedOutputDir "$PackageName$extension"
    if (Test-Path -LiteralPath $stalePackage) {
        Remove-Item -LiteralPath $stalePackage -Force
    }
}
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers IoStore packaging' -Arguments @(
    'to-zen', $patchedDir, $unmountedUtoc, '--version', 'UE5_1'
)
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers raw container extraction' -Arguments @(
    'unpack-raw', $unmountedUtoc, $rawDir
)
Invoke-Checked -Executable $resolvedRetoc -Description 'Working reference raw container extraction' -Arguments @(
    'unpack-raw', $referenceUtoc.FullName, $referenceRawDir
)
$referenceManifest = Resolve-RequiredFile -Path (Join-Path $referenceRawDir 'manifest.json') -Name 'Reference raw manifest'
$manifestPath = Resolve-RequiredFile -Path (Join-Path $rawDir 'manifest.json') -Name 'Generated raw manifest'
Assert-RawManifestEquivalent -ReferenceManifest $referenceManifest -CandidateManifest $manifestPath `
    -Description 'Generated HitMarkers manifest'

Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers mounted container packaging' -Arguments @(
    'pack-raw', $rawDir, $packedUtoc
)
Invoke-Checked -Executable $resolvedPatcher -Description 'HitMarkers reference IoStore index restoration' -Arguments @(
    '--restore-container-index', $referenceUtoc.FullName, $packedUtoc, $internalUtoc
)
Invoke-Checked -Executable $resolvedPatcher -Description 'Independent HitMarkers IoStore index verification' -Arguments @(
    '--verify-container-index', $referenceUtoc.FullName, $internalUtoc
)
$internalBase = Join-Path $internalDir $internalName
$packedBase = Join-Path $packedDir $internalName
Copy-Item -LiteralPath (Resolve-RequiredFile -Path "$packedBase.ucas" -Name 'Packed HitMarkers .ucas') -Destination "$internalBase.ucas" -Force
Copy-Item -LiteralPath (Resolve-RequiredFile -Path "$referenceBase.pak" -Name 'Reference companion .pak') -Destination "$internalBase.pak" -Force
foreach ($extension in '.pak', '.ucas', '.utoc') {
    Copy-Item -LiteralPath (Resolve-RequiredFile -Path "$internalBase$extension" -Name "Internal $extension package") `
        -Destination (Join-Path $resolvedOutputDir "$PackageName$extension") -Force
}
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers package verification' -Arguments @(
    'verify', $outputUtoc
)
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers raw round-trip extraction' -Arguments @(
    'unpack-raw', $outputUtoc, $roundTripRawDir
)
$roundTripManifest = Resolve-RequiredFile -Path (Join-Path $roundTripRawDir 'manifest.json') -Name 'Round-trip raw manifest'
Assert-RawManifestEquivalent -ReferenceManifest $referenceManifest -CandidateManifest $roundTripManifest `
    -Description 'Round-trip HitMarkers manifest'

$containerInfo = (& $resolvedRetoc 'info' $outputUtoc | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "HitMarkers container inspection failed with exit code $LASTEXITCODE."
}
$internalInfo = (& $resolvedRetoc 'info' $internalUtoc | Out-String)
if ($LASTEXITCODE -ne 0 -or $internalInfo -notmatch '(?m)^ShowDMGStalker2-Windows\s*$') {
    throw "HitMarkers internal build identity postcondition failed.`n$internalInfo"
}
if ($containerInfo -notmatch [regex]::Escape('mount_point: ../../../Stalker2/Content/') -or
    $internalInfo -notmatch [regex]::Escape('mount_point: ../../../Stalker2/Content/')) {
    throw "HitMarkers mount point postcondition failed.`n$containerInfo"
}
if ($containerInfo -notmatch 'chunks:\s+8' -or $containerInfo -notmatch 'packages:\s+7') {
    throw "HitMarkers cooked package count postcondition failed.`n$containerInfo"
}
foreach ($extension in '.pak', '.ucas', '.utoc') {
    $internalHash = (Get-FileHash -LiteralPath "$internalBase$extension" -Algorithm SHA256).Hash
    $externalHash = (Get-FileHash -LiteralPath (Join-Path $resolvedOutputDir "$PackageName$extension") -Algorithm SHA256).Hash
    if ($internalHash -ne $externalHash) {
        throw "External $PackageName$extension differs from the internally identified ShowDMG container."
    }
}

foreach ($extension in '.pak', '.ucas', '.utoc') {
    Copy-Item -LiteralPath "$internalBase$extension" -Destination $combinedDir -Force
}
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers legacy round-trip conversion' -Arguments @(
    'to-legacy', $combinedDir, $legacyRoundTripDir, '--filter', 'ShowDMG', '--version', 'UE5_1'
)
Invoke-Checked -Executable $resolvedRetoc -Description 'HitMarkers world subsystem legacy round-trip conversion' -Arguments @(
    'to-legacy', $combinedDir, $legacyRoundTripDir, '--filter', 'Autogenerated_1758836765_WorldSubsystemData', '--version', 'UE5_1'
)
Invoke-Checked -Executable $resolvedPatcher -Description 'Legacy round-trip cooked Blueprint verification' -Arguments @(
    $verifyMode, $legacyRoundTripDir
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
