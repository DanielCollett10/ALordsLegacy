# 0022: Navigation grid, A* and tick movement in the Sim

**Context.** M7 needs citizens to walk over the terrain, faster on roads, around blocking footprints
(`movement.md`). The M2 perf spike tried a cost grid with A* in plain C#, but its findings weren't kept.
The owner chose to carry on with plain C# (decision 0002) rather than wait for the numbers.

**Decision.**
- `NavGrid` (Sim) covers the terrain bounds with **2 m cells** (`NavCellSizeM`). Each cell stores
  `Blocked` (slope over `MaxWalkSlopeDeg` at its centre, or its centre inside a blocking footprint) and a
  speed multiplier (the highest one among road corridors containing the centre, otherwise 1). The slope part
  is computed once. Road and footprint parts are rebuilt only for the cells under a changed road or footprint,
  and `Version` goes up.
- `Pathfinder`: 8-neighbour A* over travel time (cell distance ÷ multiplier). Diagonals can't cut a blocked
  corner. The heuristic is straight-line distance ÷ the highest multiplier. Then the path is smoothed
  (MOV-11). The output is a polyline in metres. It's a pure query, with nodes visited in a fixed order.
- Movement: each tick, every moving agent advances along its polyline by speed × the multiplier at its
  position. Positions are `double` metres (same-machine reproducibility, CLK-07, is enough). When the
  `NavGrid.Version` changes, every agent re-plans.
- No path cache in M7 (10 citizens). The cache from NAV-10 is added when profiling asks for it. `IPathfinder`
  is the seam for that, and for the Burst escape hatch in decision 0002.

**Consequences.** About 1M cells for the 2 km map (a few MB). Larger maps need FG-02 or a coarse layer (NAV-09).
Slope never enters movement maths (MOV-01). Agents are plain records in Sim registries, and Unity only draws them.
