# Roads

**Purpose:** freeform roads as a network of nodes and segments. Roads speed walkers up, and areas must avoid them (decision 0013).

## Key types and files

Folder `Assets/Sim/Roads/` (`LordsLegacy.Sim.Roads`).

| Type | Role |
|---|---|
| `RoadNetwork` (`SimWorld.Roads`) | Nodes and segments, `Version`, `AddSegment`, `SplitSegmentAt`, `RemoveSegment`, `MoveNode`, `FindNearest` → `RoadHit`, `AreConnected`, `ComponentCount`, `ReachableSegmentCount`. |
| `RoadNode` | Position and attached segment ids. |
| `RoadSegment` | Immutable: id, node A/B, type id, curve kind, control points, samples, `LengthM`, `StartTangent`/`EndTangent`. `SideLine(side, catalog)`, `CorridorPolygon(catalog)`. |
| `RoadTypeDef`, `RoadTypes`, `RoadTypeCatalog` | Type data. `road.dirt_path`: 2.5 m wide, 15% max grade, 1.25× speed. |
| `ICurveSampler`, `CatmullRomSampler` | Centripetal Catmull-Rom, about 1 m apart, hits every control point. `Sample(points, start?, end?)` takes tangents (RD-16). |
| `ControlPointFilter` | `Clean` (RD-02). |
| `RoadValidator` | Pure. `Validate` for a draft, `PreviewMove` / `ValidateMove` for a node drag. Returns `RoadPreview`. |
| `RoadCutter` | Internal. Cuts a new road's samples and control points at its junctions. |
| `NodeMovePlanner` | Internal. Pins a two-segment node to one shared direction (within 1°). |

## How data moves

`DrawRoad` runs `RoadValidator`. If valid it splits existing segments at T/X junctions (the originals are removed, two new
ids issued), cuts the new road at each junction, adds one segment per piece, and removes trees within half the width
plus `RoadTreeClearanceM` of each centre line. `MoveRoadNode` replaces the attached segments with new ids (node ids kept).
Both return created and removed ids.

Validation order (first failure wins): outside map, too steep, crosses itself, overlaps road, crosses a building area
(`RoadRefusal.CrossesArea`, "crosses <name>"; last, and only for building areas). Junction kinds in the preview: `T`, `X`,
`JoinNode`.

Navigation reads `RoadSegment` corridors through `NavGrid` (speed multiplier). Nothing in the Sim draws roads.

## Extending

- A new road type is a `RoadTypeDef` in the catalog. Read it through `world.RoadTypes`, never the static list.
- A new validation rule goes at the end of `RoadValidator`, so earlier refusals keep their meaning.
- New segment state must be added to the land save section (`docs/systems/save.md`).

## Gotchas

- Segments are immutable. Changing a road means remove and add, which issues new ids.
- `RoadSegment` does not store a corridor; it is computed from the centre line (decision 0013).
- Tangents on new end segments are stored (decision 0029). T, X and 2+ segment nodes have none.
- Samples are spaced by `round(span length ÷ 1 m)` equal parts per control-point span (RD-03 note in `docs/design/roads.md`).

## Tests

`Sim.Tests/Roads/` (`DrawRoadTests`, `DeleteRoadSegmentTests`, `MoveRoadNodeTests`, `RoadExtensionTests`,
`RoadNetworkTests`, `RoadTreeClearanceTests`, `RoadValidatorTests`, `RoadsTests`), plus `Sim.Tests/Save/LandSectionTests.cs`.
