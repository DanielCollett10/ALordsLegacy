# 0014: Area shape model: vertices plus edge kinds, flattened outline stored

**Context.** Areas have 4–8 player points, edges that can be bent into curves, and edges that follow a road
(docs/design/areas.md). Checks need plain polygons (decision 0012). Reshaping confirmed areas comes later.

**Decision.**
- `Assets/Sim/Areas/` (`LordsLegacy.Sim.Areas`). `AreaShape` = ordered vertices (`PointMm`) plus one `AreaEdge` per
  vertex (edge i runs from vertex i to i+1). Edge kinds: `Straight`; `Bent` (one handle point that lies **on** the
  curve at its middle; the quadratic control point is `2·handle − (a+b)/2`); `FollowsRoad` (segment id + side Left/Right).
- `AreaFlattener.Flatten(shape, roads)` turns a shape into a CCW `PointMm` polygon: bent edges are sampled every
  1 m; road-following edges copy the road side line between the two vertices' nearest positions along that side.
  If the road segment no longer exists, the edge falls back to Straight.
- A confirmed area stores both the **shape** (for future reshaping, AREA-11) and the **flattened outline** (used by
  every check, the renderer and tree clearing). Deleting or reshaping a road later never changes an existing area.
- Snapping is decided in the tool (screen pixels). The Sim offers candidate queries (`AreaRegistry.FindNearestSnap`,
  road side lines) and re-validates whatever shape the tool sends. A FollowsRoad edge is only accepted if both of
  its vertices are within 5 cm of that road side; otherwise the Sim rejects the command.
- `AreaTypeDef` holds the per-type limits (points, m², slope, kind, clears trees, blocks roads), built in code until
  the definition pipeline exists.

**Consequences.** Areas are reshape-ready and independent of later road edits. The flattened outline can hold
hundreds of points, so overlap tests use the spatial index and bounding boxes first.
