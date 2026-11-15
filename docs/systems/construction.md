# Construction

**Purpose:** a module with a non-empty recipe is placed as a site. Logs are delivered by ox, then built by workers. When the
last log is in and the work is done, the site becomes a built footprint (decision 0033, `docs/design/construction.md`).

## Key types and files

| Type | File | Role |
|---|---|---|
| `ConstructionRegistry` | `Assets/Sim/Construction/` (`SimWorld.Construction`) | One `ConstructionSite` per unbuilt footprint. No record means built. |
| `ConstructionSite` | `Construction/ConstructionSite.cs` | Delivering / Building state, reservation ids, progress minutes, `LogsConsumed`, priority. |
| `ConstructionSources` | `Construction/ConstructionSources.cs` | `Available` sums stockpile, built log storage, unowned piles. `TryReserve` is nearest-first, all or nothing. |
| `BuildCost` | `Construction/BuildCost.cs` | Read-only: `MinimumFor(OperationTypeDef)` (8 for woodcutting), `Of(moduleTypeIds)`, `CanAfford` (decision 0037). |
| `ConstructionProgress` | `Construction/ConstructionProgress.cs` | `Of(world, footprintId)` (null when built) and `Work(...)`, which adds build minutes. |
| `ConstructionSystem`, `ConstructionTaskDefs` | `Assets/Sim/Operations/`, `Assets/Sim/Tasks/` | Operations phase, after `HaulSystem`. Opens and cancels `task.deliver_materials` and `task.build`. |
| `SetSitePriority` | `Assets/Sim/Commands/` | Normal or High, also on the site's open tasks. |
| `DeconstructionRegistry`, `DeconstructionWork` | `Construction/` | Entries per marked built module (progress minutes) and pending operation ids. Static `Finish`, `TotalMinutes`, `RefundLogs`. |
| `MarkForDeconstruction`, `CancelDeconstruction`, `DeconstructOperation` | `Assets/Sim/Commands/` | Mark a module, cancel a mark, or mark every module of an operation. |

## Life of a site

1. **Place:** `PlaceModule` checks the recipe (`ModulePlacement` refuses "Not enough logs (have N, need M)"). Footprint,
   Delivering site and reservations (owner = footprint id) are added in one `Apply`.
2. **Deliver:** `ConstructionSystem` opens `task.deliver_materials` for logs reserved to the site: up to `OxLoadLogs` per trip.
   Open tasks ask `OxPool.FindFor` each tick. The site turns Building at its first log on site.
3. **Build:** up to `MaxBuildersPerSite` (4) `task.build` tasks at once. Each builder adds a minute per tick
   (`StepBuild` → `ConstructionProgress.Work`). Cap = recipe minutes × delivered ÷ needed (on site + `LogsConsumed`).
4. **Built:** at the full total the site record is removed and the footprint counts as built (`world.IsBuilt`).
5. **Cancel:** a cancelled deliver hands its logs back. A removed site cancels its tasks; its logs go to an unowned pile.
6. **Deconstruct (T-393, T-394; decision 0039):** a marked built module gets up to 4 `task.deconstruct` tasks (HelpersOnly,
   heavy Work = recipe minutes × `DeconstructTimeShare`; hut 480). `DeconstructionWork.Finish` removes the footprint and
   drops floor(recipe logs × `DeconstructRefundShare`) logs, plus storage contents, as one unowned pile at the site.
   A 0-minute recipe (log storage) is removed when marked; an unbuilt module is cancelled at once. Cancelling a mark loses
   its progress. Marking a hut unassigns its families (CN-56).
7. **Houses (T-407, 0040):** a marked house evicts its family to the camp fire; finish or unbuilt cancel removes its plot (HM-14b). Tasks use owner -1. Gap: no operation, so builders never run.

## Rules

- Builders work where they stand; there is no walk-to-site step yet. Dispatch breaks ties by distance to the site.
- A Build is not re-offered until more logs arrive. Unfinished progress is kept overnight. Odd-jobbers follow the working day (`people.md`).
- At sunset a deliver still under way is wound down as a site-owned pile where the ox stands. Build does not wind down.
  Site-owned piles are not construction sources for other sites.
- `AssignFamily` refuses while the hut is unbuilt; the panel disables Assign with the same text.

## Extending

A new recipe item is data on the footprint type. A new site step needs a task def, a line in `ConstructionSystem` and save
rules (`save.md`). Progress rules belong in `ConstructionProgress`, not the step runner.

## Gotchas

- Footprint and operation ids share the ledger owner field. A pile owned by a site has the site's footprint id as owner.
- Removing an **unbuilt** site (`RemoveModule`) drops its logs with no refund. Only built modules refund (50%). Shares are
  tuning saved in Core. Not built: artisan build steps (PPL-42), walk-to-site.

## Tests

`Sim.Tests/ConstructionSiteTests.cs`, `ConstructionSourcesTests.cs`, `Sim.Tests/Operations/Construction*Tests.cs`,
`PlaceRemoveConstructionTests.cs`, `SitePriorityTests.cs`, `Sim.Tests/Construction/BuildCostTests.cs`, `Deconstruct*Tests.cs`,
`Sim.Tests/Homes/HouseDeconstructionTests.cs`. Unity: `Assets/Tests/PlayMode/ConstructionVisualsTests.cs`.
