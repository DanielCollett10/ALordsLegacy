# Homes

**Purpose:** a house on a plot along a road, built like any module, that takes one homeless family. The family's home
point moves to the house; a family with a house is no longer Homeless (homes.md HM-*, decision 0040).

## Key types and files

`Assets/Sim/Homes/` (`LordsLegacy.Sim.Homes`), plus the plot and house code in `Areas/`, `Commands/`, `Footprints/`.

| Type | File | Role |
|---|---|---|
| `HomeRegistry`, `HomeRecord` | `Homes/HomeRegistry.cs` | One record per house: house footprint id, plot area id, family id (0 = Empty). `SimWorld.Homes`. `Version` bumps on change. |
| `MoveInSystem` | `Homes/MoveInSystem.cs` | Operations phase, last. Each Empty, Built house takes a Homeless family (`Choose`, pure). |
| `HouseRemoval` | `Homes/HouseRemoval.cs` | `Evict` (family out, house Empty) and `RemoveWithPlot` (home and plot area go). |
| `Family.HomeHouseId` | `People/Family.cs` | Set by the registry. `IsHomeless` is `!HomeHouseId.HasValue`. |
| `DesignatePlot` | `Commands/DesignatePlot.cs` | Plot, house site and Empty home in one all-or-nothing `Apply`. |
| `HousePlacement`, `HousePlacementResult` | `Areas/HousePlacement.cs` | Pure: puts the 6 × 8 m house on the longest road-frontage edge, 1 m back. |
| `AreaTypes.Plot` (`area.plot`) | `Areas/AreaTypes.cs` | Building, 4–8 points, 60–400 m², 10° max slope, needs road frontage. |
| `ModuleTypes.House` | `Footprints/ModuleTypes.cs` | 6 × 8 m, blocks movement, recipe 10 logs / 720 worker-minutes. |
| `HomesSection` | `Save/Sections/HomesSection.cs` | Save section id 8 (see `save.md`). |
| `HousePanelView`, `PlotTool` | `Assets/Game/UI/HousePanel/`, `Assets/Game/Tools/` | Unity side: house panel and the build-bar plot tool. |

## Lifecycle

1. **Plot:** `PlotTool` (build bar `house`) draws the plot with `AreaTool` and shows the house ghost from `HousePlacement`.
   Confirm sends `DesignatePlot`. Validate refuses an invalid plot, no room, or fewer logs than the recipe ("Not enough logs").
   Trees under the plot are cleared.
2. **Build:** the house is a construction site with logs reserved. Its deliver and build tasks use owner `HouseOwnerId` (-1)
   and pool oxen (`construction.md`).
3. **Move in:** when the house is Built, `MoveInSystem` picks a Homeless family with members. Families with a workplace
   come first, nearest workplace to the house; else the lowest family id. The family's home point becomes the house's access point.
4. **Live:** the routine has a housed family mill about its house access point, not the camp (`RoutineSystem`), and commute
   from it (`WorkDay`). `CampRenderer` draws a tent only for Homeless families.
5. **Deconstruct:** marking a built house evicts its family at once (`HouseRemoval.Evict`, home point to the camp fire).
   Finishing the deconstruction, or cancelling an unbuilt site (`RemoveModule`), calls `RemoveWithPlot`: home and plot go.
6. **Delete plot:** `DeleteArea` refuses a plot that still has a home: "Deconstruct the house first".

## Extending

- A second family per house, or a choice of family: change `HomeRegistry.MoveIn` and `MoveInSystem.Choose`, not the callers.
- A new house tier: a new `FootprintTypeDef` in `ModuleTypes`, plus a plot type in `AreaTypes` if its shape differs.
- Any new saved home state goes in `HomesSection` and `SimSaveRoundTripTests` in the same task (decision 0034).

## Gotchas

- The family's old tent spot is not saved. An evicted family's home point becomes the camp fire, so its tent is drawn at the fire.
- The plot's refusals come from `AreaValidator` ("A plot must face a road", "Too small for a house") before `HousePlacement`
  runs. `HousePlacementResult.Text` is always "No room for the house".
- `MoveInSystem` retries every tick, so an Empty house fills as soon as a Homeless family exists.
- Families with no members never move in.
- Houses are footprints, so `ModuleRenderer` draws them, but they are not operation modules (HM-20).
- House deconstruct tasks have owner -1, not an operation id. Keep that in mind when reading the task board.

## Tests

- Sim: `Sim.Tests/Homes/DesignatePlotTests` (move-in rules), `HouseConstructionTests`, `HouseDeconstructionTests`
  (evict, plot refusal, cancel). `Sim.Tests/Areas/HousePlacementTests`, `PlotTypeTests`. `Sim.Tests/People/RoutineSystemTests` (housed mill).
- Unity: EditMode `BuildEntryStateTests`, `VisualRegistryTests`. PlayMode `PlotToolTests`, `HousePanelTests`, `HouseVisualsTests`,
  `ToolControllerTests`, `GameSceneTests` (house wiring).
- Gap: no round-trip test saves a house with a family (see `save.md`).
