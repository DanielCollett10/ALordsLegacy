# Operations (woodcutting)

**Purpose:** an operation is a workplace: a building area with a hut, log storage and optional oxen stand, families assigned to
it, and a work area in which it fells trees. It creates the tasks its families do and tracks the logs it makes (WC-*, decision 0031).

## Key types and files

`Assets/Sim/Operations/` (`LordsLegacy.Sim.Operations`), plus `Processes/` and the operation commands in `Commands/`.

| Type | Role |
|---|---|
| `OperationTypeDef`, `OperationTypes` | Static types: `Woodcutting`, `Foraging`, `Granary` (area-less, `AreaId` 0, 2 slots, decision 0041; `FindByModule`). Not on a catalog (decision 0019). |
| `Operation`, `OperationRegistry` | Id, type, area, module footprints, families, oxen, work area, paused flag. Every change bumps `Version`. |
| `OperationStatus` | `Of(world, op)`, computed never stored: Working / Paused, MissingHut, MissingStorage, NoWorkers, NoTrees, StorageFull. `Reported` adds WaitingForOx (display only). |
| `WoodcuttingSystem` | Operations phase. Keeps one Open `task.fell_tree` per idle adult worker, aimed at the nearest free standing tree in the work region. |
| `FellingOutcome` | After fell: tree Felled. On the last step: Stump plus logs (`floor(trunk ÷ StandardLogLengthM)`) in an operation pile. |
| `HaulSystem` (+ `.Early.cs`) | Operations phase, last. Opens `task.haul_logs` for each own pile holding ≥ `OxLoadLogs`. Early fetch and wind-down. |
| `ModulePlacement`, `DraftModulePlacement` | Pure previews. The draft variant checks polygon and ghosts not yet in the world (decision 0037). |
| `DesignateOperation`, `PlaceModules` | Commands: area + ordered modules (new operation), or modules for Expand. All or nothing. |
| `DeconstructOperation` | Marks every module; the area goes when the last one is gone. |
| `OxPool` | `FindFor(opId, from)`: assigned oxen first, then the nearest free pool ox. Stand occupancy, rest spots (WC-31/32). |
| `Processes/` | `ProcessDef` (ordered `StageDef`s), `ProcessDefs.Timber`. `EffectiveStages` moves a disabled stage's time to the next. |

## Foraging camp (T-423, FD-22..24, FD-23b)

`OperationTypes.Foraging` (`area.foraging`): mandatory `module.forager_hut` (8 logs/480 min; also a food-only store of 30, made by
`PlaceModule`, used once built), no log storage, 300 m circle. `ForagingSystem` opens one `task.forage` per idle forager aged 8+: reserve
the nearest bush, pick up to `HandCarryItems` baskets (1 item each), deliver to the nearest store with room (`ForagingStores`: own hut or
built granary, never the camp). `ForagingStatus` adds `NoBushes`; every store full = StorageFull.

## Lifecycle

1. **Designate:** `OperationDraftTool` (area, then module ghosts in `OperationDraft`) → one `DesignateOperation` on Confirm.
2. **Modules:** Expand (`PlaceModules`) adds modules to an existing operation. Footprint added, trees under it cleared. An
   empty recipe is built at once; else it is a construction site (`construction.md`). `RemoveModule` drops an unbuilt one.
3. **Assign:** `AssignFamily` sets the family's `WorkplaceId`. Nothing is automatic. Refused for an unbuilt hut (CN-04b).
   `UnassignFamily` cancels the family's unfinished tasks.
4. **Work:** felling tasks open per idle worker, then are dispatched and stepped (`tasks.md`). Timber stages come from
   the effective `ProcessDefs.Timber` stages.
5. **Haul:** ground logs go to storage by ox. Early fetch can start once a full load is near. At sunset a haul still on
   its way is wound down as an operation pile where the ox stands.
6. **Pause, work area:** `PauseOperation` / `ResumeOperation`, `SetWorkArea`, `UseDefaultWorkArea`.
7. **Deconstruct:** `DeconstructOperation` marks every module; the area and operation go with the last one.

## Rules

- Felling stops when `LogLoad` (piles, storage, carried logs, reserved room, no double counting) reaches storage capacity.
  It restarts when logs leave storage or a second storage adds capacity (to 40).
- `DeleteArea` refuses an operation area that still has modules ("Deconstruct its buildings first", CN-58), and a plot that still has a house ("Deconstruct the house first", HM-14b).

## Extending
- A new operation type is an `OperationTypeDef` plus area types in `AreaTypes`. A new status reason goes in `OperationStatus.Of`. Compute status, never store it.

## Gotchas
- `OperationStatus.Of` gates felling. Footprint and operation ids share the ledger owner field, so `RemoveModule` guards
  against an operation seeing a site's pile as its own. Oxen stands hold `OxCapacity` = 1 (camp stand unlimited).

## Tests
`Sim.Tests/Operations/` (`Woodcutting*`, `Foraging` incl. the full pipeline, `OperationStatus`, `OperationCommand`, `Haul*`, `Ox*`, `LogLoad`,
`DeconstructOperation`), `Sim.Tests/Commands/`, `Sim.Tests/Processes/`. Unity (EditMode): `OperationPanelTests`, `OperationDraft*Tests`.
