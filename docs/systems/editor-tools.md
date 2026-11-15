# Editor tools

**Purpose:** generate or bake everything that is a Unity asset or scene, so no one edits YAML by hand (decision 0010).
Code lives in `Assets/Editor/` (`LordsLegacy.Editor` assembly, namespace `LordsLegacy.EditorTools`).

## Menus and scripts

Exact menu strings (from the `[MenuItem]` attributes):

| Menu item | Script | Output |
|---|---|---|
| `Lords Legacy/Map/Generate Test Terrain` | `MapGen/Terrain/TestTerrainGenerator` | `Assets/Maps/TestMap/TestMapTerrain.asset`, `TestMap.unity`, `forest-region.json` |
| `Lords Legacy/Map/Scatter Forest Trees` | `MapGen/Forest/ForestScatter` | `Assets/Maps/TestMap/TestMapTrees.asset` (seeded, ids 0..n−1) |
| `Lords Legacy/Map/Bake Test Map` | `MapGen/Bake/MapBaker` | `Assets/StreamingAssets/Maps/testmap.lsmap` (needs `TestMap.unity` open) |
| `LordsLegacy/Map/Generate Start Layout` | `MapGen/Start/StartLayoutGenerator` | `Assets/Maps/TestMap/StartLayout.asset` (idempotent) |
| `LordsLegacy/Config/Create default SimConfig` | `Scenes/SimConfigGenerator` | `Assets/Game/Config/SimConfig.asset` (never overwrites) |
| `LordsLegacy/Config/Reset SimConfig to code defaults` | `Scenes/SimConfigGenerator` | Resets the asset's values to the code defaults |
| `LordsLegacy/Scenes/Generate Game scene` | `Scenes/GameSceneGenerator` | `Assets/Scenes/Game.unity` (rebuilt from empty each run) |
| `LordsLegacy/Art/Generate placeholder visuals` | `Visuals/PlaceholderVisualGenerator` | `VisualDefinition` assets and `VisualRegistry.asset` (idempotent) |
| `LordsLegacy/Art/List placeholder visuals` | `Visuals/PlaceholderVisualLister` | Log of ids marked as placeholders (ART-03) |
| `Lords Legacy/Create Shared PanelSettings` | `CreatePanelSettings` | `Assets/Game/UI/PanelSettings.asset` |

Other editor code: `AgentNoc/AgentFriendlyProjectSettings` (forces text serialization and visible `.meta` files when the Editor
loads), `Scenes/SceneYamlNormalizer` (stable file ids so two generator runs give the same scene file).

`Assets/Editor/T151TempTestRunner.cs` (`Tools/T151 …` menus) and `Assets/Editor/TmpTestRun.cs` look like temporary test
runners from an earlier task. They are not part of the workflow above. Whether to delete them is for the human.

## Rules

- A generator is idempotent, or it says plainly that it overwrites. Re-run it after any change it owns, then commit the result.
- Generators wire serialized references in code. Do not wire them by hand in the scene.
- Re-baking after any terrain or tree change is a manual step. Runtime tests use the committed `.lsmap` and never bake.
- The editor scripts may reference `LordsLegacy.Sim` and `LordsLegacy.Game`. The Sim never references the editor.

## Extending

Add a menu script in the right folder, with `[MenuItem]`, that writes assets through `AssetDatabase`. If it changes a scene,
add a `GameSceneTests.GameSceneHas...` PlayMode check for the new wiring.

## Gotchas

- `MapBaker` fails with a clear error if the terrain was moved or the 2 m rule is broken.
- Same seed gives the same forest and terrain. Changing the generator changes every committed asset on the next run.

## Tests

Unity EditMode: `Assets/Tests/EditMode/MapBakeTests.cs`, `ForestScatterTests.cs`, `SimConfigTests.cs`, `StartLayoutConfigTests.cs`,
`VisualRegistryTests.cs`. PlayMode: `GameSceneTests.cs` (checks the generated scene's wiring per milestone).
