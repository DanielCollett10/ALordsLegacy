# Placement: where a point may go (M6)

Owner rule (M5 play, screenshot 5): **don't let the player put a point where the thing can't go**, rather than
accepting the point and refusing the finished shape later. This applies to every point the player places or drags
(road points, road node drags, area vertices) and to future placed things (buildings, modules).

| ID | Rule | Tag |
| --- | --- | --- |
| PL-01 | **Building-area vertex** (test area, later plots and building areas): may not lie **inside** another building area or **inside** a road corridor. Lying **on** the boundary is allowed (area edge or corner, road side line, within 5 cm), because that's how plots touch and line up with roads. It can lie inside a work area | DECIDED |
| PL-02 | **Work-area vertex**: may go anywhere on the map, over building areas, roads and other work areas (work areas group trees and, later, a building's work zone; WORK-03, WORK-05) | DECIDED |
| PL-03 | **Road point** (new point or a dragged node): may not lie inside a building area (boundary allowed). It may lie on a road (that's how branches and junctions are made) or inside a work area | DECIDED |
| PL-04 | Outside the map is never placeable | DECIDED |
| PL-05 | **The check comes after snapping:** the snapped position is what's tested. With snapping off, the raw cursor point is tested | DECIDED |
| PL-06 | **Not placeable means:** the click does nothing, and the **cursor marker (ring or sphere) is hidden**, so no marker means you can't place there. While dragging a point or node, it stays at its **last placeable position** and only follows the cursor again once the cursor is back on placeable ground | DECIDED |
| PL-07 | The Sim answers these questions (`PlacementRules`, pure, no state change), and the tools only ask. The shape checks (AR-15, AR-16, RD-13) still run on the finished shape, because edges can cross things even when every point is fine | DECIDED |

## Plots fit together (owner 16/11/2026, M15)

A **plot** is any building-type outline: drawn areas (woodcutting, forager, house plot, market) and the fixed plots of
standalone structures (the granary's building plus 5 m yard, FD-30b).

| ID | Rule | Tag |
| --- | --- | --- |
| PL-10 | A fixed structure's plot is a **real plot outline** in the Sim, like a drawn area, so every overlap check and snap that applies to areas applies to it too | DECIDED |
| PL-11 | **No plot may overlap another plot.** Touching (a shared edge) is allowed (AR-15). Bug today: a house plot can be drawn over the granary's yard; it must be refused with *Overlaps Granary <id>* | DECIDED (owner 16/11/2026: bug) |
| PL-15 | **Ground piles block building.** A ground pile that holds items (any owner: unowned piles, operation log piles, dropped food) covers a **circle of 2 m radius** (PLACEHOLDER) around its point. No building area, plot, module or fixed structure may overlap that circle: *Items on the ground here*. Roads and work areas may. The circle goes when the pile is empty. It is derived from the ledger, so no new save state | DECIDED (owner 16/11/2026; radius and scope PROPOSED) |
| PL-12 | While any placing tool is active (area, plot, market, granary or a module ghost), **every existing plot's outline is shown**, with its corners marked as snap points | DECIDED |
| PL-13 | Area and plot vertices snap to fixed-plot corners and edges, the same as to other areas (snap order in `areas.md`), so a house plot can be butted against the granary yard | DECIDED |
| PL-14 | A fixed structure's ghost **snaps flush** to a nearby plot edge or road side within the snap distance (12 px): it turns to lie parallel to that edge and slides so its plot edge touches it. Q/E rotation still works when it isn't snapped. Snapping off: no snap (shared toggle) | PROPOSED |

> Implementation note (PL-06, dragging a point): the cursor marker is hidden whenever the cursor is over unplaceable
> ground, including during a drag of a placed point (`AreaTool.PointerMove` and `RoadTool.PointerMove` both call
> `UpdateCursor`, which hides it). The dragged point does stay at its last placeable position. Node drags do keep the
> road ring on the last placeable target (`RoadTool.MoveDraggedNode`), as the rule says. So a dragged point hides its
> marker, while a dragged node keeps one. Owner to decide which behaviour is wanted for points.
