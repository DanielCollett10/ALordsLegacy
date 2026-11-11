# 0028: Placement checks are pure Sim queries that the tools ask

**Context.** The owner rule (placement.md PL-01..06) is that the player must not place a point where the thing can't
go. The tools must show this live, and the rule must live in the Sim, not the UI (CLAUDE.md, decision 0001).

**Decision.**
- `PlacementRules` (`Assets/Sim/Placement/`) answers `CanPlaceAreaVertex(world, areaTypeId, point)` and
  `CanPlaceRoadPoint(world, point)` with a `PlacementResult` (Ok, or a reason and text). Both are pure queries.
- `AreaTool` and `RoadTool` ask on the snapped point (PL-05). When the answer is no, they hide the cursor marker (PL-06).
- Shape checks (AR-15, AR-16, RD-13) stay in the validators, because edges can cross things even when every point is fine.

**Consequences.** Placement rules are in one testable place. The tools hold no rules. Only the tools call
`PlacementRules`; the commands (`DrawRoad`, `DesignateArea`, `MoveRoadNode`) do not, so a point the tool allows is not
re-checked at the command. Owner to decide whether commands should re-check it.
