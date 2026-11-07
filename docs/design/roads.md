# Roads (MVP-0.5, milestone M3)

Implements GDD section 4 (ROAD-01..12) and tech spec section 10 (NAV-01..06), with the owner
decisions below. **Where this file and the GDD disagree, this file wins.**
Technical shape: decisions 0011 (commands), 0012 (geometry) and 0013 (road model).

## What the player does

1. Click **Road** on the build bar. The road tool is active until the player clicks Road again,
   picks another tool, or presses Escape with no road in progress.
2. **Left-click** places the first point. Each further left-click adds a point. The preview is a
   smooth curve that **passes through every clicked point** and lies on the terrain.
3. While drawing, the player can **drag** any placed point to move it, and **Backspace** removes the
   last point.
4. **Double-click** places a final point at the cursor and finishes the road (the double-click adds one point,
   not two). **Enter** or the panel's **Confirm** finishes without adding a point.
   **Right-click** or **Escape** cancels the road in progress.
5. With no road in progress, **right-click on a road deletes the road segment** under the cursor.
   No popup and no confirmation.
6. Left-clicking on an existing road starts a new road from that point (a branch, which makes a junction).

7. (M5) With no road in progress, **left-drag a node** (a road end or a junction) to move it. Every road attached to it
   re-shapes live, then is checked and kept or put back on release (RD-17).

Dragging a confirmed road's **middle** control points is later work. **The data must allow it**: every segment keeps
its own control points (decision 0013), so a future `ReshapeRoadSegment` command only re-samples and re-validates one segment.

## Rules

| ID | Rule | Tag |
| --- | --- | --- |
| RD-01 | The curve is a **centripetal Catmull-Rom** spline through the clicked points (control points). The curve type is a field on the road, so a Bézier variant can be tried later without changing stored roads or the network | DECIDED |
| RD-02 | A road needs at least 2 control points. A control point closer than **1 m** to the previous one is ignored, except the **last** point: if it is within 1 m of the previous one, it replaces that point, so the road always ends where the player clicked | DECIDED (distance PLACEHOLDER) |
| RD-03 | The Sim samples the curve into a centre line every **1 m** of arc length. Heights come from the terrain, never stored (ROAD-02, NAV-02) | DECIDED (spacing PLACEHOLDER) |
| RD-04 | Every road has a **road type**. MVP has one: `road.dirt_path`, width **2.5 m** (2–3 people abreast), free (ROAD-12). Wider and better surfaces (gravel, cobble) come later as new types | DECIDED (width PLACEHOLDER) |
| RD-05 | **Grade check:** each 1 m sample step whose rise ÷ run is over the type's max grade (**15%**) is invalid. Invalid stretches show red in the preview, and the road cannot be confirmed (ROAD-09, NAV-03) | DECIDED (value PLACEHOLDER) |
| RD-06 | **Corridor:** each road has a corridor = centre line ± half width (NAV-06). Used for overlap checks, tree clearing and area checks | DECIDED |
| RD-07 | **Junctions:** if a new road's first or last point is within half a width of another road's centre line, it joins there (T-junction). If its centre line crosses another road's centre line, a junction is made at the crossing (X-junction). Both split the existing segment in two (ROAD-05, NAV-04). This happens whether or not snapping is on; snapping only helps the cursor land there | DECIDED |
| RD-08 | A road may not cross **itself** (refused: "crosses itself") | PROPOSED |
| RD-09 | A new road's corridor may not overlap another road's corridor except within **one road width** of a junction it makes (stops roads running on top of each other). Refused: "overlaps road" | PROPOSED |
| RD-10 | The whole centre line must be inside the map (refused: "outside the map") | DECIDED |
| RD-11 | **Trees in the corridor are removed** on confirmation: any tree whose trunk is within half width + **0.5 m** of the centre line. They yield nothing and simply disappear (same rule as BLD-10-A) | DECIDED (MVP; clearance PLACEHOLDER) |
| RD-12 | **Delete:** right-click removes one **segment** (the stretch between two nodes: road ends or junctions). A node left with no segments is removed. Trees do not come back (no regrowth exists; see `gdd-amendments.md` BLD-10-D) | DECIDED |
| RD-13 | Roads may not cross building areas or plots (ROAD-08). Checked: refused with "crosses <area name>". Work areas don't block roads | DECIDED |
| RD-14 | No pre-placed (baked) trunk road. Any starting road (WORLD-08) is an ordinary road created when the game starts, and the player can delete or reroute it. Not needed in M3 | DECIDED |
| RD-15 | Commands work while the game is paused | DECIDED |
| RD-16 | **Extending a dead end (M5):** if a new road's first or last point joins a node with **exactly one** segment (a dead end, via RD-07 or snapping), the two roads meet **flush, like two pipes end to end**. The new curve leaves the node in the same direction the old road arrives (tangent within **1°**), so there's no kink and the road surfaces meet with no gap or overlap. The old segment isn't changed. T-junctions, X-junctions and branches from a node with 2+ segments are exempt and keep the free curve end they have now | DECIDED |
| RD-17 | **Move a node (M5):** command `MoveRoadNode(nodeId, position)`. Each attached segment keeps its other control points, and its end on that node moves to the new position (RD-02 applies to the point next to it). At a node with exactly 2 segments, both curves leave the node along one shared direction, so a smooth joint stays smooth. Each re-shaped segment is checked like a new road (RD-05, RD-08..10, RD-13), and the attached segments are ignored when checking each other near the node. A move that would make a **new junction** (cross another road, or land on one) is refused: "would join a road" (joining by dragging is later work). On success, the segments get new ids and the node keeps its id, trees in the new corridors are removed (RD-11), and areas keep their stored outlines (AR-05). Works while paused | DECIDED |
| RD-18 | **Cursor ring (M5, presentation only):** while the road tool is active, a ring as wide as the road lies on the ground at the cursor, showing where the next point will go. When the point would snap, the ring jumps to the snap point and changes colour. It's hidden while the pointer is over UI | DECIDED |

