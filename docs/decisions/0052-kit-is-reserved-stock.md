# 0052: Kit stores hold real items; a trip borrows one by reservation

**Context.** Baskets double a carrier's load (`design/materials.md` MAT-50..54). They are claimed by buildings into a
kit store (`design/storage.md` ST-20..25), fetched by the building's own workers, and dropped on the ground if the building goes.

**Decision.**
- A kit store is a `GoodsStore` with `ModuleId` = the building and a **per-item slot count** from the building's def
  (forager hut 5 baskets, granary 2). It is never a construction, workshop or yard-collect source, but it counts
  in village totals.
- Claims: a building with free slots and a basket available in a General store (camp, yard, weaver's store) opens a kit fetch for its own
  family. Claims are served oldest first by building id order of first claim tick (ST-24).
- A carry trip that can use a basket **reserves one basket** in the kit store at plan time and releases it when the
  task ends, so the basket never moves in the ledger. The plan uses the doubled load only with a reservation.
- On removal of the building, the kit store's items become an unowned ground pile at its access point (ST-23).

**Consequences.** No per-person equipment state. Later tools (axes, saws) reuse the same kit store and reservation.
