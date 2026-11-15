# Map and terrain

**Purpose:** the Sim's only terrain and original-tree input. The map is baked from Unity assets into a binary file the
Sim reads. The Sim never asks Unity for terrain (decision 0003).

## Key types and files

| Type | File | Role |
|---|---|---|
| `IHeightField` | `Assets/Sim/Terrain/IHeightField.cs` | `HeightAt`, `SlopeDegAt`, `Contains`, `Bounds`. Out-of-bounds queries clamp. The only terrain API Sim code uses. |
| `HeightGrid` | `Terrain/HeightGrid.cs` | Metres, bilinear height, central-difference slope. Origin (0,0). About 36 MB for 6 km at 2 m. |
| `FlatHeightField` | `Terrain/FlatHeightField.cs` | No-map world. |
| `Bounds2` | `Terrain/Bounds2.cs` | Rectangle. |
| `MapData` | `Assets/Sim/Map/MapData.cs` | Format version, map id, `HeightGrid`, `MapTree` list, `ContentHash` (64-bit FNV-1a of the file bytes). |
| `MapDataIO` | `Map/MapDataIO.cs` | Binary read and write from `byte[]` or `Stream`. Bad magic, unknown version or truncation throw `MapDataFormatException`. |
| `SyntheticMap` | `Map/SyntheticMap.cs` | `CreateRamp()`: 200×200 m test map with 5 trees, used by tests. |

File format: little-endian, magic `LLMP`, int32 format version (`MapData.CurrentVersion` = 1), map id, height grid,
then trees. Other versions are rejected. The map has no baked roads (RD-14).

## Bake pipeline (Unity editor)

1. **Authoring:** `Lords Legacy/Map/Generate Test Terrain` (`TestTerrainGenerator`) writes `Assets/Maps/TestMap/TestMapTerrain.asset`,
   the scene `TestMap.unity` and `forest-region.json`. Same seed gives the same result.
   `Lords Legacy/Map/Scatter Forest Trees` (`ForestScatter`) reads the JSON and writes `TestMapTrees.asset` (trunk 12–20 m).
2. **Bake:** `Lords Legacy/Map/Bake Test Map` (`MapBaker`) needs `TestMap.unity` open. It fails with a clear error if the
   terrain moved or the size rule is broken.
3. **Output:** `Assets/StreamingAssets/Maps/testmap.lsmap`, committed as plain binary (`*.lsmap binary`, no LFS).
   Runtime reads its bytes from `Application.streamingAssetsPath`.

Rule: terrain size ÷ (heightmap resolution − 1) = 2 m, the bake cell size, so Unity samples coincide with Sim grid nodes.
Test map: 2,048 × 2,048 m, 1025 × 1025 heightmap. Tree ids in the file equal the `TreeAuthoringData` ids.

Coordinates: Sim x/z = Unity world x/z in metres; Sim height = Unity world y. The terrain sits at world (0,0,0).

## Extending

- A new map version gets a new file name (SV-31); do not re-bake a shipped map in place.
- Anything new that needs terrain takes an `IHeightField` from the world. Do not read the Unity Terrain.

## Gotchas

- **Re-baking is manual.** After any terrain or tree change, run the menu item, then commit the new `.lsmap`.
- Re-baking breaks old saves in development (SV-32). They are refused by hash (`SaveMapMismatchException`).
- Unity Terrain is drawn and used as a picking collider only. It is not built from Sim data.

## Tests

`Sim.Tests/MapDataTests.cs`, `Sim.Tests/Terrain/HeightFieldTests.cs`. Unity: `Assets/Tests/EditMode/MapBakeTests.cs`,
`ForestScatterTests.cs`.
