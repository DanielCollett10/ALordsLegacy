# Tasks

**Purpose:** the unit of work a worker does: a list of steps (walk, work, ox moves) with a state, a priority and reservations.
Operations create tasks; the dispatcher assigns them; the step runner performs them (decision 0005, decision 0031).

## Key types and files

`Assets/Sim/Tasks/` (`LordsLegacy.Sim.Tasks`). Registry `TaskBoard` (`SimWorld.Tasks`, never null).

| Type | Role |
|---|---|
| `TaskDef` | Id, explicit `TaskStep` list, `EligibilityPolicy` (for example `AssignedThenHelpers`, `HelpersOnly`), `RequiredResource` (`Ox`, …). |
| `TaskDefCatalog` | Looks a def up by id. Saves refer to defs by id; an unknown id fails the load. |
| `ConstructionTaskDefs` | `task.deliver_materials` (HelpersOnly, Ox, heavy load/unload) and `task.build` (HelpersOnly, heavy Work), and `task.deconstruct` (HelpersOnly, heavy Work, T-394). |
| `FoodTripSystem` (`Food/`) | `task.fetch_food` (T-418, FD-14): policy `OwnerFamily`, owner `-1000-familyId`; opened per household over the Sunday morning, run by `DispatchSystem.RunFamilyTrips` and `StepRunnerSystem.Food.cs` (`WalkTo`, `CollectFood`, `DepositFood`; ignores shifts; carrier = `Family.TripCarrierId`, Food section). Text "Fetching food". |
| `TaskStep` | `MoveTo`, `Work(duration)` (with heavy flag and stage id), and ox steps `FetchOx`, `LeadTo`, `LoadOx`, `UnloadOx`, `ReturnOx`. |
| `TaskRecord` | Id, def, state, owner, assignee, priority, subject (module name), target, step index, `OxId`, `AwaitsTaskId`, `WindingDown`, `SiteFootprintId`. |
| `TaskPriority` | Normal (default) or High. Changed only by `TaskBoard.SetPriority`. |
| `DispatchSystem` | Dispatch phase. Hands Open tasks to idle workers. |
| `StepRunnerSystem` (+ `.Ox.cs`) | Steps phase. Runs Active tasks in id order. Ox steps are in the partial. |
| `CitizenTaskText` | Text for the citizen panel. |

## State machine

Open → Assigned → Active → Done, or Cancelled. Illegal transitions return false. `TaskBoard` also offers `ByOwner`,
`ByAssignee`, `InState`, `IsBusy(citizenId)` (Assigned or Active) and a `Version`.

Tasks reserve trees in `Trees` with the task id as owner. `Cancel` and completion release them.

## How data moves

1. An operation or system opens a task (`WoodcuttingSystem`, `HaulSystem`, `ConstructionSystem`). House deliver and build tasks use owner -1 (`DeconstructionWork.HouseOwnerId`), not an operation id.
2. `DispatchSystem` runs every `DispatchIntervalMinutes` (10). An idle worker (awake, no debug send, not busy, not mid-walk
   unless milling) takes the best Open task: pass 1 for the family whose workplace owns it, pass 2 for helper families.
   Ranking: priority, then travel to the job (tree, ox, or site), then id. A heavy task needs age 14+. A task that needs an
   ox is offered only once it has one. It reserves the tree, then `Assign` and `Start`.
3. `StepRunnerSystem` runs the current step. `MoveTo` issues a `MoveOrder` and waits (Walking). `Work` adds one progress
   tick per tick (Working). Ox steps drive the ox (decision 0036).
4. Felling outcomes run from the step runner (`FellingOutcome`); build steps add progress through
   `ConstructionProgress.Work`. A target gone, no assignee, or a failed move cancels the task and frees the worker.
5. A worker off shift has its Active task skipped (not cancelled), and `RoutineSystem` takes the worker home.

Haul and construction tasks carry extra rules (wind-down at the end of work, early fetch): see `operations.md` and `construction.md`.

## Extending

- A new task type is a `TaskDef` in code (for now), with explicit steps. Add its id to `TaskDefCatalog` so saves load.
- A new step kind is a `TaskStep` factory and a branch in the step runner. It must save its state, or be rebuilt on load.
- Never store a worker's task in the people registry. The board owns it.

## Gotchas

- `RoutineSystem` ignores busy citizens and resumes when the task ends.
- A task that needs an ox is not offered to a worker who is mid-tree (the worker is busy).
- Runtime step state (walk orders, ox tracks, rest targets) is not saved. Only the task record and its steps are (`save.md`).

## Tests

`Sim.Tests/TaskBoardTests.cs`, `Sim.Tests/Tasks/` (`DispatchAndStepRunnerTests`, `OxStepTests`, `CitizenTaskTextTests`,
`ConstructionTaskDefsTests`), `Sim.Tests/Operations/HaulSystemTests.cs`, `Sim.Tests/Save/TasksSectionTests.cs`.
