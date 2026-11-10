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
| MOV-09 | The road multiplier must not make road walkers look wrong next to people walking on dirt at 1×: the animation plays faster so feet don't slide, and the value stays in brisk-walk range. Value: see open question | OPEN (1.5 in data today) |

## Getting there

| ID | Rule | Tag |
| --- | --- | --- |
| MOV-10 | A route is the fastest path (travel time, not distance), so people go out of their way to use a road when it pays | DECIDED |
| MOV-11 | Routes are smoothed: no zig-zag along grid cells. A straight run is used wherever it crosses only walkable cells of the same speed | PROPOSED |
| MOV-12 | A start or target on blocked ground uses the nearest walkable point. A footprint placed on someone pushes them to the nearest walkable point | PROPOSED |
| MOV-13 | If a target can't be reached, the move fails with reason *No route*; the task (M8) is cancelled and its reservations released (TSK-13) | DECIDED |
| MOV-14 | When roads or blocking footprints change, anyone walking re-plans from where they stand, the next tick | DECIDED |
| MOV-15 | Walking advances every tick by speed × the multiplier of the ground underneath. On screen, positions are smoothed between ticks (CLK-02) | DECIDED |
| MOV-16 | A led ox and its handler walk together at ox speed (MVP-1) | DECIDED |

## Open: making distance hurt a bit less (PPL-03)

On-screen speed at 1× = walk speed (m per game minute) × game minutes per real second. So "looks like real
walking at 1×" and "covers more ground per game hour" pull against each other. The levers:

| Lever | 300 m costs | Side effects |
| --- | --- | --- |
| Today (15 real min day, 5 km/h) | 5.7 game h each way | none |
| A. Longer day at 1×: 30 real min (`RealMinutesPerGameDayAt1x`) | 2.9 h | year takes 182 h at 1×, 5.7 h at 32× (TIME-A1 still met at mixed speeds) |
| B. Brisk pace: 6 km/h | 4.8 h | still reads as walking |
| C. Commute outside daylight: people leave home so they arrive at sunrise and work until sunset | daylight not spent walking | shorter nights; needs a sleep minimum |
| A + B | 2.4 h | |
