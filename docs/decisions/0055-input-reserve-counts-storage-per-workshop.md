# 0055: The workshop input reserve counts storage only and is set per workshop

**Context.** Without a reserve, hewers and sawyers take every log and beam they can reach, and the village has none left for
sites and the yard (`design/planks.md` PL-30). Counting every log on the ground would let a workshop's own dropped logs
block it, and counting in-transit goods would let several trips overshoot together.

**Decision.**
- `InputReserve` counts only what is held in stores: logs in log storage and the camp stockpile, beams in workshop and yard
  GoodsStores, rods in coppice-hut and yard stores, reserved or not (PL-31). Piles, input stocks, sites and carried items are not counted.
- A fetch opens only if storage − fetches already on the way − load ≥ reserve. Fetched work always finishes (PL-33).
- The reserve is per operation and per input in `Operation.Reserves`, set by `SetWorkshopReserve`. An unset input uses
  `WorkshopDefaultReserve` (5). 0 means no reserve. Saved in the Structures section.

**Consequences.**
- A reserve above what the player can store makes the workshop wait; it is not a deadlock in the rules (PL-34).
- Any new store kind that holds an input must be added to `InputReserve.Counts`, or its goods will not count.
