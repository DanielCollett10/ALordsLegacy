# Placement rules

**Purpose:** say whether one already-snapped point may be placed for an area or a road. Pure queries, so tools can ask
before they draw (decision 0028, `docs/design/placement.md`).

## Key types and files

`Assets/Sim/Placement/` (`LordsLegacy.Sim.Placement`):

- `PlacementRules.CanPlaceAreaVertex(world, areaTypeId, point)` and `CanPlaceRoadPoint(world, point)`.
- `PlacementResult`: `Ok`, or `Reason` (`InsideArea` with `AreaName`, `OnRoad`, `OutsideMap`) and `Text` for the UI.

## Rules

- "Inside" means strictly inside. A point within `BoundaryToleranceMm` (50 mm) of an area edge or road side line is on the
  boundary and is Ok.
- Work-area vertices are Ok anywhere on the map.
- Building-area vertices are refused inside a road corridor (`OnRoad`).
- Road points are refused only inside a building area.
- Shape checks (AR-15, AR-16, RD-13) still run on the finished shape in the validators. Placement does not replace them.

## Extending

Add a reason to `PlacementResult` with its text, and keep the rule pure. Tests go in `Sim.Tests/PlacementRulesTests.cs`.

## Tests

`Sim.Tests/PlacementRulesTests.cs`.
