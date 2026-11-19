# Shared infrastructure: use these, don't rebuild them

Before writing a new type, check this list. If your need is here, extend the listed piece; if it almost fits, extend it
rather than copy it. A review fails a task that duplicates one of these. Items marked (M22x) arrive in that milestone.
Architecture tests (`Sim.Tests/Architecture`, `Assets/Tests/EditMode/Architecture`) enforce the rows marked *tested*.
Copies of code are found by `pwsh Tools/dup-scan.ps1` (jscpd, decision 0062); run it before reporting a large task.

## Sim

| Need | Use | Doc |
|---|---|---|
| A placeable thing on the build bar, its category, label and tooltip | `BuildCatalog`, `ToolInfo` (M22a) *tested* | decision 0059 |
| A building's display name ("Sawyers yard 2") | `BuildingNames` (M22b) | |
| Balancing numbers (costs, durations, yields, sizes) | `SimTuning` + mirror rule; never literals *tested* | decision 0060 |
| A workplace with families and slots | `OperationTypeDef`, `OperationRegistry`, `OperationStatus` | systems/operations.md |
| A building that turns inputs into outputs | `WorkshopDef` + `WorkshopSystem`, `InputSources`, `InputReserve` | decision 0047 |
| A recipe, affordability, "Not enough X" | `ConstructionRecipe`, `BuildCost`, `RecipeAffordability`, `SiteMaterials` | systems/construction.md |
| Unbuilt state, build progress, removal | `ConstructionRegistry`, `ConstructionProgress`, `DeconstructionRegistry` | decision 0033, 0039 |
| Stock, stores, reservations, piles | `Ledger` (locations, reservations, journals) | decision 0032, 0035 |
| Village totals | `GoodsStats`, `VillageStockReport` (M22a) | |
| Work for citizens | `TaskDef` + `TaskDefCatalog` + `TaskStep`; dispatch via `DispatchSystem` | decision 0005, 0031 |
| Moving items by hand or ox | `CollectItems`/`DepositItems`, ox steps, `OxPool`, `KitLoans` | systems/tasks.md |
| Yard and granary pickup rules | `IYardJobSource` list (`YardJobs.Standard()`) | decision 0054 |
| Placement yes or no | `PlacementRules`, `ModulePlacement`, `DraftModulePlacement` | decision 0028 |
| Removing trees under something | `TreeClearing` | systems/forestry.md |
| Road access, same network (M22d) | `RoadAccess` | design/road-access.md |
| Debug trace | `SimLog` (M22a); player messages use `SettlementLog` | decision 0058 |
| Randomness | `SimRandom` (seeded) | decision 0025 |
| Anything a panel shows | a `*Report.Of(world, id)` read model *tested* | ui.md UIS-02 |

## Unity

| Need | Use | Doc |
|---|---|---|
| A building panel | `PanelFrame`, `PanelManager`, `FamilySlotsSection` (M22b) *tested* | decision 0057 |
| A tooltip | `TooltipService` (M22a) | ui.md UIS-05 |
| A confirm prompt | `ConfirmDialog` | systems/unity-ui.md |
| A new placement tool | `ITool` with `TryPopStep`, `ToolController.Back()`; reuse `StructureTool`, `OperationDraftTool`, `AreaTool` before writing a tool | ui.md UIT-01 |
| Focus and keyboard safety | `UiFocusRules` (the frame applies it) | |
| Drawing on the ground | `TerrainDrape` | decision 0020 |
| Art by id | `VisualRegistry`, `PlaceholderVisualGenerator` | decision 0009, 0010 |
