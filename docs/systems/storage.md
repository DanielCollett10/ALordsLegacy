# Storage yard

**Purpose:** a built, staffed yard that holds the village's General goods (planks, beams), how families move goods in and out, and the workshop input reserve. Design: `design/storage.md` (ST-*), `design/planks.md` (PL-30..34). Decisions 0054, 0055.

## Key types and files

Sim: `Assets/Sim/Operations/` (yard planner and sources), `Operations/Workshops/InputReserve.cs`, `Commands/SetWorkshopReserve.cs`. Unity: `Assets/Game/UI/StorageYardPanel/`, `AgentPicker` (the workshop slot picks yards), `StructureTool` id `storage-yard`.

| Type | Role |
|---|---|
| `ModuleTypes.StorageYard`, `OperationTypes.StorageYard` | 8 x 6 m building, 8 logs, 480 min; `operation.storage_yard`, area-less, 2 family slots. |
| `YardFamilySystem` | Operations phase. Plans `task.haul_by_hand` for idle members of assigned families. |
| `IYardJobSource`, `YardJobs.Standard()` | Ordered job rules: camp store (ST-40), piles (ST-41), workshop output (ST-42), beams by ox (ST-43). |
| `YardRoute` | Nearest yard with room (ST-09), `TripFrom`, `FirstHandCarriable` (beams skipped). |
| `StorageYardReport` | Read model: stock, total and capacity, families, ST-45 status, village totals. |
| `InputReserve`, `SetWorkshopReserve` | Reserve rule (PL-31) and its command (0..9999). |
| `CampRemovalSystem` | Camp General store rules (ST-04, ST-30..32). |

## How data moves

1. **Build:** `PlaceStructure` makes footprint, site and operation in one Apply; `WorkshopBuildings.OnBuilt` adds the General `GoodsStore` (capacity 300).
2. **Plan:** each tick, `YardFamilySystem` counts idle members (age 8+, not in passage) and open trips per working yard, then `PlanOne` takes the first usable trip, cuts it to the hand load, free stock and room, and reserves both ends (ST-44).
3. **Carry:** `task.haul_by_hand` uses the store families' collect and deposit steps. Beams are `task.fetch_input` with an ox, opened by `YardBeamSource`, which returns no hand trip.
4. **End:** a sweep frees the reservations of finished trips and drops goods still carried as an unowned pile.
5. **Draw:** construction takes planks from GoodsStores (`ConstructionSources`); sawpits take beams from hewing stores and yards (`InputSources.BeamFinder`); `GoodsStats` counts yard stock.

## Rules

- A GoodsStore takes General items only. Its capacity is shared across items (ST-08); kit and coppice stores also cap each item (T-555).
- Any free workshop output is collected, fullest store first (`YardCollectThreshold` 0). Never coppice huts, the daub pit, input stock or reserved stock.
- Beams go by ox only. With no free pool ox the panel says "Waiting for ox".
- Reserve: a fetch opens only if storage total − fetches on the way − load ≥ reserve. Default 5, 0 = none, per operation in `Operation.Reserves`.
- Camp: with a yard, an empty camp General store closes (ST-04), and at removal its goods drop as an unowned pile (ST-31). With no yard the camp stays and says so once (ST-30, ST-32).

## Extending

- New job: implement `IYardJobSource` and add it to `YardJobs.Standard()` in priority order. An ox job makes its own task and returns null, as `YardBeamSource` does.
- New General item: add it to `Items`; give `HandLoads.For` a row if it has its own load, else `HandCarryItems` applies.
- New status: add it to `StorageYardStatus` and `TextOf`, and keep the order in `StatusOf`.

## Gotchas

- Storage counts logs in Storage and Stockpile, beams in workshop and yard stores, and rods in coppice and yard stores. Piles, input stock, sites and carried goods do not count. `WorkshopStock` is never a source. "Collecting" in ST-45 includes beam trips.
- Demolishing a yard drops its stock at the access point as an unowned pile.
- The Supplies market is greyed out ("No demand yet", ST-10). Yard status is computed on each read and never saved.

## Gaps

- ST-45 has seven statuses (the design lists five). No yard case in `SimSaveRoundTripTests`; `StorageYardEndToEndTests` covers a mid-run save and reload.

## Kit fetch and drop (T-564, ST-21..23)

- `Kit/KitFetchSystem` (after `YardFamilySystem`): a kit store with free slots holds a claim, a full one closes it. Oldest claim first, the building's own family gets `task.haul_by_hand` from the nearest camp General store, working yard or weaver store.
- `KitStores.DropOnRemoval` runs on deconstruct, `RemoveModule` and `DeleteArea`: the claim closes and the kit becomes an unowned ground pile at the access point for yard families.

## Tests

Sim: `Operations/StorageYard*`, `Yard*` (`FamilySystem`, `Sources`, `GoodsSources`, `WorkshopSource`, `BeamSource`), `Inventory/HandLoadsTests`, `Start/CampYardRemovalTests`, `Workshops/InputReserveTests`, `WorkshopReserveSettingTests`, `TimberChainEndToEndTests`.
Unity PlayMode: `StorageYardPanelTests`, `WorkshopPanelTests`, `StructureToolTests`, `GameSceneTests`.
