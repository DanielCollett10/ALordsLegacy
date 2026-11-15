# Sim core

**Purpose:** the root of all game state, the fixed tick order, the seeded random source and the balancing numbers.

## Key types and files

| Type | File | Role |
|---|---|---|
| `SimWorld` | `Assets/Sim/Core/SimWorld.cs` | Root. Tick clock, pause, `Terrain`, `Trees`, `Roads`, `Areas`, `Footprints`, `Operations`, `People`, `Tasks`, `Inventory`, `Construction`, `Nav`, `Random`, `Camp`, `CommandLog`. Never-null registries. |
| `SimWorld.<Area>.cs` | `Assets/Sim/Core/` | One partial per subsystem. Add a new one, don't grow `SimWorld.cs`. |
| `ISimSystem` | `Core/ISimSystem.cs` | `Step(world)` once per tick; `OnCommandApplied(world, result)` after an accepted command. |
| `SimSystems` | `Core/SimSystems.cs` | `CreateDefault()` is the one ordered list. The world copies it at construction. |
| `SimRandom` | `Core/SimRandom.cs` | PCG-XSH-RR 64/32, seeded from `SimTuning.WorldSeed` (12345). State is one `ulong`. |
| `SimTuning` | `Assets/Sim/Tuning/SimTuning.cs` | Every balancing number. |

## Shape

- `new SimWorld(tuning, roadTypes?, areaTypes?, systems?)` gives flat ground with no trees.
  `SimWorld.FromMapData(map, tuning, ...)` uses the baked grid and trees. `SimWorld.ForRestore(map, core)` is
  internal and used by the save reader (no start layout, no systems stepped).
- `Step(n)` runs n ticks. Each tick advances `Tick`, then runs every system's `Step` in list order. A paused
  world runs nothing on `Step`.
- `Execute(cmd)` runs validate-then-apply immediately, works while paused, never changes `Tick`, and appends to
  `CommandLog`. Rejected commands notify no system.
- Phase order (earliest first): **NavSync (sync) → RoutineSystem (routine) → DispatchSystem (dispatch) →
  StepRunnerSystem (steps) → MovementSystem, DebugSendSystem (movement) → WoodcuttingSystem, HaulSystem,
  ConstructionSystem (operations)**. A new system goes in the block of its phase.
- `SimWorld.FindSystem<T>()` is internal and used by the save code.
- `Camp` (`CampInfo`) is null until the start layout is applied.

## Tuning

`SimTuning` holds pace, speeds, start date, daylight, walk and ox speed, ageing, milling, debug linger, seed, log
length, nav cell size and slope, road and area values, Sunday meal, minimum sleep, dispatch interval, ox load and
unload minutes, builders per site. Some fields are defined but read by nothing (trunk length range, starting oxen,
rest-day switch). See `docs/design/tuning.md`.

The Unity `SimConfig` asset builds a `SimTuning` once at start (decision 0019). The save stores the tuning and catalogs
the world was made with.

## Extending

1. Add state to a new `SimWorld.<Area>.cs` partial, with a null-free default.
2. Add per-tick logic as an `ISimSystem` and place it in `SimSystems.CreateDefault()` in the right phase block.
3. Add its state to a save section in the same task (`docs/systems/save.md`).
4. Add a test in `Sim.Tests/`.

## Gotchas

- Commands run while paused, so a system's `OnCommandApplied` can fire while the world is paused.
- Tests can pass their own system list to `SimWorld` to test ordering.
- `SimTuning.StartingOxen` is not read; the start layout's `OxCount` creates the oxen.
- Obsolete catalog-less overloads of `SideLine` / `CorridorPolygon` and static `Find` lookups remain for the Unity
  layer. Sim code uses `world.RoadTypes` / `world.AreaTypes` (decision 0019).

## Tests

`Sim.Tests/SimWorldTests.cs`, `Core/SimSystemsTests.cs`, `Core/SimRandomTests.cs`, `Core/SimWorldRandomTests.cs`,
`Core/TypeCatalogTests.cs`, `SimTuningTests.cs`, `Commands/CommandPipelineTests.cs`.
