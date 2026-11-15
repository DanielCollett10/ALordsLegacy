# Construction

**Purpose:** a module with a non-empty recipe is placed as a site. Logs are delivered by ox, then built by workers. When the
last log is in and the work is done, the site becomes a built footprint (decision 0033, `docs/design/construction.md`).

## Key types and files

| Type | File | Role |
|---|---|---|
| `ConstructionRegistry` | `Assets/Sim/Construction/` (`SimWorld.Construction`) | One `ConstructionSite` per unbuilt footprint. No record means built. |
| `ConstructionSite` | `Construction/ConstructionSite.cs` | State Delivering / Building, reservation ids, `Site` ledger location id, progress minutes, `LogsConsumed`, priority. |
| `ConstructionSources` | `Construction/ConstructionSources.cs` | `Available(world, item)` sums stockpile, Built log storage and unowned piles. `TryReserve` reserves nearest-first, all or nothing. |
| `BuildCost` | `Construction/BuildCost.cs` | Read-only: `MinimumFor(OperationTypeDef)` (mandatory hut and storage recipes, 8 for woodcutting), `Of(moduleTypeIds)` (summed recipe logs), `CanAfford(world, logs)` (CN-11 available logs). Decision 0037. |
| `ConstructionProgress` | `Construction/ConstructionProgress.cs` | Read API: `Of(world, footprintId)` (null when built) and `Work(...)`, which adds build minutes. |
| `ConstructionSystem` | `Assets/Sim/Operations/ConstructionSystem.cs` | Operations phase, after `HaulSystem`. Opens and cancels deliver and build tasks. |
| `ConstructionTaskDefs` | `Assets/Sim/Tasks/` | `task.deliver_materials`, `task.build` (see `tasks.md`). |
| `SetSitePriority` | `Assets/Sim/Commands/` | Normal or High. Also sets the priority on the site's open tasks. |
| `DeconstructionRegistry` | `Construction/` (`SimWorld.Deconstruction`) | `DeconstructionEntry` per marked built module (progress minutes), plus pending operation ids. `SimWorld.IsWorking(id)` is false while marked. |
| `DeconstructionWork` | `Construction/` | Static: `TotalMinutes`, `RefundLogs`, `Remaining`, `Finish` (refund pile, remove), `CompletePendingOperation`. |
| `MarkForDeconstruction`, `CancelDeconstruction`, `DeconstructOperation` | `Assets/Sim/Commands/` | Mark a module (unbuilt: removed at once), cancel a mark, or mark every module of an operation. |

## Life of a site

1. **Place:** `PlaceModule` checks the recipe (`ModulePlacement` refuses "Not enough logs (have N, need M)"). It adds the
   footprint, a Delivering site, and reservations (owner = footprint id), all in one `Apply`.
2. **Deliver:** `ConstructionSystem` opens `task.deliver_materials` per site for logs still reserved for it: up to
   `OxLoadLogs` per trip from one source. Opening moves those logs to a reservation owned by the task. Open tasks ask
   `OxPool.FindFor` each tick. The site becomes Building when its first log is on site.
3. **Build:** up to `MaxBuildersPerSite` (4) `task.build` tasks are live at once, never more than the minutes left. Each builder
   adds one minute per tick (`StepBuild` → `ConstructionProgress.Work`). Progress cap = recipe minutes × delivered ÷ needed,
   where delivered = on site + `LogsConsumed`. Each log share passed consumes one log (`Ledger.Consume`).
4. **Built:** at the full total the site record is removed. Its footprint now counts as built (`world.IsBuilt`).
5. **Cancel:** a cancelled deliver hands its logs back. A removed site cancels its tasks and moves its logs to an unowned pile.
6. **Deconstruct (T-393, T-394; decision 0039):** a built module that is marked gets up to `MaxBuildersPerSite` `task.deconstruct`
   tasks (HelpersOnly, heavy Work of recipe minutes × `DeconstructTimeShare`, hut 480). `StepDeconstruct` adds a minute to the
   entry per tick. `DeconstructionWork.Finish` removes the footprint and puts floor(recipe logs × `DeconstructRefundShare`)
   logs plus any storage contents into one unowned ground pile at the site. A 0-minute recipe (log storage) is removed at once
   when marked. An unbuilt module is cancelled at once. `CancelDeconstruction` removes the entry (progress is thrown away)
   and the system then cancels its tasks. Marking a hut unassigns its families (CN-56).

## Rules

- Builders work where they stand. There is no walk-to-site step yet. The dispatcher breaks ties by distance to the site.
- A Build is not re-offered until more logs have arrived. An unfinished build keeps its progress overnight.
- Odd-jobbers with construction work follow the working day (`people.md`).
- At the end of work, a deliver still under way is wound down as a site-owned pile where the ox stands, with its
  reservation moved to that pile. Build does not wind down.
- Site-owned piles are not construction sources for other sites.
- `AssignFamily` refuses while the hut is unbuilt, and the operation panel disables Assign with the same text.

## Extending

A new recipe item is data on the footprint type. A new site step means a new task def, a line in `ConstructionSystem`, and
its save rules (`save.md`). Put progress rules in `ConstructionProgress`, not the step runner.

## Gotchas

- Footprint and operation ids share the ledger owner field. A pile owned by a site has the site's footprint id as owner.
- Removing an **unbuilt** site (`RemoveModule`) moves its logs to the ground with no refund. Only built modules refund (50%).
- Refund and time shares are tuning values saved in Core. A new game uses the current values; old saves keep theirs.
- Not built: artisan build steps (PPL-42) and a walk-to-site step.

## Tests

`Sim.Tests/ConstructionSiteTests.cs`, `ConstructionSourcesTests.cs`, `Sim.Tests/Operations/ConstructionBuildTests.cs`,
`ConstructionDeliveryTests.cs`, `ConstructionSunsetDropTests.cs`, `PlaceRemoveConstructionTests.cs`, `SitePriorityTests.cs`,
`Sim.Tests/Construction/BuildCostTests.cs`, `Sim.Tests/Operations/DeconstructionMarkTests.cs`, `DeconstructTaskTests.cs`,
`DeconstructOperationTests.cs`.
Unity: `Assets/Tests/PlayMode/ConstructionVisualsTests.cs`.
