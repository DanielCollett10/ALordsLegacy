# 0012: 2D geometry in integer millimetres; curves are flattened

**Context.** Area overlap, road crossing and junction rules must give the same answer every time,
in the tool preview and in the Sim (GEO-02, CMD-02). Floating-point polygon maths drifts.

**Decision.**
- `Assets/Sim/Geometry/` (`LordsLegacy.Sim.Geometry`) holds pure 2D maths on the ground plane (x, z).
  Points are `PointMm` (two `long` values in millimetres). Metres are converted at the edge (`FromMetres`, rounded).
- Cross products use `long`: coordinates up to 25 km = 2.5e7 mm, so products stay near 1e15, far below the
  `long` limit. Exact yes/no tests (segment crossing, point in polygon, simple polygon) use only integers.
- **Overlap area** is the one tolerance-based test. Algorithm: ear-clip triangulate both polygons (integer
  orientation tests), then for each pair of triangles whose bounding boxes overlap, clip one convex triangle by the
  other (Sutherland–Hodgman) in `double` and sum the areas. Compare the total with the tolerance (0.1 m², areas.md
  AR-15). Shared edges and touching vertices give near-zero area, so they count as touching: there are no special
  cases for degenerate shapes. The same inputs always give the same result, so the preview and the Sim agree.
  General concave clipping (Greiner–Hormann, Weiler–Atherton) is not used.
- Polygons are stored counter-clockwise (GEO-03); commands with clockwise input are normalised, not rejected.
- Curves (road centre lines, bent area edges, road-following edges) are **flattened** into points at fixed spacing
  before any check. Checks only ever see straight segments.
- Overlap and touching use polygon intersection area with a small tolerance (areas.md AR-15), not exact touching tests.
- A uniform-grid spatial index (`SpatialGrid`, cell 32 m PLACEHOLDER) holds area outlines and road corridors for
  "what is here" and "what overlaps this" queries (GEO-16).

**Consequences.** Results are exact and repeatable; tests use whole-mm coordinates. Unity code converts
`Vector3` to `PointMm` once and never does its own geometry checks.
