# Storage yard

**Purpose:** the General goods store: a built, staffed yard that holds planks and beams, how goods get in and out, and the workshop input reserve. Design: `design/storage.md` (ST-*), `design/planks.md` (PL-30..34). Details live in operations.md, workshops.md and tasks.md; this page is the map and the end-to-end proof.

## The loop

1. `PlaceStructure(module.storage_yard)` makes the 8 x 6 m footprint and site (8 logs); workers build it. `StorageYards.OnBuilt` makes its General `GoodsStore` and `operation.storage_yard`.
2. `AssignFamily` staffs it. `YardFamilySystem` plans `task.haul_by_hand` trips from `YardJobs.Standard()`, in order:
   camp General store (ST-40), unowned General piles (ST-41), workshop output stores with any free item (ST-42, `YardCollectThreshold` 0), beams by pool ox from a hewing store with a free beam (ST-43).
3. A hand trip carries the item's hand load (planks 2). Room and stock are reserved when planned, so trips never double-book (ST-44).
4. Others draw from it: construction plank sources, sawpit beam fetches, the beam reserve and `GoodsStats` all count yard stock by distance (ST-06, ST-07).
5. Camp removal (`CampRemovalSystem`): with a yard, an empty camp General store closes (ST-04); when all families are housed and food is handed over, leftovers drop as one unowned pile and the yard collects it (ST-31).
6. `StorageYardReport` is the read-only panel model.

## Workshop reserve (PL-30..34)

A fetch opens only if storage total minus stock reserved by open fetches minus the load is at least the reserve. Default `WorkshopDefaultReserve` = 5. `SetWorkshopReserve(op, item, n)` sets it per workshop (0..9999, own input only). The status reads "Waiting: 60 / 60 logs in storage" when the reserve is the only block. Saved in CoreSection and the operations section.

## End-to-end proof

`Sim.Tests/Operations/StorageYardEndToEndTests.cs`, from `StartLayoutApplier`, real commands and ticks, ledger Audit on every tick:

- Camp planks reach the yard (10); a sawpit holding 1 plank has it collected; a stall is built and its 2 planks come out of the yard.
- Default reserve: logs become beams become planks that reach the yard; reserve logs stay in storage.
- `SetWorkshopReserve(hewing, log, 60)` with 66 logs: exactly 6 are fetched and hewn, then it waits with the text above.
- Camp goes once housed and food handed over; its planks are collected.
- Save mid-run, load, run two more days: byte-identical (`SaveDiff`).

Shortcuts: housing (`HomeHouseId`, `FoodHandedOver` set directly) in the camp-removal test only.

## Gaps and follow-ups

- No Unity yard panel or build button yet (the bar shows Storage yard greyed out); no UI for the reserve.
- Two logs of a fetched batch were seen on ground piles after a hewing run (a site/ox sunset drop, see workshops.md); not fixed here.
- Houses are not built in the end-to-end test; housing is a shortcut.

## Kit fetch and drop (T-564, ST-21..24, ST-23)

- `Kit/KitFetchSystem` (after `YardFamilySystem`): a kit store with free slots holds a claim, a full one closes it. Oldest claim first, the building's own family gets `task.haul_by_hand` from the nearest camp General store, working yard or weaver store.
- `KitStores.DropOnRemoval` runs on deconstruct, `RemoveModule` and `DeleteArea`: the claim closes and the kit becomes an unowned ground pile at the access point for yard families.
