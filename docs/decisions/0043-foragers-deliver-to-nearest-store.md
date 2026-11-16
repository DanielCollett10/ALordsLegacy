# 0043: Foragers deliver to the nearest store with room, never the camp

**Context.** A foraging camp must work with no granary (FD-23b). The camp food store is a stop-gap that granaries must not
refill (FD-12), so foragers must not add to it.

**Decision.**
- A forage task reserves one bush (`BushRegistry.Reserve`, owner = task id) and carries up to `HandCarryItems` (10) items,
  never more than 20, to one store chosen when the task is planned.
- Candidate store is the operation's own forager hut store only (narrowed by 0044). Granaries and the camp food store are never candidates.
- Room is free space minus items already promised to unfinished forage tasks. No store with room means no new tasks.
- Each basket is one ledger `Produce` on the task's carrier. Deposit is one ledger `Transfer`, and it fails if the store stopped being Working.
- A task that ends mid-carry drops its food as a ground pile where it stands or at the bush.

**Consequences.** Foraging needs no new operation type; it is task work. Bush reservations and carriers are saved (Bushes and
Tasks sections, and the ledger). Storage full is decided by the store room, not by a counter on the operation.
See 0044: the granary candidates above were removed, so "nearest store" is now the hut store.
