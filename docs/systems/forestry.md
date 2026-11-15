# Forestry (trees)

**Purpose:** the standing trees, their felling state and their reservations. Trees come from the baked map and are
changed only by commands and felling.

## Key types and files

`Assets/Sim/Forestry/` (`LordsLegacy.Sim.Forestry`).

| Type | Role |
|---|---|
| `TreeRegistry` (`SimWorld.Trees`) | All trees keyed by `TreeId`, in a uniform 16 m spatial grid. `QueryRect`, `QueryRadius`, `NearestUnreservedStanding(x, z, region)`, `AnyStandingIn`, `CountStandingIn`, `LogYield`, `Felled`, `Stumps`, `Remove`, `Reserve`, `Release`. `Version` bumps on any Add or Remove. |
| `TreeRecord` | Id, x/z, usable trunk length, `State` (Standing → Felled → Stump, forward only via `SetState`), `ReservedBy`. |
| `TreeId` | Typed id. |
| `TreeRegion` | Circle or polygon, for queries. |
| `TreeClearing` (static) | `RemoveInPolygon` and `RemoveNearPolyline`, geo-1 on trunk positions rounded to mm. Returns the removed ids sorted. |

## How data moves

- The map gives the initial trees (`MapTree`). The save stores only changes against them (removed ids, felled or stump
  state, reservations).
- Clearing happens in `DesignateArea` (building kinds), `DrawRoad` (road width + `RoadTreeClearanceM`), `PlaceModule`
  (under the footprint), and the start layout (under the camp). Each returns the removed ids.
- Felling (`WoodcuttingSystem`, `FellingOutcome`) sets Felled, then Stump. See `operations.md`.
- Tasks reserve a tree with their own id as owner. A reservation is released on cancel or completion.

## Rules

- `Count`, `All` and the queries list standing trees only. `Felled` and `Stumps` are for the renderer.
- A removed id can never be added again (BLD-10-D).
- Leaving Standing cancels the tree's reservation.

## Extending

Add a new tree state only with a forward-only rule and a save update. Anything that changes trees must bump `Version`
through `Add`, `Remove` or `SetState`, or the renderer will not update.

## Gotchas

- `NearestUnreservedStanding` ignores trees reserved by someone else. Callers must not assume the nearest tree is free.
- Unity draws trees from `TreeRegistry` with no GameObjects (decision 0008). The Unity `TreeAuthoringData` asset is only the
  bake input.

## Forage bushes (T-421, FD-20..21)

`Assets/Sim/Foraging/`: `BushRegistry` (`world.Bushes`: reserve/release/`TakeBasket`/`NearestAvailable`), `BushScatter` (new game
only, own seeded `SimRandom`, forest edges plus a few clearings), `BushRegrowSystem` (full after `BushRegrowDays`). Tuning:
`BushBaskets` 3, `BushRegrowDays` 14, `BushCount`, `BushEdgeM`, `BushTreeClearM`, `BushClearingChance`. Saved by `BushesSection`.
Tests: `Save/BushesSectionTests.cs`. No picking task or renderer yet.

## Tests

`Sim.Tests/TreeRegistryTests.cs`, `TreeStateTests.cs`, `TreeClearingTests.cs`, `Roads/RoadTreeClearanceTests.cs`,
`Operations/StorageFullFellingTests.cs`. Unity: `Assets/Tests/PlayMode/TreeRendererTests.cs`.
