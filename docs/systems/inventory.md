# Inventory (ledger)

**Purpose:** every physical item (logs and food) has a location and a count. Items enter only by production and leave only by
transfer or consumption, so nothing appears from nowhere (decision 0032, decision 0035).

## Key types and files

`Assets/Sim/Inventory/` (`LordsLegacy.Sim.Inventory`), registry `Ledger` (`SimWorld.Inventory`, partial `SimWorld.Inventory.cs`).

| Type | Role |
|---|---|
| `Ledger` | Locations, counts, reservations, journals (produced, consumed), `Version`, `Audit()`. `AddCarrier`, `AddFoodStore`, `FindFoodStore`. |
| `Ledger.Reservations.cs`, `Ledger.Restore.cs` | Reservation methods and internal restore methods for the save reader. |
| `LedgerLocation`, `LocationKind` | `GroundPile` (point, optional owner operation id), `Storage` (module id, capacity from the type), `Carrier` (capacity; an ox, or a person's trip or forage carrier), `Stockpile` (unlimited, camp logs), `Site` (construction site, footprint id), `FoodStore` (food only; refuses non-food; larders, the camp food store, hut and granary stores), `GoodsStore` (General items only, `AddGoodsStore`; refuses logs and food; decision 0046), `WorkshopStock` (`AddWorkshopStock(module, item, cap)`; holds only `InputItemId`; not a construction source, left out of `TotalOf`; `WorkshopDef` in `Operations/Workshops`, T-520, save v28). |
| `Reservation` | Id, location, item, count, owner, kind (stock or capacity). |
| `ItemDef`, `ItemCategory`, `Items` | `Items.Log` (`item.log`, Log). `Plank`, `Beam` (category General). `Meat` (3 rations), `Berries` and `Mushrooms` (1 ration each), all category Food. `Items.All` lists them. |
| `HandLoads.For` | Hand load per trip (ST-43, PL-16): planks use `HandCarryPlanks`, other General items `HandCarryItems`, beams and non-General goods return null (beams go by ox). Add a row for an item with its own load. |
| `OperationPool` | `SimWorld.OperationPool(opId, item)` sums the operation's own storage modules only. |

## Operations

- `Produce(location, item, n)` / `ProduceOnGround` merge into the nearest same-owner pile within 1 m. `Produce` throws past capacity.
- `Transfer(from, to, item, n, owner?)` is atomic: false and no change on failure. It consumes the matching reservations.
- `Consume` removes items for good (construction and eating use it), and is journalled.
- `ReserveStock` / `ReserveCapacity(loc, item, n, owner)` return a reservation id (0 = refused). `Release(id)`, `CancelOwner(owner)`.
- Per location and item: `TotalOf`, `ReservedOf`, `IncomingOf`, `AvailableOf` (total − reserved), `FreeSpace` (capacity − total − incoming).
- `Audit()` returns null when consistent. It checks location sums against the produced and consumed journals, and checks
  reservations and capacity.

## Extending

A new item is an `ItemDef` in `Items`. A new location kind needs its own save and `Audit` rules.

## Gotchas

- Unowned ground piles are construction sources. Site-owned piles are not (`construction.md`).
- A `GoodsStore` takes General items only. For sites it is the only source of General items (planks, beams); logs never come from it. A `WorkshopStock` is never a source.
- `Storage` capacity comes from the module type. A removed storage's logs stay in the ledger; `RemoveModule` refuses a storage
  with logs reserved for a site.
- A food store's capacity comes from its type, or from `ForagerHutCapacity` for the forager hut. `FoodStats` and the granary panel read counts through the ledger.
- The save stores both counters and journals; `Audit` runs on load and throws `SaveFormatException` if they disagree.

## Tests

`Sim.Tests/LedgerTests.cs`, `LedgerReservationTests.cs`, `Sim.Tests/Operations/LogLoadTests.cs`, `Sim.Tests/Save/LedgerSectionTests.cs`,
`Sim.Tests/Save/LedgerFoodTests.cs`, `Sim.Tests/Inventory/GoodsStoreTests.cs` (General goods stores). Food stores: `Sim.Tests/Food/FoodStoreTests.cs`. Workshop stock: `Sim.Tests/Workshops/InputSourcesTests.cs`.
Unity: `Assets/Tests/PlayMode/LogStacksTests.cs`.
