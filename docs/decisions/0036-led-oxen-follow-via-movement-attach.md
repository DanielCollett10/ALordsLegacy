# 0036: A led ox follows its handler through MovementSystem.Attach

**Context.** An ox moves only when a worker leads it (MOV-16, WCO-06). The handler and the ox must walk together at
ox speed, the logs must travel with the ox, and a second path planner for oxen is not wanted.

**Decision.**
- `MovementSystem.Attach(follower, leader, offsetM = 1.5)` ties an ox to its handler through `IMovable.Follow`
  (a `FollowLink`). `Detach` ends it, and attaching drops any move order the ox had.
- While attached, the leader moves at min(own speed, follower speed) × its cell multiplier.
- After all agents have moved, the follower is placed `offsetM` behind the leader's facing. If that cell is blocked
  or off the map, the follower moves in to 75%, 50%, 25% of the offset, then onto the leader.
- The handler's task steps decide where the pair goes: `FetchOx`, `LeadTo(point)`, `ReturnOx`. `LoadOx` and
  `UnloadOx` move logs between the ledger and the ox's carrier.
- Oxen state (`OxState`: Free, Reserved, Led, Resting) is changed by these steps and `OxPool`, not by movement.

**Consequences.** An ox's path is its handler's path, so a re-plan of the handler moves the ox too. Stand occupancy
and rest spots belong to `OxPool`. A future ox operator (WC-36) can reuse `Attach` unchanged.
