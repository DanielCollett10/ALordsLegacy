# People

**Purpose:** families, citizens and oxen, their daily routine, the working day, and debug send-to. Work is assigned by
`tasks.md`; this file covers who exists and when they are awake or on shift.

## Key types and files

`Assets/Sim/People/` (`LordsLegacy.Sim.People`). The registry is `Population` (`SimWorld.People`).

| Type | Role |
|---|---|
| `Population` | `Family`, `Citizen`, `Ox` records by explicit int id, iterated in id order. Ids never reused. `Version` bumps on add or remove. |
| `Family` | Members, home point (tent spot while Homeless, house access point once housed), `HomeHouseId`, `IsHomeless`, `WorkplaceId` (= operation id, or none). Food: `LarderLocationId`, `CarriedRations`, `IsHungry`, `TripCarrierId`, allotment reservations (`food.md`). `Passage` (None, Arriving, Leaving; `IsInPassage`, decision 0045, T-498). See `homes.md`. |
| `Citizen` | `IMovable`. `AgeYears(nowTick, SimTuning.AgeingRate)`, `Activity`, `DebugSend`, the routine and work state. |
| `Ox` | `IMovable`. `State` (`OxState`: Free, Reserved(task), Led(citizen), Resting(spot, stand id)). |
| `RoutineSystem` | Routine phase, before movement. Per-citizen state Asleep, Milling, GoingHome. Issues `MoveOrder`s only. |
| `WorkDay` | Plans shifts once per morning per citizen (see below). |
| `DebugSendState`, `DebugSendSystem` | The walk order and linger for a debug send. Movement phase, after `MovementSystem`. |
| `Activity` | Enum shown in the citizen panel (for example `MillingAbout`, `Walking`). |

## How data moves

- **Routine:** at sunrise plus a seeded 0 to `WakeStaggerMaxMinutes` delay, a sleeper mills: walks to a seeded walkable
  point within `MillRadiusM` of the camp fire (family home if no camp), stays `MillStayMinMinutes` to `MillStayMaxMinutes`,
  repeats. At sunset citizens walk home and become Sleeping on arrival. Oxen stay Waiting. Random choices use `SimRandom`.
- **Housed:** a housed family mills about its house access point (not the fire), and its members commute from there.
- **Busy skip:** a citizen with a move order set by someone else is skipped until it finishes. Later phases (work, debug
  send) can take over. Children mill around the fire too (PPL-30 to 32).
- **Working day (`WorkDay`):** adult members of families with a `WorkplaceId` get a shift. Everyone else is always on shift.
  Work ends at sunset (Sunday: minus `SundayMealMinutes`). Depart tick = max(sunrise − commute, previous work end + commute +
  `MinSleepHours`). The commute is the walk to the task's target tree (0 for target-less construction tasks). Odd-jobbers
  (working-age, no workplace) follow the same day while they hold or have open construction work (CN-25).
- **Off shift:** `RoutineSystem` walks the worker home to sleep. The step runner pauses the worker's task (progress and
  reservations kept). On shift it resumes; a task paused mid-work first walks back to its tree.
- **Passage (M16):** a family with `Passage` Arriving or Leaving is skipped by `WorkDay`, `RoutineSystem` and `DispatchSystem`, and `AssignFamily` refuses it. See `growth.md`.
- **Debug send:** `SendCitizenTo(id, point)` sets `Citizen.DebugSend`. `RoutineSystem` skips the citizen and drops its
  routine state. On arrival the linger (`DebugLingerMinutes`, 30) starts, then `DebugSend` clears and the routine starts
  afresh. A failed walk ends the send at once. Works while paused.

## Extending

- A new age-based rule uses `Citizen.AgeYears`, not a stored age, so ageing stays a function of the tick.
- New per-citizen routine state goes in the Movement save section, not in a new registry.

## Gotchas

- Heavy tasks need age 14+ (dispatcher), and the operation status "NoWorkers" also counts members aged 14+.
- The 2 starting oxen come from the start layout's `OxCount`, not from `SimTuning.StartingOxen`.
- Assigned workers with no task mill about near their family home.

## Tests

`Sim.Tests/PopulationTests.cs`, `Sim.Tests/People/` (`RoutineSystemTests`, `WorkDayTests`, `OddJobberWorkDayTests`,
`DebugSendSystemTests`, `FamilyPassageTests`, `PassageWorkReadersTests`), `Sim.Tests/Commands/SendCitizenToTests.cs`, `Sim.Tests/Save/PopulationSectionTests.cs`.
Unity: `Assets/Tests/PlayMode/DebugSendTests.cs`, `PeopleRendererTests.cs`.
