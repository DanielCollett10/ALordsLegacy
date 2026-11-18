# Footprints and module types

**Purpose:** the built or planned physical objects on the map: huts, storage, stands, the camp. A footprint is an oriented
rectangle with an access point. It blocks or clears movement and holds a construction recipe if it needs materials
(decision 0033 for sites).

## Key types and files

`Assets/Sim/Footprints/` (`LordsLegacy.Sim.Footprints`) and `SimWorld.Footprints.cs`.

| Type | Role |
|---|---|
| `Footprint` | Oriented rectangle: CCW `Polygon`, centre, rotation, `AccessPoint` 1 m outside the front edge (local −Z). Type id. |
| `FootprintTypeDef` | Size, `BlocksMovement`, `ClearsTrees`, `LogCapacity`, `OxCapacity` (both 0 by default), `ConstructionRecipe`. |
| `FootprintRegistry` (`SimWorld.Footprints`) | `FindAt`, `QueryRect`, `Ids`, `NextId()`, `Version` (bumps on add or remove). |
| `ModuleTypes` | Module types (`All`): woodcutting hut 6×4 m (blocks), log storage 8×6 m (20 logs), oxen stand 3×3 m (1 ox), house 6×8 m (blocks, M13), granary 8×6 m standalone with a 5 m yard (food store 100, M14), forager hut 6×4 m (food store 30, M14), market stall 3×2 m (2 planks, 60 min, food store 30, no blocking; M15), storage yard 8×6 m with a 3 m yard margin (8 logs, 480 min, General store 300; M18). |
| `ConstructionRecipe` | Item counts and build worker-minutes. Hut 8 logs / 960 min, oxen stand 1 / 60, house 10 / 720, granary 8 / 600, forager hut 8 / 480, log storage and camp empty (built at placement). |

`module.market_stall` (T-459): 3×2 m, non-blocking, 2 planks / 60 min (T-519), food store 30. No operation: `Markets/StallSystem` (operations
phase) queues one per store family on the free `StallGrid` slot nearest the centre, reusing an unstaffed stall first (MK-20..23).
`Market.FamilyOf(stall)` is the staffing family (0 = unstaffed, saved); `NoRoomFamilyIds` is recomputed each tick. Deleting the market
drops stall food as ground piles. Its site tasks use owner -1 like a house. Tests: `Markets/StallSystemTests.cs`.

`footprint.camp` is 24×16 m, walkable, and clears trees.

## Rules

- Footprint ids come from `NextId()` and are never reused.
- A blocking footprint is a movement block for `NavGrid` (see `navigation.md`).
- A footprint with a non-empty recipe is not built until its site is built (`ConstructionRegistry`). `IsBuilt(id)` is true when
  there is no site record.

- **Standalone structure** (granary, T-422): a type with `YardMarginMm > 0`; `Footprint.PlotPolygon` is building + yard. Placed
  by `PlaceStructure` after `StructurePlacement.Preview` (whole plot clear of footprints, areas, roads). Only the building
  blocks the nav grid. Its food-only store is `Ledger.FindFoodStore(footprintId)`, created at placement (capacity from the
  type); deconstruction drops the contents as a ground pile. No save layout change: type id, centre, rotation are saved.

## Extending

New buildings are a `FootprintTypeDef` plus a `ModuleTypes` entry. Set the recipe for construction. Put the rule in the type,
not in the placement code.

## Gotchas

- The access point is 1 m outside the front edge. Woodcutting measures from the hut's access point when it picks trees.
- `DeleteArea` removes an operation's module footprints but not their ledger locations (see `operations.md`).

## Tests

`Sim.Tests/FootprintTests.cs`, `Sim.Tests/Operations/PlaceRemoveConstructionTests.cs`, `Sim.Tests/Save/StructuresSectionTests.cs`.
Standalone granary: `Sim.Tests/Placement/GranaryPlacementTests.cs`, `Sim.Tests/Operations/GranaryOperationTests.cs`.
Fixed plot outlines (PL-10..13): `Placement/FixedPlots` reads each standalone footprint's PlotPolygon for area overlap ("Overlaps Granary <id>"), vertex rules and snaps; tests `Sim.Tests/Placement/FixedPlotTests.cs`. Save version 18.
Workshops (T-521, HW-01/PL-01): `module.hewing_station` and `module.sawpit` (6 x 4 m, 3 m yard, 6 logs / 480 min) are standalone like the granary; `PlaceStructure` also creates their area-less 1-slot operation (`operation.hewing`/`operation.sawing`, def via `OperationTypes.WorkshopOf`). `WorkshopBuildings.OnBuilt` makes the WorkshopStock and GoodsStore when the site finishes; deconstruction drops both as unowned piles. Tests `Sim.Tests/Operations/WorkshopPlacementTests.cs`.
- Weaver and wattle yard (T-561, MAT-20..24): `module.weaver` / `module.wattle_yard` (same 6 x 4 m, 6 logs / 480 min as the hewing station), operations `operation.weaving` / `operation.wattling`, defs `WorkshopDef.Weaving` / `Wattling` (hand-carried rods, numbers fixed in the def, not tuning). Tests `Sim.Tests/Workshops/WeaverWattleYardTests.cs`. Build-bar entries come in cw-14.
- Kit stores (T-563, decision 0052, ST-20..24): `Kit/KitStores.OnBuilt` (called from `WorkshopBuildings.OnBuilt`) gives a built forager hut / granary a GoodsStore (ModuleId = building) capped at `ForagerHutBasketSlots` / `GranaryBasketSlots` baskets. `ConstructionSources` skips it; village totals count it. Claims: `world.KitClaims` (building + first-claim tick, `Ordered()` oldest first), saved in the Structures section; save version 36. Tests `Sim.Tests/Kit/KitStoreTests.cs`.
