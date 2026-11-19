# Construction

**Purpose:** a module with a non-empty recipe is placed as a site. Logs are delivered by ox, other items (planks) by hand, then built by workers. When the
last log is in and the work is done, the site becomes a built footprint (decision 0033, `docs/design/construction.md`).

## Key types and files

| Type | File | Role |
|---|---|---|
| `ConstructionRegistry` | `Assets/Sim/Construction/` (`SimWorld.Construction`) | One `ConstructionSite` per unbuilt footprint. No record means built. |
| `ConstructionSite` | `Construction/ConstructionSite.cs` | Delivering / Building state, reservation ids, progress minutes, `Consumed` counts per item (`LogsConsumed` = logs), priority. |
| `ConstructionSources` | `Construction/ConstructionSources.cs` | Logs: stockpile, built log storage, unowned piles. General items (planks): goods stores only. `TryReserve` is nearest-first, all or nothing. |
| `SiteMaterials` | `Construction/SiteMaterials.cs` | Reserves every recipe item at once, nearest first, all or nothing (`TryReserveAll`). `Shortfall` gives "Not enough planks (have N, need M)". Used by every placement path (T-514, T-515). |
| `SiteRecipe` | `Construction/SiteRecipe.cs` | The recipe a footprint is built to now. An old stall that holds logs uses the 1-log recipe (PL-13); an old house holding logs but no thatch or panels uses `ModuleTypes.HouseLogRecipe` (10 logs, MAT-40b). Derived, nothing saved. House: 10 logs + 10 thatch + 10 panels + 10 daub (HL-04), 720 min; thatch, panels and daub are hand-carried (1, 2 and 2 a trip, `HandLoads`). |
| `BuildCost` | `Construction/BuildCost.cs` | Read-only: `MinimumFor(OperationTypeDef)` (8 for woodcutting), `Of(moduleTypeIds)`, `CanAfford` (decision 0037). |
| `ConstructionProgress` | `Construction/ConstructionProgress.cs` | `Of(world, footprintId)` (null when built) and `Work(...)`, which adds build minutes. |
| `ConstructionSystem`, `ConstructionTaskDefs` | `Assets/Sim/Operations/`, `Assets/Sim/Tasks/` | Operations phase, after `HaulSystem`. Opens and cancels `task.deliver_materials` and `task.build`. |
| `SetSitePriority` | `Assets/Sim/Commands/` | Normal or High, also on the site's open tasks. |
| `DeconstructionRegistry`, `DeconstructionWork` | `Construction/` | Entries per marked built module (progress minutes) and pending operation ids. Static `Finish`, `TotalMinutes`, `RefundLogs`. |
| `MarkForDeconstruction`, `CancelDeconstruction`, `DeconstructOperation` | `Assets/Sim/Commands/` | Mark a module, cancel a mark, or mark every module of an operation. |

## Life of a site

1. **Place:** every placement path reserves the whole recipe with `SiteMaterials.TryReserveAll` (or refuses with `Shortfall`); footprint, site and reservations go in one `Apply`.
2. **Deliver:** `ConstructionSystem` opens `task.deliver_materials` for logs (`OxLoadLogs` per trip, by ox) and `task.carry_materials` for other items (`HandCarryPlanks` per trip, on foot; `StepRunnerSystem.Carry.cs`). A failed or sunset carry drops a site pile. Stall carries go to the granary family (T-518). The site turns Building at its first item.
3. **Build:** up to `MaxBuildersPerSite` (4) `task.build` tasks; each adds a minute per tick (`ConstructionProgress.Work`). Cap = recipe minutes × delivered ÷ needed, over all items (on site + `Consumed`).
4. **Built:** at the full total the site record is removed and the footprint counts as built (`world.IsBuilt`).
5. **Cancel:** a cancelled deliver/carry hands its items back. A removed site cancels its tasks; its logs go to an unowned pile.
6. **Deconstruct (T-393, T-394; decision 0039):** a marked built module gets up to 4 `task.deconstruct` tasks (HelpersOnly, heavy Work = recipe minutes × `DeconstructTimeShare`; hut 480). `DeconstructionWork.Finish` removes the footprint and drops every item at `DeconstructRefundShare` (`Refunds`, T-516), plus storage contents, as one unowned pile.
   A 0-minute recipe (log storage) is removed when marked; an unbuilt module is cancelled at once. Cancelling a mark loses
   its progress. Marking a hut unassigns its families (CN-56).
7. **Houses (T-407, 0040):** a marked house evicts its family to the camp fire; finish or unbuilt cancel removes its plot (HM-14b). Deliver and build tasks use owner -1 (`DeconstructionWork.HouseOwnerId`) and pool oxen; the house is built like any module.
8. **Granary (M14, 0041):** tasks use owner -1 like a house's. Its deconstruction removes its operation (`food.md`).

## Rules

- Builders work where they stand (no walk-to-site step yet). Dispatch breaks ties by distance to the site.
- A Build is not re-offered until more logs arrive. Unfinished progress is kept overnight. Odd-jobbers follow the working day (`people.md`).
- At sunset a deliver still under way is wound down as a site-owned pile where the ox stands. Build does not wind down. Site-owned piles are not construction sources for other sites.
- `AssignFamily` refuses while the hut is unbuilt; the panel disables Assign with the same text.

## Extending

A new recipe item is data on the footprint type. A new site step needs a task def, a line in `ConstructionSystem` and save rules (`save.md`). Progress rules belong in `ConstructionProgress`.

## Gotchas

- Footprint and operation ids share the ledger owner field. A pile owned by a site has the site's footprint id as owner.
- Removing an **unbuilt** site drops its logs with no refund; built modules refund 50%. Artisan steps (PPL-42) are not built.

## Tests

`Sim.Tests/ConstructionSiteTests.cs`, `ConstructionSourcesTests.cs`, `Operations/Construction*Tests.cs`, `PlaceRemoveConstructionTests.cs`,
`SitePriorityTests.cs`, `Construction/BuildCostTests.cs`, `SiteMaterialsTests.cs`, `MultiItemConstructionTests.cs`, `Deconstruct*Tests.cs`,
`Homes/House*Tests.cs`. Unity: `Assets/Tests/PlayMode/ConstructionVisualsTests.cs`.
