# Geometry

**Purpose:** exact 2D geometry for roads, areas, footprints and placement. Integer millimetres, so predicates are exact (decision 0012).

## Key types and files

All in `Assets/Sim/Geometry/` (`LordsLegacy.Sim.Geometry`).

| Type | Role |
|---|---|
| `PointMm` | Integer-mm point. The unit for polygons and polylines in the Sim. |
| `Segments` | Exact classify and intersect, `MeetingPoint` for one crossing pair. |
| `Polygons` | Area, winding, `IsSimple`, `Locate` (point in polygon), bounds, `Crossings` (where non-adjacent edges touch or cross, one point per pair). |
| `Polylines` | Length, nearest point, min distance, `PointsEvery(line, spacingM)` at arc length 0, s, 2s…, `PointsEveryWithin(...)` (snap dots, AR-22). Throws for spacing ≤ 0. |
| `Triangulator` | `EarClip`: n−2 integer-exact triangles. Collinear vertices give zero-area triangles. |
| `PolygonBoolean` | `Difference(subject, clips, out holes)` via vendored Clipper2 (`Assets/Sim/ThirdParty/Clipper2/`, decision 0021). Clipper types stay private. |
| `PolygonOverlap` | `Overlaps`: tolerance overlap test (area above a tolerance). |
| `SpatialGrid<T>` | Uniform-grid index over `BoundsMm`, cell size in metres. `Insert`, `Remove`, `QueryRect`, `QueryPoint`, each item once in insertion order. Used for building areas. |

Predicates are exact. Distances and areas are doubles in mm.

## Rules

- Polygon winding in the Sim is CCW for outlines.
- Clipper results round new crossing vertices to the nearest mm.
- Anything the Unity layer needs to draw (metres) is converted at the edge, not in the Sim.

## Extending

Add pure functions to the right static class. Keep them free of state and engine references. Add a `Sim.Tests` case
with a hand-computed answer, especially for edge cases (collinear, touching, zero-length).

## Gotchas

- Touching edges count as not overlapping, within the tolerance used by each caller.
- `Polylines.PointsEvery` returns no points for a zero-length line.

## Tests

`Sim.Tests/GeometryTests.cs`, `PolygonBooleanTests.cs`, `PolygonCrossingsTests.cs`, `PolygonOverlapTests.cs`,
`PolylineSpacingTests.cs`, `TriangulatorTests.cs`, `SpatialGridTests.cs`.
