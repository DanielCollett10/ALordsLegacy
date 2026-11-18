# Foraging

**Purpose:** foragers aged 8+ pick berries and mushrooms from bushes in a work area and carry them to their own forager
hut store, if it has room. Design: `design/food.md` (FD-20..24). Decisions 0043, 0044 (hut store only).

## Key types and files

`Assets/Sim/Foraging/` (`LordsLegacy.Sim.Foraging`), `Core/SimWorld.Foraging.cs`, `Tasks/StepRunnerSystem.Forage.cs`,
`Save/Sections/BushesSection.cs`. Operation and area types: `Operations/OperationTypes.cs`, `Areas/AreaTypes.cs`.

| Type | Role |
|---|---|
| `BushRecord`, `BushKind` | Position, kind (Berries or Mushrooms), baskets, emptied tick, `ReservedBy` task id. |
| `BushRegistry` | All bushes (`SimWorld.Bushes`). Reserve, take a basket, nearest available, regrow. `Version` ignores reservations. |
| `BushScatter`, `BushRegrowSystem` | New-game scatter with the seeded random. Operations phase: regrow after 14 days. |
| `ForagingSystem` | Operations phase. Per Working camp: sweep, one open `task.forage` per idle forager, reserve a bush, plan baskets. Stateless. |
| `ForagingStores` | The own hut store only (0044; never a granary or the camp) and room after pending deposits. |
| `ForagingStatus` | Computed status: Working, Paused, MissingHut, NoWorkers, NoBushes, StorageFull. |
| `StepRunnerSystem.Forage.cs` | Steps: `WalkTo` bush, `Work` per basket (`PickBasket` makes one item on the carrier), `WalkTo` store, `DepositForage`. |

## Data flow

1. The camp (`operation.foraging`) needs a built forager hut (6 × 4 m, 8 logs, 480 min, food store 30), a work region
   (300 m circle or drawn), and a family member aged 8+.
2. `ForagingSystem` opens `task.forage` per idle forager (owner = operation, `AssignedFamilyOnly`, light). It reserves the
   nearest bush and plans `min(bush baskets, 10 hand-carry, hut store room, 20)` baskets.
3. `DispatchSystem` offers it only to a forager aged `ForagerMinAge` (8) or more. Travel is measured to the bush.
4. Steps: walk to the bush; each basket is 30 minutes of work, then one item on a ledger carrier; walk to the store;
   `DepositForage` moves everything carried, if the store still works and has room.
5. A task that ends mid-carry drops its food at the bush or hut. Bushes of finished tasks are released. The last basket
   starts the regrow clock.

## Rules

- Only the camp's own hut store (0044). Never a granary, never the camp food store (FD-12). Granary store families collect from the hut store (`markets.md`, `StoreFamilySystem`).
- Room = free space minus items promised to unfinished forage tasks. Hut full means Storage full and no new tasks (FD-23b).
- A granary operation has no work region, so foraging skips it by type.

## How to extend

- **New bush kind:** add it to `BushKind`, map it in `PickBasket`, and update the kind check in `BushesSection`.
- **Store families** collect from the hut store into granaries (`markets.md`). Foragers stay hut-only.
- **Balancing:** `SimTuning` `BushBaskets`, `BushRegrowDays`, `ForagerMinAge`, `ForagerHutCapacity`,
  `GatherMinutesPerBasket`, `HandCarryItems`. All are placeholders until playtested.

## Gotchas

- The hut store is the camp's own food store. `PlaceModule` sizes it from `ForagerHutCapacity`.
- Bush reservations do not move `Bushes.Version`, so `BushRenderer` does not redraw on a reservation.
- Bushes are not in the baked map (FD-20). They come from the new-game scatter, then from the save.

## Basket loan (T-565, MAT-50..54)

- `Kit/KitLoans` reserves one kit basket at plan time (owner `-3_000_000 - taskId`): load is 2 x `HandCarryItems`, else normal. `ReleaseFinished` frees it when the task ends. Tests: `Kit/KitLoanTests`.

## Tests

- Sim: `Operations/ForagingTests` (full pipeline), `Operations/OperationStatusTests`, `Save/BushesSectionTests`,
  `Tasks/DispatchAndStepRunnerTests`.
- Unity PlayMode: `ForagerUiTests`. Gap: no `BushRenderer` test.
