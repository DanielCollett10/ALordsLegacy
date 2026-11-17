# Areas

**Purpose:** drawn polygons on the map. Building areas hold buildings and must not overlap each other or roads. Work areas say where a worker goes (decision 0014, 0018).

## Key types and files

Folder `Assets/Sim/Areas/` (`LordsLegacy.Sim.Areas`).

| Type | Role |
|---|---|
| `AreaShape` | Vertices plus one `AreaEdge` per vertex: Straight, Bent (quadratic handle), FollowsRoad, NearRoad (AR-24), NearArea (AR-25). |
| `AreaTypeDef`, `AreaTypes`, `AreaTypeCatalog` | Types: `area.test` (Building), `area.test_work` (Work), `area.woodcutting` (Building, 150–5,000 m²), `area.work_woodcutting` (Work), `area.plot` (Building, 60–400 m², needs road frontage; M13), `area.foraging` (Building, 100–5,000 m², clears trees) and `area.work_foraging` (Work, 100–400,000 m²; M14), and `area.market` (Building, 100–2,000 m², no frontage; one per town, M15). Limits per `docs/design/tuning.md`. |
| `HousePlacement` | Pure. Puts the 6 × 8 m house on a plot's longest frontage edge, 1 m back (HM-04). Used by `DesignatePlot` and the plot tool's ghost. See `homes.md`. |
| `AreaFlattener` | `Flatten(shape, roads, areas, spacing)`: a CCW `PointMm` outline that passes through every vertex in order (AR-30). Helpers `NearRoadGeometry`, `NearAreaGeometry`. |
| `AreaValidator` | Pure. `Validate(world, shape, typeId)` → `AreaPreview` (outline, m², `AreaRefusal`, neighbour names). |
| `AreaTrim` | Internal. Building areas: subtracts overlapped neighbours and road corridors (AR-34). |
| `AreaPlacementChecks` | Internal. Slope (AR-14), overlap with another building area (AR-15), road crossing (AR-16). |
| `AreaUntangler` | `TryUntangle(points, out order)`: reorders a crossed (hourglass) draft by angle around its centroid, if that gives a simple polygon (AR-11). |
| `AreaRegistry` (`SimWorld.Areas`) | Stores `AreaRecord`s (id, type, shape, outline, bounds, name). Building areas in a `SpatialGrid`. `FindAt`, `FindNearestSnap` → `AreaSnapHit`. `Version`. |

## How data moves

`DesignateArea(shape, typeId)` re-runs `AreaValidator`. If valid it adds the area and, for building kinds, removes the
trees inside the outline (ids in `RemovedTreeIds`). `DeleteArea` removes the area (trees do not return). An operation's
building area creates the operation (see `operations.md`).

Validation order: point count, FollowsRoad vertices within 5 cm of the road side, NearRoad vertices within 10 m of it,
edges cross or outline not simple, outside map. Building kinds then trim (AR-34), check size, then run the placement
checks. Work kinds stop after size.

Names are "<prefix> N" with per-type counters that never reuse a number.

## Extending

- Add a type in `AreaTypeCatalog` (data), not in code. Then check the Unity tool offers it.
- Snapping is presentation only (decision 0017). Do not add it to the Sim.
- A new edge kind must keep AR-30 (`FlattenInvariantTests`).

## Gotchas

- Work areas never trim and never block roads. Only building areas are in the building index.
- Trimming can split an area into pieces or lose a vertex. Those give `OverlapsArea` or `CrossesRoad` refusals.
- Edge index in `AreaSnapHit` is the shape edge, measured on the flattened outline.
- Right-click point removal has no minimum point check in the tool (`docs/design/areas.md` note).

## Tests

`Sim.Tests/Areas/` (`AreaTests`, `AreaValidatorTests`, `AreaPlacementTests`, `AreaTrimTests`, `AreaRoadTrimTests`,
`AreaUntanglerTests`, `AreaRegistryTests`, `NearAreaEdgeTests`, `NearRoadEdgeTests`, `FlattenInvariantTests`, `AreaCommandTests`,
`PlotTypeTests`, `HousePlacementTests`, `MarketAreaTests`).
`area.market` (T-456): no frontage, size from `MarketMin/MaxAreaM2`, refused with no stall slot or a 2nd market; `Sim/Markets`.
Unity: `Assets/Tests/PlayMode/AreaToolTests.cs`, `AreaRendererTests.cs`, `AreaPlacementTests.cs`.
