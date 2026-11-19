# Sim core

**Purpose:** the root of all game state, the fixed tick order, the seeded random source and the balancing numbers.

## Key types and files

| Type | File | Role |
|---|---|---|
| `SimWorld` | `Assets/Sim/Core/SimWorld.cs` | Root. Tick clock, pause, `Terrain`, `Trees`, `Roads`, `Areas`, `Footprints`, `Operations`, `People`, `Tasks`, `Inventory`, `Construction`, `Homes`, `Bushes`, `Nav`, `Random`, `Camp`, `CommandLog`, `SettlementLog` (last 20 tick + text entries, saved in Core; T-498), `Log` (`SimLog` debug ring buffer, not saved; T-628). Never-null registries. |
| `SimWorld.<Area>.cs` | `Assets/Sim/Core/` | One partial per subsystem (for example `SimWorld.Foraging.cs` for `Bushes`). Add a new one, don't grow `SimWorld.cs`. |
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
- Phase order (earliest first): sync `NavSyncSystem` → routine `RoutineSystem`, `EatingSystem`, `AllotmentSystem`,
  `FoodTripSystem` → dispatch `DispatchSystem` → steps `StepRunnerSystem` → movement `MovementSystem`, `DebugSendSystem` →
  operations `WoodcuttingSystem`, `HaulSystem`, `ConstructionSystem`, `MoveInSystem`, `BushRegrowSystem`, `ForagingSystem`.
- `SimWorld.FindSystem<T>()` (internal, used by save) and `Camp` (`CampInfo`, null until the start layout is applied).

## Tuning

`SimTuning` holds pace, calendar, daylight, speeds, walking, ageing, sleep, construction, timber, roads, areas, food
(starting meat, rations, market range) and foraging (bush baskets and regrow, forager age and hut capacity, gather time).
Some fields are read by nothing (trunk length range, starting oxen, rest-day switch). See `docs/design/tuning.md`.
The Unity `SimConfig` asset builds it once at start (decision 0019). The save stores the tuning it was made with.

## Extending

1. Add state to a new `SimWorld.<Area>.cs` partial, with a null-free default.
2. Add per-tick logic as an `ISimSystem` and place it in `SimSystems.CreateDefault()` in the right phase block.
3. Add its state to a save section in the same task (`docs/systems/save.md`).
4. Add a test in `Sim.Tests/`.

## Gotchas

- Commands run while paused, so `OnCommandApplied` can fire while the world is paused. Tests can pass their own system list.
- `SimTuning.StartingOxen` is not read; the start layout's `OxCount` creates the oxen.
- Module recipes and sizes (`ModuleTypes`) are static data and do not read `SimTuning` (house, daub pit, fence marker). The workshop defs do read it. See `thatch.md`, `workshops.md`.
- Obsolete catalog-less overloads and static `Find` lookups remain for the Unity layer. Sim code uses `world.RoadTypes` / `world.AreaTypes` (decision 0019).

## Tests

`Sim.Tests/SimWorldTests.cs`, `Core/SimSystemsTests.cs`, `Core/SimRandomTests.cs`, `Core/SimWorldRandomTests.cs`,
`Core/TypeCatalogTests.cs`, `SimTuningTests.cs`, `Commands/CommandPipelineTests.cs`.
