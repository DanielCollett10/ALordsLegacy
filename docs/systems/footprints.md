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
| `ModuleTypes` | Module types (`All`): woodcutting hut 6×4 m (blocks), log storage 8×6 m (20 logs), oxen stand 3×3 m (1 ox), house 6×8 m (blocks, M13), granary 8×6 m standalone with a 5 m yard (food store 100, M14), forager hut 6×4 m (food store 30, M14). |
| `ConstructionRecipe` | Item counts and build worker-minutes. Hut 8 logs / 960 min, oxen stand 1 / 60, house 10 / 720, granary 8 / 600, forager hut 8 / 480, log storage and camp empty (built at placement). |

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
