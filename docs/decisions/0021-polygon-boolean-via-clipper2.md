# 0021: Polygon difference uses a vendored Clipper2, wrapped in Sim geometry

**Context.** AR-34 (areas.md) trims a new building area against the neighbours it overlaps, which is a polygon
difference. Neighbours often share edges and corners exactly (snapped, terraced plots). Hand-written clipping
(Greiner-Hormann and the like) is notoriously fragile in exactly those cases. Our geometry is integer millimetres
(decision 0012).

**Decision.**
- Vendor the C# **Clipper2** library (Angus Johnson, Boost Software License 1.0) as source under
  `Assets/Sim/ThirdParty/Clipper2/`, with its licence file. It's pure C# with integer (`Point64`) coordinates and no
  Unity references, so it fits `noEngineReferences` and is picked up by `Sim.Tests` automatically.
- Sim code never calls Clipper directly. `LordsLegacy.Sim.Geometry.PolygonBoolean` wraps it, taking and returning
  `PointMm` polygons (CCW, no repeated closing vertex). Today it has one operation, `Difference(subject, clips)`.
- Pin the vendored version in `ThirdParty/Clipper2/VERSION.txt`. Don't modify the vendored files. If something has to
  change for C# 9 or Unity, record it in that file.

Owner approved the dependency (M6 planning): it's free, including for commercial use. Any future outside library
in the Sim must also be free, permissively licensed, pure C#, and recorded in a decision like this one.

**Consequences.** Robust trimming with shared edges. Later uses (plot splitting, union of work areas, fences) go
through the same wrapper. One external source drop to keep: an upgrade is a deliberate task, not a drive-by.
