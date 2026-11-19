# Thatch (thatcher's hut)

**Purpose:** a thatcher's family cuts reed from reed beds in its work area, ties the cut into bundles that cure in place as
a stook, then carries the dry sheaves home to the hut store. Design: `design/materials.md` MAT-30..39. Added after M19 (T-572..574).

## Key types and files

`Assets/Sim/Thatch/` (`LordsLegacy.Sim.Thatch`). Reed beds are `BushRecord`s of kind `ReedBed` (`Foraging/BushKind.cs`).
Operation `operation.thatching` (`OperationTypes.Thatching`, areas `AreaTypes.ThatchingId` / `ThatchingWorkId`), hut
`ModuleTypes.ThatcherHut`.

| Type | Role |
|---|---|
| `ThatchingSystem` | Operations work: one open `task.cut_reed` per idle cutter (heavy, age 14+), reserves a reed bed. Winter is the full rate; other seasons take longer per bundle (MAT-34). Ends as one stook. |
| `CuringRegistry`, `StookRecord` | Stooks on the ground: operation, bundles, ready tick. `Version` bumps on change. Saved by `CuringSection`. |
| `CuringSystem` | Dries stooks at their ready tick (MAT-32). |
| `SheafCollecting` | Plans `task.collect_thatch`: one dry sheaf per trip (MAT-35), from the nearest dry pile to the hut store with room. Promised sheaves are counted so none is double-booked. |
| `ThatchingStatus` | Computed status: Working, Paused, MissingHut, NoWorkers, NoBushes (no reed bed in the region), StorageFull (MAT-39e). |

## Data flow

1. A cutter reserves a reed bed and cuts bundles. The task ends with one `StookRecord` at the bed.
2. `CuringSystem` dries the stook when its ready tick passes. Dry sheaves are then collectable.
3. `SheafCollecting` opens collect trips while the hut store has room. Sheaves go into the hut store.
4. Thatch is an item (`Items.Thatch`, General). Houses and other recipes draw it from stores.

## Rules

- Green (uncured) bundles count nowhere and nothing can take them.
- Cutting and collecting stop when the hut store is full. Stooks wait.
- Tuning: `ReedSheavesPerCut`, `ReedRegrowDays`, `ReedBedCount` (saved in the Core section, T-572). Scatter is in `BushScatter`.

## Tests

`Sim.Tests/Operations/ThatchingTests.cs`, `SheafCollectingTests.cs`, `Sim.Tests/Save/CuringSectionTests.cs`. Save section id 12, version 37.
Unity: none yet. No build-bar entry, panel text or thatch test exists in `Assets/Game` or `Assets/Tests`.
