# 0056: Green thatch waits in a curing registry until it is dry

**Context.** A cut reed bundle cures for 7 days where it stands. Until then it must not count as thatch, be carried, or be
taken by a house (`design/materials.md` MAT-32, MAT-39c). Putting green bundles in the ledger would need a "curing" state on
every location, fetch and reservation, and would widen what `Audit()` must check.

**Decision.**
- Green bundles are `StookRecord`s in `CuringRegistry` (position, owner operation, count, ready tick). They are not in the ledger.
- `CuringRegistry.Step` runs each tick from `CuringSystem`. When a stook is dry it produces its count as thatch in an
  operation ground pile at the stook (journalled, `ProduceOnGround`) and removes the record.
- The registry has its own `Version`, `Audit()`, and a save section (`CuringSection`, id 12, version 37).

**Consequences.**
- Green stooks are invisible to the ledger and to every fetch, so nothing can take them by mistake.
- A new drying or curing process should follow the same pattern: a registry that produces into the ledger when done.
- Views draw stooks from `Curing.Version`, not from the ledger.
