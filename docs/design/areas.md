# Areas (MVP-0.5, milestone M4)

Implements GDD section 3 (AREA, WORK) and tech spec section 9 (GEO), with the owner decisions
below. **Where this file and the GDD disagree, this file wins.** Geometry: decision 0012.

Status: built in M4 (see `docs/architecture.md`, Areas). Reshaping a confirmed area is not built.

## What the player does

1. Click **Test area** (or **Test work area**) on the build bar.
2. **Left-click** places points one at a time: point 1, point 2 draws a line, point 3 shows a
   triangle preview (not yet a valid area), point 4 makes a four-sided shape with any angles (not forced
   square). More points can follow, up to the maximum. The preview closes back to point 1 automatically.
3. **Close** the shape (only once it has at least 4 points) with a double-click, **Enter**, a click on point 1, or
   the panel's **Confirm**.
4. Reshape before confirming:
   - **Drag a point** to move it.
   - **Click on an edge** to insert a point there.
   - **Drag an edge's middle handle** to bend that edge into a curve. Double-click the handle to straighten it again.
   - **Right-click a point** to remove it (not below the minimum).
5. **Confirm** creates the area. **Escape**, the **Cancel** button, or a right-click on empty ground cancels the draft.
6. With no draft in progress, **right-click on an area of the active tool's kind deletes it**. No popup.

Reshaping a confirmed area (AREA-11) is later work.

> Implementation note (right-click removes a point): `AreaTool` removes the point with no minimum check, so the draft
> can drop below 4 points; it then shows "too few points" instead of refusing the click. Owner to decide which is right.

## Rules

| ID | Rule | Tag |
| --- | --- | --- |
| AR-01 | An area is a closed polygon of **vertices** joined by **edges**. Each edge is *straight*, *bent* (curve), *follows road* or (M5) *near road* (AR-23) | DECIDED |
| AR-02 | Points: minimum **4**, maximum **8** per area (from the area type). An area with fewer than 4 points is never complete: no 3-point areas. Only vertices count; bent and road-following edges add no points, so curves are how players get rounder shapes | DECIDED (max PLACEHOLDER, may be lowered after play) |
| AR-03 | **Bent edge:** a quadratic curve between its two vertices. The handle the player drags lies **on** the curve at its midpoint, so the edge goes through where the mouse is | DECIDED |
| AR-04 | **Follows road:** with snapping on, when two consecutive vertices snap to the **same side** of the same road segment, the edge between them follows that road side's curve exactly (AREA-05, GEO-14) | DECIDED |
| AR-05 | For every check the Sim **flattens** the shape into a plain polygon (integer mm): bent edges are sampled every **1 m**, followed road edges use the road side's samples. The flattened outline is stored with the area, so deleting the road later doesn't change the area | DECIDED (spacing PLACEHOLDER) |
| AR-06 | Areas lie on the terrain: outlines are drawn draped on the ground and heights are never stored (AREA-07, GEO-01) | DECIDED |

## Validation (checked live in the tool and again in the Sim)

Checks run on the flattened polygon. The first failing check (in this order) is the reason shown.

| ID | Check | Test area | Test work area |
| --- | --- | --- | --- |
| AR-10 | Too few points (< min) / too many (> max) | yes | yes |
| AR-11 | Self-intersection or zero-length edge: "edges cross" (GEO-06) | yes | yes |
| AR-12 | Outside the map | yes | yes |
| AR-13 | Too small / too large (m²): test area **100–5,000** | yes | **100–40,000** |
| AR-14 | Too steep: terrain slope sampled on a **2 m** grid inside the polygon; any sample over **10°** fails (GEO-08) | yes | no |
| AR-15 | Overlaps another building area: "overlaps <name>". **Touching (a shared edge) is allowed.** Overlap smaller than **0.1 m²** counts as touching, to absorb rounding on snapped curves (GEO-09) | yes | no |
| AR-16 | Crosses a road: the polygon overlaps a road corridor by more than 0.1 m² (GEO-10). Lying flush against the road edge is fine | yes | no |

Minimum width (GEO-07, "no thin slivers") is deferred.

