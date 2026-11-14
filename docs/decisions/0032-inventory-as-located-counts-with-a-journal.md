# 0032: Inventory is located counts, entered only through Produce, checked by a journal

**Context.** M8 creates logs on the ground and in storage, and LED-01/09 require every item to be located and the
world to prove nothing appeared or vanished by accident. M9 will move, reserve and carry items, so the model has to
allow that without rework.

**Decision.**
- `Ledger` (`SimWorld.Inventory`, `Assets/Sim/Inventory/`) holds `LedgerLocation`s. A location is either a ground pile
  (a point plus an optional owner operation id) or a storage bound to a module id. Each holds counts per item id.
- There are no per-item entities. A log has no identity; the renderer draws counts (`LogPileRenderer`).
- Items enter only through `Produce` / `ProduceOnGround`. Ground piles with the same owner within 1 m merge.
  Each `Produce` also adds to a journal of totals made.
- `Audit()` returns null when every location's counts sum to the journal and no count is negative. It is a debug check.
- Removal and transfer were added in M9 (T-286): `Transfer` and reservations live in `Ledger`, and `Audit` also checks
  reservations and capacity. See 0035. Nothing may bypass `Audit`.

**Consequences.** Save/load is a plain list of locations and counts. Operations read their own load from the ledger
(`OperationStatus.LogLoad`). Deleting an area leaves its ledger locations in place (known gap). Any code that changes
counts outside `Ledger` breaks the audit, which is the intent.
