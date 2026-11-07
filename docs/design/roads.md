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
4. **Double-click** (on the last point) or **Enter** or the panel's **Confirm** finishes the road.
   **Right-click** or **Escape** cancels the road in progress.
5. With no road in progress, **right-click on a road deletes the road segment** under the cursor.
   No popup and no confirmation.
6. Left-clicking on an existing road starts a new road from that point (a branch, which makes a junction).

Editing the shape of a confirmed road (dragging its points) is later work. For now the player deletes and redraws.

## Rules

| ID | Rule | Tag |
| --- | --- | --- |
| RD-01 | The curve is a **centripetal Catmull-Rom** spline through the clicked points (control points). The curve type is a field on the road, so a Bézier variant can be tried later without changing stored roads or the network | DECIDED |
| RD-02 | A road needs at least 2 control points. Control points closer than **1 m** to the previous point are ignored | DECIDED (distance PLACEHOLDER) |
| RD-03 | The Sim samples the curve into a centre line every **1 m** of arc length. Heights come from the terrain, never stored (ROAD-02, NAV-02) | DECIDED (spacing PLACEHOLDER) |
| RD-04 | Every road has a **road type**. MVP has one: `road.dirt_path`, width **2.5 m** (2–3 people abreast), free (ROAD-12). Wider and better surfaces (gravel, cobble) come later as new types | DECIDED (width PLACEHOLDER) |
| RD-05 | **Grade check:** each 1 m sample step whose rise ÷ run is over the type's max grade (**15%**) is invalid. Invalid stretches show red in the preview, and the road cannot be confirmed (ROAD-09, NAV-03) | DECIDED (value PLACEHOLDER) |
| RD-06 | **Corridor:** each road has a corridor = centre line ± half width (NAV-06). Used for overlap checks, tree clearing and area checks | DECIDED |
| RD-07 | **Junctions:** if a new road's first or last point is within half a width of another road's centre line, it joins there (T-junction). If its centre line crosses another road's centre line, a junction is made at the crossing (X-junction). Both split the existing segment in two (ROAD-05, NAV-04). This happens whether or not snapping is on; snapping only helps the cursor land there | DECIDED |
| RD-08 | A road may not cross **itself** (refused: "crosses itself") | PROPOSED |
| RD-09 | A new road's corridor may not overlap another road's corridor except within **one road width** of a junction it makes (stops roads running on top of each other). Refused: "overlaps road" | PROPOSED |
| RD-10 | The whole centre line must be inside the map (refused: "outside the map") | DECIDED |
| RD-11 | **Trees in the corridor are removed** on confirmation: any tree whose trunk is within half width + **0.5 m** of the centre line. They yield nothing and simply disappear (same rule as BLD-10-A) | DECIDED (MVP; clearance PLACEHOLDER) |
| RD-12 | **Delete:** right-click removes one **segment** (the stretch between two nodes: road ends or junctions). A node left with no segments is removed. Trees do not come back | DECIDED |
| RD-13 | Roads may not cross building areas or plots (ROAD-08). Checked once areas exist (M4): refused with "crosses <area name>". Work areas don't block roads | DECIDED |
| RD-14 | No pre-placed (baked) trunk road. Any starting road (WORLD-08) is an ordinary road created when the game starts, and the player can delete or reroute it. Not needed in M3 | DECIDED |
| RD-15 | Commands work while the game is paused | DECIDED |

Refusal reasons are shown in the tool panel, and the first one found is the one shown. Order: outside
the map, too steep, crosses itself, overlaps road, crosses area.

## Network

- **Node:** a road end or junction, at an x/z position (mm).
- **Segment:** one stretch of road between two nodes, with its road type, its own sampled centre line,
  its length (m) and its corridor. A segment belongs to the road command that drew it (for debugging only;
  the player deletes segments, not "roads").
- Splitting a segment at a junction keeps the curve shape exactly: both halves keep the same centre-line
  samples, cut at the junction point.
- Two nodes are "connected" if a chain of segments links them. The debug overlay can show node and segment ids.

## Snapping (shared with areas, GEO-13..15)

- One toggle in the top bar, **Snapping: On/Off**, default On. Holding **Alt** inverts it while held.
- Road snap targets (in this order): road ends, points on a road's centre line, and later area vertices
  and area edges (M4). Snap distance: **12 screen pixels** (tool-side only, PLACEHOLDER).

## Out of scope for M3

Editing a confirmed road's shape, speed bonus on roads (needs movement), road upgrades, bridges,
saving, and tree-felling jobs for clearing (future: woodcutters fell and haul the timber; see `gdd-amendments.md`).
