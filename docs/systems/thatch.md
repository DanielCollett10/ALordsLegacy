# Thatch (thatcher's hut)

**Purpose:** a thatcher's family cuts reed from reed beds in its work area, binds the cut into bundles that cure where they
stand as a stook, then carries the dry sheaves home to the hut store. Design: `design/materials.md` (MAT-30..39e, MAT-43).
Shipped in M20 (`milestones/M20.md`). Decision 0056 (green bundles stay outside the ledger).

## Key types and files

`Assets/Sim/Thatch/` (`LordsLegacy.Sim.Thatch`). Reed beds are `BushRecord`s of kind `ReedBed` (`Foraging/BushKind.cs`).
Operation `OperationTypes.Thatching`, areas `AreaTypes.Thatching` / `ThatchingWork`, hut `ModuleTypes.ThatcherHut`.

| Type | Role |
|---|---|
| `ThatchingSystem` | Operations phase. Per Working hut: collect trips first (one per idle cutter), then one open `task.cut_reed` per idle cutter (age 14+, heavy), each reserving the nearest free bed. Frees beds of ended tasks. |
| `StepRunnerSystem.Thatch.cs` | Cut steps: walk to the bed, then one heavy `Work` step per bundle. At the end the bundles become one stook. |
| `CuringRegistry`, `StookRecord` | Green stooks, outside the ledger: position, operation, count, ready tick. `Step` dries them into a thatch pile. `Version`, `Audit`. `CuringSystem` calls `Step` each tick. |
| `SheafCollecting` | Pure queries and task builder for `task.collect_thatch`: one dry sheaf per trip, nearest dry pile to the hut store. Promised sheaves are counted, so none is double-booked. |
| `ThatchingStatus`, `ThatchingReport` | Computed status (Paused, MissingHut, NoWorkers, NoBushes, StorageFull, else Working) with panel text, and the panel read model. |
| `GrownGrainSource` | Always unavailable ("Needs farming", MAT-31). No fields exist in the Sim yet. |
| `Items.Thatch` | General item. Hand load `HandCarryThatch` (1 a trip). |

## Data flow

1. A cutter reserves a bed and cuts. The task ends with one `StookRecord` at the bed (`StepCutReed`).
2. `CuringSystem` dries it at its ready tick (`ThatchCureDays` days). The sheaves become a journalled pile owned by the operation.
3. `ThatchingSystem` plans collect trips while the hut store has room. Sheaves go into the hut's `GoodsStore`.

## Rules

- Green bundles count nowhere, and nothing can take them. Curing and collecting run in any season. Collecting comes before cutting.
- Winter (Dec–Feb) cuts at `ThatchCutMinutesPerBundle` (30) a bundle. Other months multiply that by `ThatchOffSeasonFactor` (4).
- A cut takes up to `ReedSheavesPerCut` (8) bundles, limited by the bed. A bed regrows after `ReedRegrowDays` (90).
- Cutting and collecting stop when the hut store is full (`ThatcherHutStore`, 40): status "Store full", stooks wait.
- `BushScatter` puts `ReedBedCount` (20) beds on `LowGround` (below the 25th height percentile, slope ≤ 3°), on their own random stream.

## Unity

- Build bar `thatcher` (`ToolEntry`) runs `OperationDraftTool` (`ThatcherToolId`), which offers only the hut. Gate: `BuildCost.MinimumFor`.
- `OperationPanelView` thatch rows: status, store, stooks curing, dry sheaves, greyed grain toggle. `BushRenderer` draws beds
  (green when full, stubble while regrowing). `StookRenderer` draws stooks (green curing, pale dry in piles). `ModuleRenderer` draws the hut as the woodcutting box (`design/placeholder-art.md`).

## How to extend

- A new thatch source (grown grain): give it an `Available` check like `GrownGrainSource`. The panel toggle is already wired.
- A new drying process: a registry with `Step(tick, ledger)` that produces into the ledger when done (decision 0056).
- New tuning: `SimTuning`, `CoreSection` (write and read), the `SimConfig` asset, and tests (the mirror rule in `CLAUDE.md`).

## Gotchas

- `ModuleTypes` is static and cannot read tuning. The hut's logs, minutes and footprint (`ThatcherHutLogs`, `ThatcherHutMinutes`,
  `ThatcherHutWidthM`, `ThatcherHutDepthM`) are saved and shown in SimConfig, but nothing reads them. The hut is hard-coded
  (8 logs, 480 min, 6 × 4 m). See `design/materials.md` (MAT-39b note) and `milestones/M20.md`.

## Tests

- Sim: `Operations/ThatchingTests`, `SheafCollectingTests`, `ThatchingReportTests`, `Save/CuringSectionTests`, `Homes/HouseMaterialsTests`,
  `Start/CampGeneralStoreTests` (90 thatch, 90 panels).
- End to end: `Scenarios/ThatchChainScenarioTests`. A December hut cuts, cures 7 days and fills its store. A house is built from
  that thatch and yard panels. Summer is 4× slower. Ledger and curing are audited each tick, and a mid-cure save is byte-identical.
- Unity PlayMode: `ThatcherUiTests` (build-bar gate, reed-bed and stook drawing, panel rows). EditMode: `SimConfigTests`.
