# Navigation and movement

**Purpose:** a walkable grid over the terrain, A* pathfinding over it, and the system that walks citizens and oxen along
move orders (decision 0022, `docs/design/movement.md`).

## Key types and files

- `NavGrid` (`Assets/Sim/Navigation/`, `SimWorld.Nav`, never null): cells of `NavCellSizeM` (2 m) over the terrain bounds.
  Per cell: `IsBlocked` (slope over `MaxWalkSlopeDeg`, or centre inside a movement-blocking footprint) and
  `SpeedMultiplier` (highest road multiplier, else 1). `Version` bumps when any cell changes. No map = no cells.
- `NavSyncSystem`: first in the sync phase, and also runs on `OnCommandApplied`. The first `Sync` is a full build; later
  ones diff road segment and footprint ids and recompute only the cells under changed items.
- `GridPathfinder` (`IPathfinder` for injection): `Find(nav, from, to)`. 8-neighbour A*, no corner cutting, step cost =
  length ÷ mean of the two cells' multipliers. Blocked ends snap to the nearest walkable centre within 20 m. Output
  `PathResult` (`Ok`, `Points` in metres, `TravelMinutes` at 1 m/min), smoothed over straight lines that cross only
  equal-multiplier cells.
- `MovementSystem` (movement phase): advances each agent's `MoveOrder` by walk or ox speed × the multiplier of the cell
  under it. Also `Attach(follower, leader, offsetM)` and `Detach` for oxen led by a citizen (decision 0036).
- `MoveOrder` (`People/`): target, `Path`, `SegmentIndex`, `ProgressM`, `Status` (Pending, Moving, Done, Failed), `FailReason`.
- `IMovable` (`People/`): implemented by `Citizen` and `Ox`. Position, `FacingRad`, `PreviousX/Z`, `CurrentMultiplier`, `Move`, `Follow`.

## How data moves

The owner sets `Move` (a routine, a task step or debug send). `MovementSystem` steps it each tick. When `Nav.Version`
changes, agents on blocked cells move to the nearest walkable point, and unfinished orders re-plan from where the agent
stands. A finished order stays on the agent until its owner clears it. Movement never touches `Activity`.

Followers are placed behind the leader after all agents have moved. The offset shrinks to 75/50/25% and then onto the
leader if the cell is blocked or off the map.

## Extending

- A new walker or mover implements `IMovable` and is added to `MovementSystem`.
- Cost or speed changes go in `SimTuning`, or in `RoadTypeDef` for roads.
- Do not query the grid for rules outside movement. Use `NavGrid` cells.

## Gotchas

- `TravelMinutes` is at 1 m/min, so it is a walking-time estimate, not the walk speed.
- The nav grid cells are not saved. Only `Nav.Version` is, and `MovementSystem.SeenNavVersion` keeps orders from
  re-planning on the first step after a load (`save.md`).
- Blocking modules push agents aside through nav sync and `MovementSystem`, not through a special rule.

## Tests

`Sim.Tests/Navigation/` (`NavGridTests`, `GridPathfinderTests`, `MovementSystemTests`, `FollowMovementTests`),
`Sim.Tests/Save/MovementSectionTests.cs`.
