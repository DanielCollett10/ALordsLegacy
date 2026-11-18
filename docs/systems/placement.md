# Placement rules

**Purpose:** say whether one already-snapped point may be placed for an area or a road. Pure queries, so tools can ask
before they draw (decision 0028, `docs/design/placement.md`).

## Key types and files

`Assets/Sim/Placement/` (`LordsLegacy.Sim.Placement`):

- `PlacementRules.CanPlaceAreaVertex(world, areaTypeId, point)` and `CanPlaceRoadPoint(world, point)`.
- `PlacementResult`: `Ok`, or `Reason` (`InsideArea` with `AreaName`, `OnRoad`, `OutsideMap`) and `Text` for the UI.
- `StructurePlacement.Preview(world, typeId, centre, rotation)` (M14): for a standalone structure such as the granary, the hewing station or the sawpit. Checks the
  whole plot (building plus yard) is clear of footprints, areas, roads and pile circles. Used by `PlaceStructure` and the structure tool.
- `FixedPlots` (M15, T-471): the plot outline of each fixed structure (`Footprint.PlotPolygon`). `FindOverlapping`,
  `FindStrictlyInside` and `FindNearestSnapIncludingAreas` (areas and plots together, PL-13). Named "Granary 4" and so on.
- `StructureSnap.Snap(world, typeId, centre, rotation, snapDistanceM)` (T-473/481): moves and turns a structure ghost flush to a
  nearby plot edge or road side. Returns the input unchanged when nothing is in reach.

## Rules

- "Inside" means strictly inside. A point within `BoundaryToleranceMm` (50 mm) of an area edge or road side line is on the
  boundary and is Ok.
- Ground piles block building (PL-15, T-472): `PileZones.Of(world)` gives a `SimTuning.PileClearanceM` (2 m) circle for every
  ground pile with items, any owner, derived from the ledger (no saved state; the circle goes when the pile empties).
  Building-area vertices (`ItemsOnGround`), building areas, plots and markets (`AreaRefusal.ItemsOnGround`), modules
  (`ModuleRefusal.ItemsOnGround`, also draft preview) and structure plots (`StructureRefusal.ItemsOnGround`) are refused:
  "Items on the ground here". Roads and work areas are not.
- Fixed plots (PL-11) are fitted around like areas: `AreaTrim` clips a drawn area to the piece outside the plot. Only a trim
  that splits the area or loses a vertex is refused (`OverlapsArea`, naming the plot). See `design/placement.md` note.
- Work-area vertices are Ok anywhere on the map.
- Building-area vertices are refused inside a road corridor (`OnRoad`).
- Road points are refused only inside a building area.
- Shape checks (AR-15, AR-16, RD-13) still run on the finished shape in the validators. Placement does not replace them.

## Extending

Add a reason to `PlacementResult` with its text, and keep the rule pure. Tests go in `Sim.Tests/PlacementRulesTests.cs`.

## Tests

`Sim.Tests/PlacementRulesTests.cs`, `Sim.Tests/Placement/` (`PileZoneTests`, `FixedPlotTests`, `StructureSnapTests`, `GranaryPlacementTests`).
