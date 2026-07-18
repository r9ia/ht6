# Optional Third-Party Unity Assets

The Night Watch bootstrap is fully functional without third-party content. Optional Asset Store packages improve presentation but must not become hard dependencies.

## Approved candidates

### Environment: Backrooms Like Asset RE

- URL: <https://assetstore.unity.com/packages/3d/environments/backrooms-like-asset-re-349136>
- Publisher: Loafbrr
- Verified listing: version 1.0, released 2026-01-21, 292.8 MB, free, Standard Unity Asset Store EULA / Extension Asset.
- Listed compatibility: URP on Unity 2023.2.22f1.
- Project target: Unity 6000.5.4f1. Import and rendering must therefore be smoke-tested; compatibility is promising but not guaranteed by the listing.

### Creature: Creep Horror Creature

- URL: <https://assetstore.unity.com/packages/3d/characters/creatures/creep-horror-creature-244853>
- Publisher: AC Game Assets
- Verified listing: version 1.0, released 2023-02-24, 216.9 MB, free, Standard Unity Asset Store EULA / Extension Asset.
- Listed compatibility: Built-in, URP, and HDRP on Unity 2021.3.10f1/2021.3.19f1.
- Project target: Unity 6000.5.4f1. Validate materials, rig/animations, shadows, and performance after import.

## Import and repository policy

1. Acquire/import packages through Karan's Unity Asset Store account. The project does not scrape, redistribute, or bypass Asset Store licensing.
2. Do not commit `.unitypackage` downloads. The package contents are large; decide on team distribution and Git LFS only after validating them.
3. Run **Dread Director → Validate Optional Asset Discovery** to see which prefabs the bootstrap selected.
4. Run **Dread Director → Build Night Watch Scene**. The bootstrap scores imported prefabs by package/name keywords, normalizes their bounds, and wires the result automatically.
5. If no suitable prefab exists or validation fails, the bootstrap uses its primitive security room and apparition. Core gameplay remains unchanged.

## Acceptance checks

- No magenta/missing URP materials.
- No missing scripts or package-only runtime errors.
- Environment has usable bounds and colliders.
- Creature is visible at approximately human scale and can be hidden/revealed by `ApparitionController`.
- Scene remains playable at the target demo frame rate.
- Package import does not alter or stage `Assets/TutorialInfo/Icons/URP.png`.


## Current repository import

On 2026-07-18, only `Assets/LoafbrrAssets/BackroomsLikeAssetRe` was selectively restored from `origin/georgia` (`4abee77`): 679 files / approximately 294.5 MiB materialized. Its included `README.txt` declares CC0. The older duplicate `Assets/Asset/BackroomsLikeAsset` tree and unrelated ~1.1 GB `Assets/TerrainDemoScene_URP` tree were deliberately not imported. The bootstrap deterministically prefers `prefab/Level/TstLevel.prefab`.


### Current creature: Quaternius Demon

- Model: <https://poly.pizza/m/Mo2ky6vkf8>
- Bundle: <https://poly.pizza/bundle/Ultimate-Monsters-Bundle-5oyGWAmOB6>
- License: CC0 1.0; animated FBX/GLTF listing.
- Imported file: `Assets/ThirdParty/Quaternius/UltimateMonsters/Demon/Demon.fbx`.
- Only the selected Demon was retained from the public 45-model archive. Checksums and attribution are recorded in `Assets/ThirdParty/Quaternius/UltimateMonsters/LICENSE.md`.
