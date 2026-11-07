# 0013: Road model: control points, curve kind, sampled segments, graph

**Context.** The owner chose curves that pass through clicked points (centripetal Catmull-Rom) and may want to
try Bézier curves later. Roads must join, split at junctions and be deletable per segment (docs/design/roads.md).

**Decision.**
- `Assets/Sim/Roads/` (`LordsLegacy.Sim.Roads`). A `DrawRoad` command carries control points (mm), a road type id
  and a `CurveKind` (only `CatmullRomCentripetal` for now).
- `ICurveSampler` turns control points into a centre line sampled every 1 m of arc length. One implementation per
  `CurveKind`. Trying Bézier later means adding a sampler and tool input for handles; stored segments and the
  network don't change.
- After validation the Sim stores **segments** (node A, node B, road type id, curve kind, **control points**, sampled
  centre line, length, corridor) and **nodes** (position, attached segment ids) in a `RoadNetwork`.
- Junctions split a segment by cutting its sampled centre line at the junction point, so the drawn shape doesn't move.
  The control points are split too: the first half keeps the control points before the cut plus the cut point as its
  last control point; the second half starts with the cut point and keeps the rest. Samples stay exactly as they were
  until the segment is reshaped.
- Every segment's first and last control points are its two node positions.
- Road types (`RoadTypeDef`: id, width, max grade, speed multiplier, visual id) come from a definitions list built in
  code until the definition pipeline (DEF-01) exists.
- The Unity road view builds a mesh per segment from the samples and terrain heights, and rebuilds when the
  network's `Version` changes.

**Consequences.** Reshaping a confirmed road (owner: "design around it being reshaped") will be a
`ReshapeRoadSegment(segmentId, controlPoints)` command: re-sample with the segment's curve kind, re-validate, keep the
end nodes (moving an end node moves every attached segment's end control point). Not built yet, but nothing in the
model blocks it. After a reshape, a half that was split from a longer road may curve slightly differently near the
cut; that is accepted. The pathfinder (later) reads segments and corridors only.
