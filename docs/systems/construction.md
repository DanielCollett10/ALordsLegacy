# Construction

**Purpose:** a module with a non-empty recipe is placed as a site. Logs are delivered by ox, then built by workers. When the
last log is in and the work is done, the site becomes a built footprint (decision 0033, `docs/design/construction.md`).

## Key types and files

| Type | File | Role |
|---|---|---|
| `ConstructionRegistry` | `Assets/Sim/Construction/` (`SimWorld.Construction`) | One `ConstructionSite` per unbuilt footprint. No record means built. |
| `ConstructionSite` | `Construction/ConstructionSite.cs` | State Delivering / Building, reservation ids, `Site` ledger location id, progress minutes, `LogsConsumed`, priority. |
| `ConstructionSources` | `Construction/ConstructionSources.cs` | `Available(world, item)` sums stockpile, Built log storage and unowned piles. `TryReserve` reserves nearest-first, all or nothing. |
| `ConstructionProgress` | `Construction/ConstructionProgress.cs` | Read API: `Of(world, footprintId)` (null when built) and `Work(...)`, which adds build minutes. |
| `ConstructionSystem` | `Assets/Sim/Operations/ConstructionSystem.cs` | Operations phase, after `HaulSystem`. Opens and cancels deliver and build tasks. |
| `ConstructionTaskDefs` | `Assets/Sim/Tasks/` | `task.deliver_materials`, `task.build` (see `tasks.md`). |
| `SetSitePriority` | `Assets/Sim/Commands/` | Normal or High. Also sets the priority on the site's open tasks. |

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
- The 50% removal refund (CN-33) is not built. Removing a site moves its logs to the ground with no refund.
- Not built: artisan build steps (PPL-42) and a walk-to-site step.

## Tests

`Sim.Tests/ConstructionSiteTests.cs`, `ConstructionSourcesTests.cs`, `Sim.Tests/Operations/ConstructionBuildTests.cs`,
`ConstructionDeliveryTests.cs`, `ConstructionSunsetDropTests.cs`, `PlaceRemoveConstructionTests.cs`, `SitePriorityTests.cs`.
Unity: `Assets/Tests/PlayMode/ConstructionVisualsTests.cs`.