**AR-11 auto-fix (T-161, owner's call):** when a draft of all-straight edges crosses itself, the tool reorders the points by angle around their centroid (`AreaUntangler`, point 1 stays first) if that gives a simple polygon. Drafts with bent or road-following edges are not reordered; they stay refused ("edges cross", Confirm disabled).

**AR-11 bug (M5):** in play, the owner drew an hourglass by clicking four points in a crossing order (a figure-8 with crossing edges) and it wasn't refused.
"Edges cross" must catch **every** self-crossing: crossings between straight edges, between bent edges, and between
road-following edges, however the draft got there (clicking, dragging a point, inserting a point, bending an edge or
removing a point). The preview turns red, the panel shows "edges cross", Confirm is disabled, and `DesignateArea` rejects it.

## Drawing aids (M5, presentation only, decision 0017)

| ID | Rule | Tag |
| --- | --- | --- |
| AR-20 | **Cursor sphere:** while an area tool is active, a small sphere (**0.6 m**) sits on the ground at the point the next click would place: the snapped point if snapping applies, otherwise the cursor. It changes colour when snapped and is hidden over UI | DECIDED |
| AR-21 | **Ghost polygon:** once the draft has 1+ points and the player isn't dragging, the preview also draws faint ghost edges from the last point to the cursor sphere and from the sphere back to point 1. That's the shape you'd get by clicking there. The ghost isn't validated; the panel status describes the placed points only. **Deferred (T-164 playtest):** built (`AreaRenderer.ShowGhost`) but switched off behind `AreaTool.GhostEnabled` (default false). Playtest: it followed the mouse over the panel buttons and ignored bent and road-following edges, so it needs polish before it is enabled | DECIDED |
| AR-22 | **Snap dots and corners:** with snapping on, everything snappable within **25 m** of the cursor shows markers. **Corners** (other areas' vertices, of any kind) show as larger markers. **Dots** sit every **2.5 m** along road side lines (measured from the segment's node A end) and along other areas' edges (measured along the flattened outline from each edge's start vertex, so every edge starts on its corner). Dots don't slide as the cursor moves. A vertex snaps to the nearest marker within the snap distance (12 px) instead of any point on a line. Order: corner, then road-side dot, then area-edge dot. Your own draft's points aren't snap targets (they're for dragging) | DECIDED |
| AR-23 | **Edge bends to match a nearby road:** with snapping on, when one end of an edge is snapped to a road side and the other end is within **10 m** of the same side (on the side away from the road, not snapped to it), the edge becomes **NearRoad**. **Both points stay where the player clicked.** The edge copies the road's curve, starting on the road at the snapped end and drifting evenly (linear by length along the road) out to the other point, so it runs parallel-ish to the road instead of cutting a straight chord across a bend. It's checked when either end is placed or dragged, and for the closing edge on close. Beyond 10 m, or snapping off, the edge stays Straight. Double-clicking its handle straightens it (like a bent edge) | DECIDED |
| AR-24 | **NearRoad in the Sim:** edge kind `NearRoad` (segment id + side), flattened per decision 0014's amendment: road side samples between the two vertices' nearest positions on the side, each offset outward by a distance interpolated from vertex A's to vertex B's distance. Refused as "not on road" if either vertex is more than 10 m from the side, is on the road side of the line, or the segment is missing at validation. All other checks (edges cross, crosses road, overlap...) run on the flattened outline as usual | DECIDED |
| AR-25 | **NearArea (T-166):** the same bend works against another area's curved outline. With snapping on, when one end of an edge is snapped to another area's edge or corner and the other end is outside that area within **10 m** of its outline and not snapped to it, the edge becomes **NearArea** (area id). It follows the neighbour's flattened outline the shorter way round between the two nearest positions, tapering outward like NearRoad. Both points stay where clicked. Refused as "not on road" if the neighbour is missing or a vertex is inside it or beyond 10 m. Double-clicking its handle straightens it | DECIDED |

## Area kinds and effects

| Kind | Example now | Blocks other building areas | Blocks roads | Clears trees |
| --- | --- | --- | --- | --- |
| Building area | Test area (`area.test`) | yes | yes (RD-13) | yes, on confirmation |
| Work area | Test work area (`area.test_work`) | no, may overlap anything (WORK-03, WORK-05) | no | **never** |

- **Tree clearing:** every tree whose trunk lies inside a confirmed building area is removed, yielding nothing
  (BLD-10-A). Work areas never clear trees or land.
- Test work areas are always visible in M4. Hiding them until their building's panel is open (WORK-04) arrives with real buildings.
- Area names: "Test area 1", "Test area 2"… numbered per type, never reused in a session.

## Snapping (shared toggle, see `roads.md`)

Area snap targets (in this order): area vertices, road edges (the side of the corridor, not the centre line),
other area edges. Snap distance 12 screen pixels. Snapping to a neighbouring area's edge gives terraced plots
that touch without overlapping.

## Out of scope for M4

Reshaping after confirmation, modules inside areas, fences, plot analysis (frontage, backyard), saving,
and land-clearing jobs.
