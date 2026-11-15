# Operations (woodcutting)

**Purpose:** an operation is a workplace: a building area with a hut, log storage and optional oxen stand, families assigned to
it, and a work area in which it fells trees. It creates the tasks its families do and tracks the logs it makes (WC-*, decision 0031).

## Key types and files

`Assets/Sim/Operations/` (`LordsLegacy.Sim.Operations`), plus `Processes/` and the operation commands in `Commands/`.

| Type | Role |
|---|---|
| `OperationTypeDef`, `OperationTypes` | Static types. `Woodcutting`: hut, log storage, optional oxen stand, family slots, default work radius. `Find`, `FindByBuildingArea`. Static, not on a catalog (decision 0019 says they should be; open). |
| `Operation` | Id, type, area id, module footprint ids, family ids, ox ids, optional work area id, paused flag. |
| `OperationRegistry` (`SimWorld.Operations`) | Holds operations; every change bumps `Version`. `AssignOx` refuses an ox owned by another operation (`FindOxOwner`). |
| `OperationStatus` | `Of(world, op)` is computed, never stored: Working / Paused, MissingHut, MissingStorage, NoWorkers (no member 14+), NoTrees, StorageFull. `Reported` adds WaitingForOx. `LogLoad` is the one place the storage-full sum lives. |
| `WoodcuttingSystem` | Operations phase. For each Working operation keeps one Open `task.fell_tree` per idle adult worker, aimed at the nearest standing unreserved tree to the hut access point within the work region. |
| `FellingOutcome` | Called by the step runner after each step. After fell: tree Felled. On the last step: Stump plus `floor(trunk ÷ StandardLogLengthM)` logs in an operation-owned pile at the tree. |
| `HaulSystem` (+ `.Early.cs`) | Operations phase, last. Opens `task.haul_logs` for each own pile holding ≥ `OxLoadLogs`. Early fetch and wind-down. |
| `ModulePlacement` | Pure `Preview(world, opId, moduleTypeId, centre, rotationDeg)` → `ModulePlacementResult` (`Ok`, `Refusal`). |
| `OxPool` | `FindFor(opId, from)`: assigned oxen first, then the nearest free pool ox. Stand `Occupancy`/`HasRoom`, `RestSpot`/`Rest` (WC-31/32). |
| `Processes/` | `ProcessDef` (ordered `StageDef`s), `ProcessDefs.Timber` (fell 90, delimb 30 off, buck 15 per log). `EffectiveStages(logCount)` moves a disabled stage's time into the next enabled one. |

## Lifecycle

1. **Designate:** `DesignateArea` of a building type with an operation type creates an operation.
2. **Modules:** `OperationDraftTool` (ghosts, `DraftModulePlacement`) → `DesignateOperation` or, when expanding, `PlaceModules`. The footprint is added and trees under it are
   cleared. A module with an empty recipe is built at once, else it is a construction site (`construction.md`).
   `RemoveModule` removes it.
3. **Assign:** `AssignFamily` sets the family's `WorkplaceId` and the operation's family list. Nothing is automatic. Assign
   is refused for a hut that is not built (CN-04b). `UnassignFamily` cancels the family's unfinished tasks.
4. **Work:** felling tasks are opened per idle worker. They are dispatched and stepped (`tasks.md`). Each step's walk and
   timber stages come from the effective `ProcessDefs.Timber` stages.
5. **Haul:** logs on the ground go to storage by ox. Early fetch can start before the fell task finishes, once a full load is
   near. At sunset a haul still on its way is wound down: logs dropped where the ox stands, as an operation pile.
6. **Pause and work area:** `PauseOperation` / `ResumeOperation`, `SetWorkArea` (must be the type's work area), and
   `UseDefaultWorkArea`. `DeleteArea` resets a linked work area to the default.

## Rules

- Felling stops when `LogLoad` (piles, storage, carried logs, reserved room, all without double counting) reaches total
  storage capacity. It restarts when logs leave storage or a second storage adds capacity (to 40).
- A removed operation area also removes its module footprints. Ledger locations are not cleaned up.

## Extending

- A new operation type is an `OperationTypeDef` (and its area types in `AreaTypes`). Its build recipe goes on the footprint type.
- A new status reason goes in `OperationStatus.Of`. Use `Reported` for display-only text.
- Do not store status. Compute it.

## Gotchas

- `OperationStatus.Of` gates felling. `Reported` is display only.
- Footprint and operation ids share the owner field in the ledger. An operation with the same id as a site sees that site's
  pile as its own. `RemoveModule` guards this, and the wind-down relies on it.
- Oxen stands: a stand holds `OxCapacity` = 1; the camp stand is unlimited.

## Tests

`Sim.Tests/Operations/` (`WoodcuttingSystemTests`, `WoodcuttingTypeTests`, `OperationStatusTests`, `OperationCommandTests`,
`ModulePlacementTests`, `OxCommandTests`, `OxPoolTests`, `LogLoadTests`, `StorageFullFellingTests`, `HaulSystemTests`,
`HaulFetchAndSunsetTests`, `BuiltModuleGateTests`), `Sim.Tests/Processes/ProcessDefTests.cs`, `Sim.Tests/Save/StructuresSectionTests.cs`.
Unity: `Assets/Tests/PlayMode/OperationPanelTests.cs`, `WoodcuttingVisualsTests.cs`.
