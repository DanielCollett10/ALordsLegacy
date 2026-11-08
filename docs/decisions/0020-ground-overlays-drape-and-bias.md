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

**Consequences.** Overlay meshes have more vertices (fine at today's counts; roads already sample every 1 m). New
ground visuals must use `TerrainDrape` and the overlay shader. A visual can still be overdrawn by a tall object in
front of it, and that's correct.
