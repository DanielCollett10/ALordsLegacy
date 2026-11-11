# 0029: Road end tangents are stored on the new segment, not the old one

**Context.** RD-16 says a new road that joins a dead end must leave the node in the direction the old road arrives,
so the two surfaces meet flush with no kink. Changing the old segment would mean re-sampling and re-checking a road the
player did not touch.

**Decision.**
- `RoadValidator` samples a joined curve with the outward end direction of the single segment at that node
  (`RoadTangent`) as its start or end tangent. The result is in `RoadPreview.StartTangent` / `EndTangent`.
- `DrawRoad` stores them on the new first or last `RoadSegment`. The old segment is not changed.
- Only a join at a node with exactly one non-loop, non-ignored segment gets a tangent. T, X and 2+-segment nodes get none.
- Splitting a segment (`RoadNetwork`) keeps each outer end's tangent on the half that keeps that end.

**Consequences.** A segment carries everything needed to re-sample it later (control points plus its own tangents), which
a future middle-point drag will need. Joins at busy nodes keep the free curve end, as RD-16 says.
