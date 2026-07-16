# Building

HitMarkers is built without Zone Kit or UE4SS. The build converts and patches cooked Unreal Engine 5.1 assets, then writes the `.pak`, `.ucas`, and `.utoc` IoStore package with `retoc`.

## Requirements

- S.T.A.L.K.E.R. 2 installed and updated to patch 1.6 or later.
- Windows Developer Mode enabled so the build can create read-only symbolic links to the game containers.
- .NET 8 SDK.
- `retoc` 0.1.5.

## Package

Run:

```powershell
.\scripts\package.ps1 `
    -RetocPath 'C:\Tools\retoc.exe' `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl'
```

The script downloads the pinned Damage Numbers 2.0 reference archive from mod.io, verifies its SHA-256 hash, converts the required cooked assets against the installed game, applies the HitMarkers event-graph changes, packages the result, and runs `retoc verify`. After packing, it restores and independently verifies the working reference's container ID, chunk order, perfect-hash data, directory index, and mount point while retaining the generated chunk offsets and hashes.

Outputs are written to `dist` and are intentionally excluded from Git.

## Reference compatibility control

Before deriving HitMarkers from the pinned Damage Numbers package, build an untouched control:

```powershell
.\scripts\package.ps1 `
    -RetocPath 'C:\Tools\retoc.exe' `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl' `
    -ReferenceControl
```

This copies the original `ShowDMGStalker2-Windows` trio without converting, patching, repacking, or renaming it. The build verifies byte-for-byte hashes, the original container identity, mount point, package count, and `retoc verify` before creating `dist\ShowDMG-Control.zip`.

To isolate cooked-asset conversion from Blueprint patching, build the conversion control:

```powershell
.\scripts\package.ps1 `
    -RetocPath 'C:\Tools\retoc.exe' `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl' `
    -ConversionControl
```

This performs the same UE5.1 legacy and Zen conversions as HitMarkers without changing any Blueprint. It restores the working reference's IoStore index, requires cooked payloads and parsed asset semantics to survive the round trip, and creates `dist\ShowDMG-ConversionControl.zip` under the original `ShowDMGStalker2-Windows` identity. Serializer-only `.uasset` header normalization is accepted only when names, imports, exports, and every export payload remain identical.

The conversion control is expected to display the original damage numbers in-game. If it mounts but does not run, do not use the reconstructed Zen packages. Build the direct HUD control instead:

```powershell
.\scripts\package.ps1 `
    -RetocPath 'C:\Tools\retoc.exe' `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl' `
    -DirectHudControl
```

This mode keeps the working reference's original Zen package headers and untouched raw chunks. It applies only equal-length export changes, transplants them into the original widget chunk, and creates `dist\HitMarkers-DirectHudControl.zip`. The control uses ShowDMG's proven damage and viewport flow to display a white `X` instead of its numeric value; it does not patch the runner, subsystem, spawner, or damage hooks.

After the control succeeds, build the bootstrap-only diagnostic package with `-BootstrapDiagnostics`. This preserves the working reference's internal container identity, chunk map, perfect-hash lookup, and directory index while adding only runner-startup and HUD-canary diagnostics. It does not enable or bind damage-event hooks.

## Install

Run:

```powershell
.\scripts\install.ps1 `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl'
```

For an isolated compatibility test, pass the control `.utoc` and remove only known HitMarkers/ShowDMG test packages before installation:

```powershell
.\scripts\install.ps1 `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl' `
    -PackagePath '.\dist\ShowDMGStalker2-Windows.utoc' `
    -ExclusiveHitMarkersTest
```
