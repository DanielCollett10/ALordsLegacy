# 0020: Ground overlays are densely draped meshes drawn with a depth bias

**Context.** In M5 play (screenshots 1 and 4 in `docs/design/source/`), area outlines and draft lines cut through hills,
and road meshes sank under the terrain when the camera zoomed out. There are two causes:
1. `AreaRenderer` draws each straight outline edge as **one quad**, draped only at its four corners, so a long
   edge is a straight 3D line between two ground points and dives through any rise in between.
2. Unity Terrain simplifies its mesh with distance (`heightmapPixelError`, default 5). Far away, the drawn terrain sits
   up to a metre or more off the true heights, while our overlays use the exact Sim heights, so they hide under it.

**Decision.**
- Every overlay drawn on the ground (road meshes and previews, area fills, outlines, dashes, ghosts, snap dots,
  cursor markers) is built from points **no more than 1 m apart** along lines and **triangles no larger than 2 m a side**
  in fills, each point at Sim terrain height + a small lift. In plan view, straight lines stay straight and bends
  stay bent; they only follow the ground up and down. There's one shared helper (`TerrainDrape`), used by every renderer.
- Overlays use one unlit vertex-colour shader (`LordsLegacy/GroundOverlay`, with an optional texture for roads) that
  draws after the terrain with a **depth bias**, so terrain LOD error up to the bias doesn't hide them. The bias grows with
  distance from the camera (a fixed `Offset` alone isn't enough at low pitch).
- The test terrain's `heightmapPixelError` drops to **1** to shrink the LOD error at its source. Revisit if FPS
  drops below the M2 numbers.
- No projectors or decals (the built-in pipeline's Projector is costly and hard to test).

**Amendment (after T-195/T-197, overlays still sank).** Pushing a vertex a distance `s` along the view ray lifts it
only `s · sin(grazing angle)` above the ground, so the first bias (`0.05 + 0.01·dist` along the ray) gave far too
little at low camera pitch (pitch 20°, 400 m: ~1.4 m lift vs ~2 m terrain LOD error at pixel error 5). The bias is now
specified as a **vertical clearance** and converted to a push along the ray:
- `h = _BiasConst + _BiasPerDist · dist` (vertical metres; defaults `_BiasConst = 0.15`, `_BiasPerDist = 0.004`),
- `s = min(h / max(dir.y, _MinSin), dist · 0.5)`, where `dir` is the unit vector vertex→camera and `_MinSin = 0.1`.
The constant part covers the close-range gap between Unity's two-triangles-per-cell terrain and the Sim's bilinear
heights (a few cm on hills). Terrain `heightmapPixelError` = 1 (T-196) must be in place before judging the result;
the bias is a safety margin, not the main fix. Roads only benefit once they use the overlay material (T-198).

**Consequences.** Overlay meshes have more vertices (fine at today's counts; roads already sample every 1 m). New
ground visuals must use `TerrainDrape` and the overlay shader. A visual can still be overdrawn by a tall object in
front of it, and that's correct.
