# Changelog

## 1.0.0 - 2026-07-10

- Initial release.
- Added white hit markers for enemy hits.
- Added red hit markers for enemy kills.
- Added the autoloaded IoStore package, cooked Blueprint patcher, packaging script, install script, and repository documentation.
- Fixed confirmed firearm hit detection by binding S.T.A.L.K.E.R. 2's native `BulletProjectileHit` delegate and filtering events to the local player's GUID.
- Fixed white hit and red kill classification by confirming health loss or death within a 0.2-second event window.
- Fixed HUD ownership, Z-order, spawn initialization, and fade cleanup so markers display immediately above other UI.
- Reduced Agent discovery to a 0.5-second scan and added log-only `[HitMarkers]` initialization diagnostics.
- Enforced the `../../../Stalker2/Content/` IoStore mount point and expanded cooked-asset packaging postconditions.
