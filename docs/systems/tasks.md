# Tasks

**Purpose:** the unit of work a worker does: a list of steps (walk, work, ox moves, food) with a state, a priority and reservations.
Operations and food systems create tasks; the dispatcher assigns them; the step runner performs them (decision 0005, decision 0031).

## Key types and files

`Assets/Sim/Tasks/` (`LordsLegacy.Sim.Tasks`). Registry `TaskBoard` (`SimWorld.Tasks`, never null).

| Type | Role |
|---|---|
| `TaskDef` | Id, explicit `TaskStep` list, `EligibilityPolicy` (`AssignedFamilyOnly`, `AssignedThenHelpers`, `HelpersOnly`, `OwnerFamily`), `RequiredResource` (`Ox`, …). |
| `TaskDefCatalog` | Looks a def up by id and holds the policy per id. Saves refer to defs by id; an unknown id fails the load. |
| `ConstructionTaskDefs` | `task.deliver_materials` (ox, logs), `task.carry_materials` (on foot, other items), `task.build`, `task.deconstruct` (all HelpersOnly; heavy steps). A market stall's carry is AssignedFamilyOnly for the stall's granary family (age 8+, not heavy, owner = granary op, taken before `stock_stall`; T-518). |
| `FoodTripSystem` (`Food/`) | `task.fetch_food` (OwnerFamily, owner `-1000 - familyId`), opened each Sunday morning. Ignores shifts. See `food.md`. |
| `ForagingSystem` (`Foraging/`) | `task.forage` (AssignedFamilyOnly, owner = operation, age 8+), holds a bush and a carrier. See `foraging.md`. |
| `StoreFamilySystem` (`Markets/`) | `task.stock_stall` (AssignedFamilyOnly, owner = granary operation, age 8+): walk, load 10 min, `CollectItems` (no item id: reserved, in proportion, never allotted food), walk, unload 10 min, `DepositItems`. Source: `ChooseStockSource`. |
| `YardFamilySystem` (`Operations/`), `HandHaulTaskDefs` | `task.haul_by_hand` (T-542, ST-43/44): the one store-to-store hand trip (same steps, hand load, stock and room reserved at planning, owner `OwnerFor`). Plans from the ordered `YardJobs` sources; none registered yet. |
| `WorkshopSystem` (`Operations/Workshops/`) | `task.fetch_input` (AssignedFamilyOnly, owner = operation, `RequiredResource.Ox`: fetch ox, lead, load, lead, unload, return) and `task.workshop_work` (AssignedFamilyOnly, one heavy `Work` step, owner = operation). |
| `TaskStep` | `MoveTo`, `Work(duration)` (heavy flag, stage id), ox steps `FetchOx`, `LeadTo`, `LoadOx`, `UnloadOx`, `ReturnOx`, food steps `WalkTo`, `CollectFood`, `DepositFood`, `DepositForage`, and the one item trip pair `CollectItems`/`DepositItems` (an item id moves that item, none means a stock share). |
| `TaskRecord`, `TaskPriority` | Id, def, state, owner, assignee, priority (Normal or High, set by `TaskBoard.SetPriority`), target, step index, ox, site, `BushId`, `CarrierId`. |
| `DispatchSystem` | Dispatch phase. Hands Open tasks to idle workers, and runs the Sunday family trips (`RunFamilyTrips`). |
| `StepRunnerSystem` (+ `.Ox.cs`, `.Food.cs`, `.Forage.cs`, `.Stock.cs`) | Steps phase. Runs Active tasks in id order. Each partial owns one family of steps. |
| `CitizenTaskText` | Text for the citizen panel. |

## State machine

Open → Assigned → Active → Done, or Cancelled. Illegal transitions return false. `TaskBoard` also offers `ByOwner`, `ByAssignee`, `InState`, `IsBusy(citizenId)` and a `Version`. Tasks reserve a tree or bush with the task id as owner; cancel and completion release it.

## How data moves

1. An operation or system opens a task (`WoodcuttingSystem`, `HaulSystem`, `WorkshopSystem`, `ConstructionSystem`, `ForagingSystem`,
   `FoodTripSystem`, `StoreFamilySystem`). House and granary deliver and build tasks use owner -1 (`DeconstructionWork.HouseOwnerId`), not an operation id.
2. `DispatchSystem` runs every `DispatchIntervalMinutes` (10). An idle worker (awake, no debug send, not busy, not mid-walk
   unless milling) takes the best Open task: pass 1 for the family whose workplace owns it, pass 2 for helper families.
   Ranking: priority, travel to the job, then id. Heavy tasks need age 14+, forage tasks 8+. An ox task is offered only once
   it has an ox. Family trips are handed out first, to the first awake, idle adult of the family.
3. `StepRunnerSystem` runs the current step. `MoveTo` issues a `MoveOrder` and waits (Walking). `Work` adds one progress
   tick per tick (Working). Ox steps drive the ox (decision 0036). Food steps move ledger stock (`food.md`); forage steps
   produce items on the carrier (`foraging.md`).
4. Felling outcomes run from the step runner (`FellingOutcome`); build steps add progress through
   `ConstructionProgress.Work`. A target gone, no assignee, or a failed move cancels the task and frees the worker.
5. A worker off shift has its Active task skipped (not cancelled), and `RoutineSystem` takes the worker home.
   Food trips ignore shifts.

Haul and construction tasks carry extra rules (wind-down at the end of work, early fetch): see `operations.md` and `construction.md`.

## Extending

- A new task type is a `TaskDef` with explicit steps; add its id and policy to `TaskDefCatalog` so saves load. A new step kind is a `TaskStep` factory, a `TasksSection.ReadStep` case and a step runner branch.

## Gotchas

- `RoutineSystem` ignores busy citizens and resumes when the task ends. An ox task is not offered to a busy (mid-tree) worker.
- Runtime step state (walk orders, ox tracks, rest targets) is not saved; only the task record and steps are (`save.md`).
- `DispatchSystem` cancels a forage task whose bush has gone or is no longer its own.

## Tests

`Sim.Tests/TaskBoardTests.cs`, `Tasks/` (DispatchAndStepRunner, Ox, CitizenTaskText, ConstructionTaskDefs), `Operations/HaulSystemTests.cs`, `Save/TasksSectionTests.cs`, `Food/FoodTripTests.cs`, `Operations/ForagingTests.cs`.
