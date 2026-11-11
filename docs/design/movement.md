# Movement: walking over the land (live)

How citizens and oxen get from A to B, from M7 onward. Wins over tech spec NAV-07..12 where they disagree.
Technical approach: decision 0022. Speeds are in `tuning.md`.

Tags: DECIDED = owner agreed. PROPOSED = lead's default. OPEN = not decided.

## What blocks and what slows

| ID | Rule | Tag |
| --- | --- | --- |
| MOV-01 | **Slope never changes walking speed.** (No slope maths during movement.) | DECIDED |
| MOV-02 | Ground steeper than **30°** can't be walked (`MaxWalkSlopeDeg`). Routes go around it | DECIDED (value PLACEHOLDER) |
| MOV-03 | **Only footprints block movement** (a footprint type with `BlocksMovement`, e.g. the hut). Building areas, work areas and the camp footprint do not block | DECIDED |
| MOV-04 | Each blocking footprint has an **access point** just outside one edge (its door). Anyone using it walks to the access point | PROPOSED |
| MOV-05 | Future: building areas get a fence along their edge with gaps to walk through; fences will block, gaps won't. Not in MVP-1 | DECIDED (future) |
| MOV-06 | **Trees do not block.** People and oxen walk between them (NAV-11 style; overlaps are visual only) | PROPOSED |
| MOV-07 | People and oxen pass through each other (NAV-11) | DECIDED |
| MOV-08 | On a road, speed is multiplied by the road type's speed multiplier (ROAD-10). Being "on a road" means standing inside its corridor (`CorridorPolygon`) | DECIDED |
| MOV-09 | Road multiplier **1.25×** (dirt path; replaces ROAD-10's 1.5). The walk animation plays faster by the same factor, so feet don't slide and a road walker looks brisk, not jogging. Later road types keep their multipliers in brisk-walk range | DECIDED (value PLACEHOLDER) |

## Getting there

| ID | Rule | Tag |
| --- | --- | --- |
| MOV-10 | A route is the fastest path (travel time, not distance), so people go out of their way to use a road when it pays | DECIDED |
| MOV-11 | Routes are smoothed: no zig-zag along grid cells. A straight run is used wherever it crosses only walkable cells of the same speed | PROPOSED |
| MOV-12 | A start or target on blocked ground uses the nearest walkable point. A footprint placed on someone pushes them to the nearest walkable point | DECIDED |
| MOV-13 | If a target can't be reached, the move fails with reason *No route*; the task (M8) is cancelled and its reservations released (TSK-13) | DECIDED |
| MOV-14 | When roads or blocking footprints change, anyone walking re-plans from where they stand, the next tick | DECIDED |
| MOV-15 | Walking advances every tick by speed × the multiplier of the ground underneath. On screen, positions are smoothed between ticks (CLK-02) | DECIDED |
| MOV-16 | A led ox and its handler walk together at ox speed (MVP-1) | DECIDED |

## Distance and pace (DECIDED, owner 10/11/2026: levers A + C)

On-screen speed at 1× = walk speed (m per game minute) × game minutes per real second. Both have to move
together: **walk speed in m per game minute = on-screen km/h × 1000 ÷ 60 × (real seconds per game minute)**.

| ID | Rule | Tag |
| --- | --- | --- |
| MOV-20 | A game day lasts **30 real minutes at 1×** (TIME-A2 changed from 15) | DECIDED (value PLACEHOLDER) |
| MOV-21 | On-screen pace stays the same: walking 5 km/h → **1.74 m per game minute**; ox 3 km/h → **1.04 m per game minute**. If the day length changes, both speeds change with it | DECIDED |
| MOV-22 | Commutes happen outside daylight (people.md PPL-25, PPL-26) | DECIDED |
| MOV-23 | Rejected: a brisker base pace (lever B). People always walk at 5 km/h on screen | DECIDED |

300 m off-road now takes about 2.9 game hours (about 2.3 h on a road at 1.25×).
