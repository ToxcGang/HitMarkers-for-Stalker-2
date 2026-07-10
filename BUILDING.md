# Building

HitMarkers is built without Zone Kit or UE4SS. The build converts and patches cooked Unreal Engine 5.1 assets, then writes the `.pak`, `.ucas`, and `.utoc` IoStore package with `retoc`.

## Requirements

- S.T.A.L.K.E.R. 2 installed and updated to patch 1.6 or later.
- Windows Developer Mode enabled so the build can create read-only symbolic links to the game containers.
- .NET 8 SDK.
- `retoc` 0.1.0 or later.

## Package

Run:

```powershell
.\scripts\package.ps1 `
    -RetocPath 'C:\Tools\retoc.exe' `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl'
```

The script downloads the pinned Damage Numbers 2.0 reference archive from mod.io, verifies its SHA-256 hash, converts the required cooked assets against the installed game, applies the HitMarkers event-graph changes, packages the result, and runs `retoc verify`.

Outputs are written to `dist` and are intentionally excluded from Git.

## Install

Run:

```powershell
.\scripts\install.ps1 `
    -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\S.T.A.L.K.E.R. 2 Heart of Chornobyl'
```
