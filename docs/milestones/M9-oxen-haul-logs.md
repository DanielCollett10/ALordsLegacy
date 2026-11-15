# M9: Oxen haul logs

**Shipped (player-visible):** oxen are assigned to an operation (or taken from the free pool). They are led to the logs on
the ground, loaded, walked to the log storage, unloaded, and walked back to rest. Logs visibly move with the ox. The
operation panel has an Oxen tab (assigned and free oxen, with state and Assign or Remove) and a stock line (total,
reserved and available of capacity). Status reports "Waiting for ox" while a haul waits.

**Design source:** `docs/design/woodcutting.md` (WC-21..WC-37), `docs/design/people.md` (PPL-*), `docs/design/movement.md` (MOV-16).

## Main technical changes by system

- **Ledger part 2:** `Carrier` locations (an ox, with capacity), `Stockpile` (unlimited, camp), reservations for stock and for
  capacity (`ReserveStock`, `ReserveCapacity`, `Release`, `CancelOwner`), incoming counts, `Transfer` (atomic), and
  `OperationPool` (own storages only). `Audit` also checks reservations and capacity. See `docs/systems/inventory.md`.
- **Ox pool:** `Ox.State` (Free, Reserved, Led, Resting). `OxPool.FindFor` (assigned oxen first, then nearest free),
  stand occupancy and `RestSpot`. `AssignOx` refuses an ox owned by another operation. See `docs/systems/operations.md`.
- **Ox steps and follow movement:** `MovementSystem.Attach/Detach` makes a follower trail its leader (decision 0036).
  `TaskStep` gains `FetchOx`, `LeadTo`, `LoadOx`, `UnloadOx`, `ReturnOx`. `StepRunnerSystem.Ox.cs` cancels a task whose ox is
  removed or reassigned, and drops the carried logs as an operation pile.
- **Haul tasks:** `HaulSystem` opens `task.haul_logs` for each own pile with at least `OxLoadLogs` (4) unreserved logs. It reserves
  the logs and the storage room when created. Early fetch starts a haul before the fell finishes, once a full load is near.
  At the end of work a haul still under way is wound down and its logs dropped where the ox stands. See `docs/systems/operations.md`.
- **Dispatch:** an ox task is handed out only once it has an ox, to the idle eligible worker nearest the ox. No ox task is
  handed out between the end of work and midnight. See `docs/systems/tasks.md`.
- **Unity:** `PeopleRenderer` draws oxen with their logs at the interpolated position. `LogPileRenderer` draws logs carried and
  stacked in storages and the stockpile. `OxText` shows ox state, owner and load. `CitizenPanelView` shows the ox's owner and
  cargo. `GameSceneTests.GameSceneHasM9HaulingWired` checks the wiring. No new components were added.

## Decisions made

- 0035 haul tasks reserve in the ledger.
- 0036 led oxen follow via movement attach.

## Known gaps and debt

- A haul's step numbers depend on the pile, load and tree, and they are saved with the task (`docs/systems/save.md`).
- No "previous handler" preference for hauling (WC-22, WC-24). The nearest eligible worker takes the haul. See
  `docs/design/woodcutting.md`.
- Haul behaviour is tested in Sim (`HaulSystemTests`, `HaulFetchAndSunsetTests`). The Unity side is not checked here.

## Follow-ups

- Early-fetch and wind-down rules (WC-35) are the most complex part of haul. Check them first when a haul misbehaves.
- Walk-to-site for construction builders is a gap (`docs/systems/construction.md`).
