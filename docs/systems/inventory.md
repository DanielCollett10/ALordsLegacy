# Inventory (ledger)

**Purpose:** every physical item (logs for now) has a location and a count. Items enter only by production and leave only by
transfer or consumption, so nothing appears from nowhere (decision 0032, decision 0035).

## Key types and files

`Assets/Sim/Inventory/` (`LordsLegacy.Sim.Inventory`), registry `Ledger` (`SimWorld.Inventory`, partial `SimWorld.Inventory.cs`).

| Type | Role |
|---|---|
| `Ledger` | Locations, counts, reservations, journals (produced, consumed), `Version`, `Audit()`. |
| `Ledger.Reservations.cs`, `Ledger.Restore.cs` | Reservation methods and internal restore methods for the save reader. |
| `LedgerLocation`, `LocationKind` | `GroundPile` (point, optional owner operation id), `Storage` (module id, capacity from the type), `Carrier` (an ox, capacity), `Stockpile` (unlimited, camp), `Site` (construction site, footprint id). |
| `Reservation` | Id, location, item, count, owner, kind (stock or capacity). |
| `ItemDef`, `ItemCategory`, `Items` | `Items.Log` is `item.log`, category Log. |
| `OperationPool` | `SimWorld.OperationPool(opId, item)` sums the operation's own storage modules only. |

## Operations

- `Produce(location, item, n)` / `ProduceOnGround` merge into the nearest same-owner pile within 1 m. `Produce` throws past capacity.
- `Transfer(from, to, item, n, owner?)` is atomic: false and no change on failure. It consumes the matching reservations.
- `Consume` removes items for good (construction uses it), and is journalled.
- `ReserveStock` / `ReserveCapacity(loc, item, n, owner)` return a reservation id (0 = refused). `Release(id)`, `CancelOwner(owner)`.
- Per location and item: `TotalOf`, `ReservedOf`, `IncomingOf`, `AvailableOf` (total − reserved), `FreeSpace` (capacity − total − incoming).
- `Audit()` returns null when consistent. It checks location sums against the produced and consumed journals, and checks
  reservations and capacity.

## Extending

A new item is an `ItemDef` in `Items`. A new location kind needs its own save and `Audit` rules.

## Gotchas

- Unowned ground piles are construction sources. Site-owned piles are not (`construction.md`).
- `Storage` capacity comes from the module type. A removed storage's logs stay in the ledger; `RemoveModule` refuses a storage
  with logs reserved for a site.
- The save stores both counters and journals; `Audit` runs on load and throws `SaveFormatException` if they disagree.

## Tests

`Sim.Tests/LedgerTests.cs`, `LedgerReservationTests.cs`, `Sim.Tests/Operations/LogLoadTests.cs`, `Sim.Tests/Save/LedgerSectionTests.cs`.
Unity: `Assets/Tests/PlayMode/LogStacksTests.cs`.
