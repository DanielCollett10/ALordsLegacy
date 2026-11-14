# 0035: Haul tasks reserve stock and room in the ledger when they open

**Context.** M9 moves logs from ground piles and oxen into storage while felling carries on. Two tasks could claim
the same logs or the same storage room, and a haul can be cut short or cancelled at any step. Counting in-flight
logs in each system would count some of them twice.

**Decision.**
- Stock and room are claimed in `Ledger`, not in task state. `ReserveStock` takes available logs at a location;
  `ReserveCapacity` takes free room in a storage. Each returns a reservation id (0 = refused). The owner is the task id.
- A haul reserves its load and its storage room when it opens (`HaulSystem`). An early haul (T-291) reserves its
  ox and room at once, and reserves the pile's stock when its fell task is Done.
- `Ledger.Transfer` is atomic: it consumes the matching reservations, and a failed transfer changes nothing.
- Cancelling a task releases its reservations (`Ledger.CancelOwner`), so none outlive their task.
- `Audit()` checks reservations and capacity as well as counts.
- `OperationStatus.LogLoad` counts only logs not already backed by a pile or a carrier.

**Consequences.** Save/load has to store reservations with their owners (decision 0034 lists the ledger without them
yet). Any new carrier or storage type must reserve through `Ledger`, never by changing counts directly.
