# 0013: Road model: control points, curve kind, sampled segments, graph

**Context.** The owner chose curves that pass through clicked points (centripetal Catmull-Rom) and may want to
try Bézier curves later. Roads must join, split at junctions and be deletable per segment (docs/design/roads.md).

**Decision.**
- `Assets/Sim/Roads/` (`LordsLegacy.Sim.Roads`). A `DrawRoad` command carries control points (mm), a road type id
  and a `CurveKind` (only `CatmullRomCentripetal` for now).
- `ICurveSampler` turns control points into a centre line sampled every 1 m of arc length. One implementation per
  `CurveKind`. Trying Bézier later means adding a sampler and tool input for handles; stored segments and the
  network don't change.
- After validation the Sim stores **segments** (node A, node B, road type id, sampled centre line, length, corridor)
  and **nodes** (position, attached segment ids) in a `RoadNetwork`. Junctions split segments by cutting the sampled
  centre line; the original control points are not kept on the segment.
- Road types (`RoadTypeDef`: id, width, max grade, speed multiplier, visual id) come from a definitions list built in
  code until the definition pipeline (DEF-01) exists.
- The Unity road view builds a mesh per segment from the samples and terrain heights, and rebuilds when the
  network's `Version` changes.

**Consequences.** Editing a confirmed road's shape later needs either kept control points or editing samples;
that is deferred. The pathfinder (later) reads segments and corridors only.
