# 0054: Yard jobs are an ordered list of sources; the planner holds no rules

**Context.** Storage yard families carry goods from four places (camp General store, loose piles, workshop output, beams by
ox), in a fixed priority (`design/storage.md` ST-40..43). Those rules change often during design, and the planner must not
grow a branch for each one.

**Decision.**
- `YardFamilySystem` asks `IYardJobSource` objects in order. The first usable `YardTrip` becomes a `task.haul_by_hand`.
  `YardJobs.Standard()` is the one list the game runs.
- A source returns a trip (count, source, destination, item). The planner trims it to the hand load, the source's free
  stock and the destination's room, then reserves both ends before the carrier leaves (ST-44).
- A source that needs an ox (`YardBeamSource`) makes its own ox task and returns null. The planner does not know about oxen.

**Consequences.**
- A new job is a new class plus one line in `YardJobs.Standard()`. Order is the priority.
- Sources must be pure reads except for their own task. A failed reservation cancels the task and moves on to the next source.
- `StorageYardReport` reads the same rules (`YardRoute`) so the panel status matches what the planner does.
