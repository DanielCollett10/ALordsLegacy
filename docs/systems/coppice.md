# Coppice

**Purpose:** a coppice hut's family cuts hazel and willow rods from stools in its work area into the hut's own store.
Design: `design/materials.md` MAT-10, MAT-11, MAT-13..18. Copied from foraging (`foraging.md`), kept in separate files.

## Key types and files

`Assets/Sim/Coppice/` (`LordsLegacy.Sim.Coppice`), `Tasks/StepRunnerSystem.Coppice.cs`. Stools are `BushRecord`s of kind
`HazelStool` / `WillowStool` (rods = baskets). Types: `OperationTypes.Coppice`, `AreaTypes.Coppice/CoppiceWork`, `ModuleTypes.CoppiceHut`.

| Type | Role |
|---|---|
| `CoppiceSystem` | Operations phase. Per Working hut: sweep, one open `task.coppice` per idle cutter, reserve a stool. Stateless. |
| `CoppiceStores` | Hut store lookup, rods promised by unfinished tasks per kind, room per kind. |
| `CoppiceStatus` | Working, Paused, MissingHut, NoWorkers, NoStools, StorageFull. |
| `CoppiceReport` | Read model (T-566): status text, per-kind stock and cap, stools available or regrowing in the work area. |
| `CoppiceDrop` | Rods of a task that ended mid-carry go to an operation pile at the stool (cutting) or the hut (walking home). |

## Data flow

1. Hut: 6 x 4 m, 8 logs, 480 min, 300 m default circle, 2 family slots. When built, `WorkshopBuildings.OnBuilt` adds a
   `GoodsStore` of 2 x `CoppiceHutStorePerKind` with a per-item cap of 40 on each rod.
2. Per idle cutter (age 8+, light): kinds are tried shortest first (held + pending, hazel on a tie); a kind with no room or
   no free stool in the region is skipped. The nearest free stool to the hut is reserved.
3. Plan `min(stool rods, HandCarryRods 10, kind room)` rods. Steps: WalkTo stool, Work x n (`CoppiceMinutesPerRod` 6),
   WalkTo hut, `DepositForage` (shared step kind; coppice checks the per-kind cap).
4. Emptying a stool starts its regrow clock (`CoppiceRegrowDays`). `ReleaseOrphanStools` frees stools of ended tasks.

## Rules

- One kind full stops only that kind. StorageFull only when every kind that has stools in the region is full (MAT-18).
- The yard never collects from this store (ST-42b); `HoldsGoods` counts it as storage for the rod reserve.
- No new Sim state: no save bump. Hut store caps are in the Ledger section.

## Gaps

- No Unity UI, build-bar entry, art or `OperationPanelView` text for `NoStools` yet. The SimConfig asset and its EditMode
  tests list area and module types by `All`, so they need the new types.

## Tests

`Sim.Tests/Operations/CoppiceTests` (type, kind choice, store full, regrow, drops, save, full pipeline).
