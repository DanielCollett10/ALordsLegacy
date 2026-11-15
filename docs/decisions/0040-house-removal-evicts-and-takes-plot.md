# 0040: A house leaves with its family and its plot

**Context.** Houses have no operation, so the operation-based deconstruction path (CN-58) does not apply to them. Their plot is
an area that the player can delete. Homes (HM-14, HM-14b) need the family back in a tent and the plot gone with the house.

**Decision.**
- Marking a built house for deconstruction evicts its family at once (like CN-56 for huts): the home becomes Empty, the
  family Homeless, and its home point moves to the camp fire (`HouseRemoval.Evict`).
- Finishing a house deconstruction, or cancelling an unbuilt house site, removes the footprint, the home and the plot area
  (`HouseRemoval.RemoveWithPlot`). The refund pile is the same as for any module (CN-54).
- A house's deconstruct tasks use owner id `DeconstructionWork.HouseOwnerId` (-1). They are helpers-only, so the owner only
  matters for the save and the task board.
- `DeleteArea` refuses a plot that still has a home: "Deconstruct the house first" (HM-14b).

**Consequences.** No new saved state: the marked house keeps its footprint and entry, and the Homes section stores family 0.
The tent spot is not saved, so an evicted family gets the camp fire as its home point. A later task can store tent spots.
