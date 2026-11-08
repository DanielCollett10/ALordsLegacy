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