Refusal reasons are shown in the tool panel, and the first one found is the one shown. Order: outside
the map, too steep, crosses itself, overlaps road, crosses area, would join a road (moves only).

Verified (T-119 replay): left-clicking an existing road starts a branch and makes a junction (item 6). No change needed.

> Implementation note (RD-03, sample spacing): each control-point span is split into `round(span length ÷ 1 m)` equal
> steps, so the spacing is close to 1 m, not exact.
>
> Implementation note (RD-09, exemption): besides the samples within one width of a junction, the code also exempts
> samples that move away from the junction while they get further from the road, until they are a full width away
> (`RoadValidator.Walk`). The rule above does not mention this. Owner to confirm.

## Network

- **Node:** a road end or junction, at an x/z position (mm).
- **Segment:** one stretch of road between two nodes, with its road type, its own sampled centre line,
  its length (m) and its corridor. A segment belongs to the road command that drew it (for debugging only;
  the player deletes segments, not "roads").
- Splitting a segment at a junction keeps the curve shape exactly: both halves keep the same centre-line
  samples, cut at the junction point.

> Implementation note (segment fields): a `RoadSegment` stores its id, nodes, type id, curve kind, control points,
> samples and length. It does **not** store a corridor (the corridor is the centre line ± half the type width, worked
> out when needed) and does not record which command drew it. Owner to decide whether to store either.
- Two nodes are "connected" if a chain of segments links them. The debug overlay can show node and segment ids.

## Snapping (shared with areas, GEO-13..15)

- One toggle in the top bar, **Snapping: On/Off**, default On. Pressing **Alt** toggles it too (same as
  clicking the button; it stays changed until toggled again). Not a hold.
- Road snap targets (in this order): road ends, points on a road's centre line, and later area vertices
  and area edges (M4). Snap distance: **16 screen pixels** for the road tool from M5 (was 12; area tools stay at 12).
  Tool-side only, PLACEHOLDER.

> Implementation note (road snapping to areas): `RoadTool` does not snap to area vertices or area edges yet; only the
> area tools do (`AreaTool`). Owner to confirm whether the road tool should snap to them.

## Out of scope for M3

Dragging a confirmed road's middle control points (nodes are draggable from M5), joining roads by dragging, speed bonus on roads (needs movement), road upgrades, bridges,
saving, and tree-felling jobs for clearing (future: woodcutters fell and haul the timber; see `gdd-amendments.md`).
