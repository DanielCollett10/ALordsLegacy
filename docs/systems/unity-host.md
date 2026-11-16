# Unity host and config

**Purpose:** the one MonoBehaviour that owns the Sim in play, the configuration assets it reads, and the per-frame bridge
between real time and ticks. Everything else in the Unity layer reaches the Sim through it.

## Key types and files

`Assets/Game/Bootstrap/`, `Assets/Game/Config/`, `Assets/Game/GameAssemblyMarker.cs`.

| Type | File | Role |
|---|---|---|
| `SimHost` | `Bootstrap/SimHost.cs` | MonoBehaviour in the Game scene. Owns `SimWorld` (`World`), `TickPacer` (`Pacer`), `MapData` (`Map`). Exposes `TickFraction`, `Stats`, `Execute`, `TogglePause`. |
| `SimStats` | `Bootstrap/SimStats.cs` | Unity-side measurement: `LastStepMs`, `TicksPerSecond`. Sim code never reads it. |
| `SimConfig` | `Config/SimConfig.cs` | Serialized ScriptableObject (`Assets/Game/Config/SimConfig.asset`). Builds `SimTuning` and the road and area catalogs. Its "Food and foraging" group holds the M14 food and bush values (T-426). |
| `StartLayoutConfig` | `Config/StartLayoutConfig.cs` | ScriptableObject. `ToLayout()` → Sim `StartLayout`. |
| `PendingLoad` | `Assets/Game/Saving/` | Static holder for save bytes handed across a scene reload. |

## Flow

`SimHost.Start`:
1. If not initialised, read `StreamingAssets/Maps/testmap.lsmap` through `MapDataIO.Read`. A missing file logs an error that
   names the path and the bake menu item.
2. Build `SimTuning` and the catalogs from `SimConfig` (or code defaults when it is null). Edits apply on the next Play.
3. Pending load bytes (from `PendingLoad`) → `SimSave.Read`. Otherwise `SimWorld.FromMapData` and `StartLayoutApplier`.

`SimHost.Update`: if `AutoUpdate`, `Tick(Time.deltaTime)` → `TickPacer.Advance` → `World.Step(n)`. While paused it resets the
pacer. `TickFraction` is the carry for interpolation. Views read `World`, `Pacer`, `TickFraction`.

Entry points for views: `Execute(ISimCommand)`, `TogglePause`, and `TickPacer.SetSpeed`. Nothing else writes Sim state.

`Initialize(SimTuning, MapData)` and `Initialize(SimTuning, MapData, RoadTypeCatalog, AreaTypeCatalog)` let a caller supply the world's inputs. If `Initialize` is not called, `Start` reads the map file itself.

## Config rules

- Unity code reads types from `World.RoadTypes` and `World.AreaTypes`, never the static lists.
- Menu `LordsLegacy/Config/Create default SimConfig` creates the asset. It is idempotent and never overwrites.
- A save keeps the tuning it was made with (`save.md`).

## Extending

- A new balancing number goes in `SimTuning` and is copied in `SimConfig`. Views never copy it.
- A new config asset follows `SimConfig`: a serialized ScriptableObject, read once in `Start`, with a code default when null.

## Gotchas

- Edits to `SimConfig` do not affect a running game. Restart Play.
- `SimHost` is wired by `GameSceneGenerator.CreateSimHost`. A missing asset fails the generator, not the game.

## Tests

Unity PlayMode: `Assets/Tests/PlayMode/SimHostTests.cs`, `SimConfigHostTests.cs`. EditMode: `SimConfigTests.cs`, `SimStatsTests.cs`.
