# Operations

**Purpose:** an operation is a workplace: a building area with a hut, families assigned to it, and a work area. Woodcutting
fells trees and keeps log storage; foraging picks bushes (`foraging.md`); coppice cuts rods (`coppice.md`); the granary is an
area-less operation (decision 0041). It creates the tasks its families do and tracks the logs it makes (WC-*, decision 0031).

## Key types and files

`Assets/Sim/Operations/` (`LordsLegacy.Sim.Operations`), plus `Processes/` and the operation commands in `Commands/`.

| Type | Role |
|---|---|
| `OperationTypeDef`, `OperationTypes` | Static types: `Woodcutting`, `Foraging`, `Coppice`, `Thatching`, `Granary` (area-less, 2 slots, `FindByModule`), `StorageYard` (area-less, 2 slots), and the workshops via `WorkshopOf` (area-less, 1 slot). Not on a catalog (decision 0019). |
| `Operation`, `OperationRegistry` | Id, type, area, module footprints, families, oxen, work area, paused flag. Every change bumps `Version`. |
| `OperationStatus` | `Of(world, op)`, computed never stored: Working / Paused, MissingHut, MissingStorage, NoWorkers, NoTrees, StorageFull, NoRoomAtMarket, WaitingForPlanks. `Reported` adds WaitingForOx (display only). |
| `WoodcuttingSystem` | Operations phase. One Open `task.fell_tree` per idle adult worker, aimed at the nearest free standing tree in the work region. |
| `FellingOutcome` | After fell: tree Felled. On the last step: Stump plus logs (`floor(trunk ÷ StandardLogLengthM)`) in an operation pile. |
| `HaulSystem` (+ `.Early.cs`) | Operations phase, last. Opens `task.haul_logs` per own pile: 1 to `OxLoadLogs` (1) per trip. Sunset wind-down leaves an operation pile. |
| `Workshops/WorkshopSystem` | Operations phase, after `ConstructionSystem`. See `workshops.md`. |
| `ModulePlacement`, `DraftModulePlacement` | Pure previews. The draft variant checks polygon and ghosts not yet in the world (decision 0037). |
| `DesignateOperation`, `PlaceModules` | Commands: area + ordered modules (new operation), or modules for Expand. All or nothing. |
| `DeconstructOperation` | Marks every module; the area goes when the last one is gone. |
| `OxPool` | `FindFor(opId, from)`: assigned oxen first, then the nearest free pool ox. Stand occupancy, rest spots (WC-31/32). |
| `Processes/` | `ProcessDef` (ordered `StageDef`s), `ProcessDefs.Timber`. `EffectiveStages` moves a disabled stage's time to the next. |

## Area-less and special operations

- **Foraging camp** (`area.foraging`): a forager hut, no log storage, 300 m work circle (`foraging.md`).
- **Coppice hut** (`area.coppice` plus `area.work_coppice`): `CoppiceSystem` opens `task.coppice` (`coppice.md`).
- **Thatcher's hut** (`OperationTypes.Thatching`): a hut, a work area, and a sheaf store. `ThatchingSystem` and `SheafCollecting` (`thatch.md`).
- **Storage yard**: no area, one hut, 2 family slots. `YardFamilySystem` plans the trips (`storage.md`).
- **Granary**: no area (`AreaId` 0), the granary footprint as its hut. `SetGranarySupplies` sets `SuppliesMarket` (`markets.md`).

## Lifecycle

1. **Designate:** `OperationDraftTool` (area, then module ghosts in `OperationDraft`) → one `DesignateOperation` on Confirm.
2. **Modules:** Expand (`PlaceModules`) adds modules to an existing operation. An empty recipe is built at once; else it is a
   construction site (`construction.md`). `RemoveModule` drops an unbuilt one.
3. **Assign:** `AssignFamily` sets the family's `WorkplaceId`. Nothing is automatic. Refused for an unbuilt hut (CN-04b) and for a family in passage (M16).
   `UnassignFamily` cancels the family's unfinished tasks.
4. **Work and haul:** felling tasks open per idle worker, then are dispatched and stepped (`tasks.md`). Ground logs go to storage by ox.
   At sunset a haul still on its way is wound down as an operation pile.
5. **Pause, work area, deconstruct:** `PauseOperation` / `ResumeOperation`, `SetWorkArea`, `UseDefaultWorkArea`.
   `DeconstructOperation` marks every module. The area and operation go with the last one.

## Rules

- Felling stops when `LogLoad` (piles, storage, carried logs, reserved room, no double counting) reaches storage capacity.
  It restarts when logs leave storage or a second storage adds capacity (to 40).
- `DeleteArea` refuses an operation area that still has modules ("Deconstruct its buildings first", CN-58), and a plot that still has a house ("Deconstruct the house first", HM-14b).
- Status is computed, never stored (`OperationStatus.Of`). Footprint and operation ids share the ledger owner field, so `RemoveModule`
  guards against a site's pile looking like an operation's. Oxen stands hold `OxCapacity` 1 (camp stand unlimited).

## Tests

`Sim.Tests/Operations/` (`Woodcutting*`, `Foraging`, `CoppiceTests`, `OperationStatus`, `OperationCommand`, `Haul*`, `Ox*`, `LogLoad`,
`DeconstructOperation`, `Thatching*`), `Scenarios/BasketChainScenarioTests`, `Sim.Tests/Commands/`, `Sim.Tests/Processes/`.
Unity (EditMode): `OperationDraft*Tests`. PlayMode: `OperationPanelTests`, `ThatcherUiTests`.
