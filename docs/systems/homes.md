# Homes

**Purpose:** a house on a plot along a road, built like any module, that takes one homeless family. A built house can be
fenced, and its household can rise to level 2 (decisions 0040, 0053, 0061). Design: `design/homes.md`, `design/household-levels.md`.

## Key types and files

| Type | File | Role |
|---|---|---|
| `HomeRegistry`, `HomeRecord`, `HomesSection` | `Homes/`, `Save/Sections/` | One record per house: house, plot, family (0 = Empty), fence footprint (0 = none). Section 8. |
| `MoveInSystem`, `HouseRemoval` | `Homes/` | Move-in (operations phase, last). `Evict`, and `RemoveWithPlot` (home, plot, fence). |
| `HouseFence`, `FenceState`, `FenceLine` | `Homes/`, `Areas/FenceLine.cs` | Fence state and removal. The fence line is derived from the plot, never saved. |
| `Family.Level`, `Family.AllotmentFoodTypes` | `People/Family.cs` | Household level, and food types in the last Sunday allotment. |
| `HouseholdLevels`, `NeedDef` | `Homes/HouseholdLevels.cs` | Level table as data. |
| `HouseholdNeedsReport`, `HouseReport`, `HouseholdLevelCounts` | `Homes/` | Read only: needs and Upgrade, panel model, families per level. |
| `DesignatePlot`, `BuildFence`, `UpgradeHousehold` | `Commands/` | Plot and house; fence a built house; raise an eligible family. |
| `ModuleTypes.House`, `ModuleTypes.Fence` | `Footprints/ModuleTypes.cs` | House 6 × 8 m, 10 logs, 10 thatch, 10 panels, 10 daub, 720 min. Fence: 1 m marker, not in `All`. |
| `HousePanelView`, `PlotTool` | `Assets/Game/UI/HousePanel/`, `Assets/Game/Tools/` | House panel and plot tool. |

## Lifecycle

1. **Plot:** `PlotTool` sends `DesignatePlot`. Refused for a bad plot, no room or a short item. Trees under it are cleared.
2. **Build:** the site reserves logs, thatch, panels and daub (owner `-1`, oxen pooled; `construction.md`).
3. **Move in:** a Built house takes a Homeless family with members. Workplace families first, nearest first, else lowest id.
4. **Live and deconstruct:** the family mills at the house access point. Marking evicts it at once. The teardown, or cancelling
   an unbuilt site, runs `RemoveWithPlot`. A plot with a home cannot be deleted.

## Fence (decision 0053)

- The house panel's **Build fence** shows on a Built, unfenced house. `BuildFence` also refuses a house being taken down,
  a plot with no fence line, or short panels.
- It adds a `footprint.fence` marker at the frontage access point, a site with panels reserved by `SiteMaterials`, and sets `FenceFootprintId`.
- Recipe per plot: `ceil(length / FencePanelLengthM)` panels, `FenceMinutesPerPanel` each. The line is every plot edge except road frontage.
  Only a Built fence blocks `NavGrid` (`navigation.md`); `ModuleRenderer.AddFences` draws it once built.

## Household levels (decision 0061)

- **Level 2 needs:** DiverseFood (last allotment has `DiverseFoodTypes` food types, not Hungry), FencedPlot (fence Built),
  Water and Church (always met).
- `HouseholdNeedsReport.Of` derives them on each read. Eligible: housed, not in passage, below the top level, all needs met.
- `UpgradeHousehold` re-checks, raises `Family.Level` by one and logs it. Free in M21 (`UpgradeCost` is empty).

## Extending

- A second family per house, or a family choice: `HomeRegistry.MoveIn` and `MoveInSystem.Choose`.
- A new tier: a `FootprintTypeDef` in `ModuleTypes`, plus a plot type in `AreaTypes` if the shape differs.
- A new level or need: a `HouseholdLevels` entry and a `NeedKind` case in `HouseholdNeedsReport.Check`.

## Gotchas

- The tent spot is not saved (an evicted family's home point is the camp fire, or `RoughSleep.Spot`). `MoveInSystem` retries every tick; families with no members never move in.
- Old sites keep their older recipe: logs only (`HouseLogRecipe`), or no daub (`HouseNoDaubRecipe`). See `construction.md`.
- `AllotmentFoodTypes` is 0 until the first Sunday allotment, so DiverseFood cannot be met before then.

## Tests

- Sim: `Homes/HouseholdLevel*Tests`, `HouseReportTests`, `UpgradeHouseholdTests`, `BuildFenceTests`, `DesignatePlotTests`,
  `HouseConstructionTests`, `HouseDeconstructionTests`, `HouseMaterialsTests`; `Areas/FenceLineTests`, `Navigation/FenceNavTests`, `Food/AllotmentTests`.
- End to end: `Scenarios/DaubFenceLevel2ScenarioTests` (daub, house, fence, level 2; audited, saved mid-fence).
- Unity: PlayMode `HousePanelTests`, `TopBarTests`, `GameSceneTests`. Round trip with house, fence and level 2 family: `DaubFenceLevel2ScenarioTests` (T-626).
