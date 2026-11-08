# Architecture

_Living document. Agents update it when they change how parts fit together. Each task edits only
the section it belongs to; the section headings below are fixed and keep this order._

## Layers

| Layer | Folder | Namespace / assembly | Depends on | Tested by |
|---|---|---|---|---|
| Simulation core | `Assets/Sim/` | `LordsLegacy.Sim` (asmdef `LordsLegacy.Sim`, `noEngineReferences`) | nothing | `Sim.Tests/` via `dotnet test` |
| Unity layer | `Assets/Game/` | `LordsLegacy.Game` (asmdef `LordsLegacy.Game`) | `LordsLegacy.Sim`, UnityEngine, Unity.InputSystem | Unity EditMode/PlayMode tests |
| Editor tooling | `Assets/Editor/` | asmdef `LordsLegacy.Editor`, namespace `LordsLegacy.EditorTools` | `LordsLegacy.Sim`, `LordsLegacy.Game`, UnityEditor | by hand / editor lane |
| Unity tests | `Assets/Tests/` | asmdefs `LordsLegacy.Tests.EditMode`, `LordsLegacy.Tests.PlayMode` | Unity layer, Sim | Unity Test Runner (editor lane, decision 0007) |

## Simulation core

_Owner: the task that changes the Sim core. Other tasks do not edit this section._

- `Assets/Sim/Core/SimWorld.cs` (`LordsLegacy.Sim.Core`) is the root and owns all game state: the tick clock, pause,
  `Terrain` (`IHeightField`), `Trees` (`TreeRegistry`), `Roads` (`RoadNetwork`), `Areas` (`AreaRegistry`) and `CommandLog`. All are
  never null. Build it with `new SimWorld(tuning)` (flat ground, no trees) or `SimWorld.FromMapData(map, tuning)`.
  Both take optional `RoadTypeCatalog` / `AreaTypeCatalog` arguments (decision 0019); the world exposes them as
  `RoadTypes` and `AreaTypes` (never null; null arguments give the static default definitions in `RoadTypes.All` /
  `AreaTypes.All`). All Sim code looks types up through the world (`world.RoadTypes.Find`, `RoadSegment.SideLine(side,
  catalog)`, `CorridorPolygon(catalog)`, `AreaFlattener.Flatten(..., roadTypes)`); the static `Find` methods and the
  catalog-less `SideLine`/`CorridorPolygon` overloads (`[Obsolete]`) remain only for the Unity layer until it migrates.
  **One tick = one game minute.** `Step(n)` advances `Tick` by n and does nothing while `Paused`; `Pause()` and
  `Resume()` set it. `Now` is the `GameTime` for the current tick. The Sim has no notion of real seconds.
- `Assets/Sim/Time/` (namespace `LordsLegacy.Sim.Calendar`, to avoid clashing with `UnityEngine.Time`):
  - `GameCalendar`: tick → `GameTime` (year, date, weekday, time, season). 365-day year, no leap years,
    the year number increments on 1 January.
  - `Daylight`: seasonal sunrise and sunset from min/max daylight hours. Pure, no state.
  - `TickPacer`: owns the selected speed (`SetSpeed`, `CurrentSpeed`, `Speeds`) and turns real seconds into
    whole ticks (`Advance`), carrying the remainder (`Fraction`). Over `MaxTicksPerFrame` it drops the excess
    and sets `SlowedDown`. `Reset()` clears the carry (the host calls it while paused). No pause state (decision 0006).
- `Assets/Sim/Tuning/SimTuning.cs` (`LordsLegacy.Sim.Tuning`) holds the balancing numbers. Read by code today: pace,
  speeds, start date, daylight, standard log length, road sample spacing, road minimum point spacing, road tree
  clearance, and the area values (curve flattening, slope grid, overlap tolerance, spatial index cell). Defined but
  read by nothing yet: walk and ox speed, trunk length range, starting oxen, rest-day switch (see `tuning.md`). Food,
  market, construction and population numbers are not in code yet. The Sim never hard-codes these.
- `Assets/Sim/Forestry/` (`LordsLegacy.Sim.Forestry`): `TreeRegistry` holds all trees keyed by `TreeId`, with a
  uniform 16 m spatial grid (`QueryRect`, `QueryRadius`, `LogYield`). `TreeRecord` holds id, x/z, usable trunk
  length and `State` (only `Standing` exists yet). Trees come from the baked map. `Remove(TreeId)` takes a tree out of
  the records and grid (false if unknown); a removed id can never be added again (BLD-10-D). `Version` increases on
  every Add or Remove, so views rebuild only when it changes. `Count`, `All` and the queries show standing trees only.
  `TreeClearing` (static) removes the trees under a polygon (`RemoveInPolygon`) or within a distance of a polyline
  (`RemoveNearPolyline`), using geo-1 on trunk positions rounded to mm; both return the removed ids sorted.
- `Assets/Sim/Areas/` (`LordsLegacy.Sim.Areas`, decision 0014): `AreaTypeDef`/`AreaTypes` (`area.test` Building,
  `area.test_work` Work; limits per `tuning.md`), `AreaShape` (vertices plus one `AreaEdge` per vertex: Straight,
  Bent with an on-curve handle, FollowsRoad and NearRoad (AR-24), both with segment id and `RoadSide`) and
  `AreaFlattener.Flatten(shape, roads)`,
  which gives a CCW `PointMm` polygon: bent edges are quadratics sampled every `CurveFlattenSpacingM`, road edges copy
  the road side line between the vertices' nearest positions, a missing segment falls back to Straight. NearRoad
  does the same but pushes each side sample outward (away from the road) by a distance interpolated by arc length
  from vertex A's perpendicular distance to vertex B's, so both vertices stay put (`FollowsRoad` = both distances 0;
  helpers in internal `NearRoadGeometry`). `AreaTypeDef.NearRoadReachM` (10) is the vertex reach limit. `NearArea` (AR-25, area id; internal `NearAreaGeometry`) does
  the same along another area's flattened outline (shorter way round); `AreaFlattener.Flatten(shape, roads, areas, spacingM)` takes the registry.
  `FollowsRoadPartly` (AR-26, segment + side): one vertex on the side line, the other anywhere; the flattener copies side-line
  vertices from the snapped end while they stay within `AreaFlattener.PartialFollowReachM` (10 m) of the chord, then goes straight.
  `AreaTool.ChooseNearRoad` uses it as the fallback when one end is snapped to a road side and NearRoad/NearArea don't apply.
  `AreaRegistry` (`SimWorld.Areas`, never null) stores `AreaRecord`s (id, type id, name, shape, flattened polygon,
  bounds); internal `Add`/`Remove` bump `Version`; names are "<prefix> N" with per-type counters that never reuse.
  Building-kind areas sit in a `SpatialGrid` (`SpatialIndexCellM`): `QueryOverlapCandidates(bounds)`. `FindAt(point,
  kind)` is point-in-polygon on the flattened outline; `FindNearestSnap(point, maxMm)` gives an `AreaSnapHit`
  (Vertex/Edge, vertices preferred; edge index is the shape edge, measured on the flattened outline).
  `RoadSegment.SideLine(side)` (half-width offset of the samples; Left is left of NodeA→NodeB) and `CorridorPolygon()`
  (CCW) support this. `AreaValidator.Validate(world, shape, typeId)` is pure and returns an `AreaPreview` (flattened polygon, m²,
  point count, `AreaRefusal`, overlapped area name). Checks in order: point count, FollowsRoad vertices within 5 cm of
  the road side, or NearRoad vertices within `NearRoadReachM` of the side and not on the road side of it
  ("not on road"; a missing segment also fails), edges cross (repeated shape points or non-simple
  outline), outside the map (any flattened point), size. Building kinds then run `AreaPlacementChecks` (internal):
  slope (AR-14), overlap with another building area (AR-15), road corridor crossing (AR-16). Work kinds stop after size.
  `DesignateArea` and `DeleteArea` are in `Commands/` (see below).
- `Assets/Sim/Geometry/` (`LordsLegacy.Sim.Geometry`, decision 0012): integer-mm `PointMm`, `Segments` (exact
  classify/intersect), `Polygons` (area, winding, `IsSimple`, `Locate`, bounds), `Polylines` (length, nearest,
  min distance, `PointsEvery(line, spacingM)` points at arc length 0, s, 2s… rounded to mm, empty for zero-length
  lines, throws for spacing ≤ 0; `PointsEveryWithin(line, spacingM, centre, radiusM)` the inclusive subset; snap dots, AR-22), `Triangulator.EarClip` (n−2 integer-exact triangles; collinear vertices become zero-area triangles).
  Predicates are exact; distances are double mm.
  `SpatialGrid<T>` (decision 0012, GEO-16) is a uniform-grid index over `BoundsMm` with a cell size in metres
  (rounded to mm, `SimTuning.SpatialIndexCellM` is the value to pass). `Insert(item, bounds)`, `Remove(item)`
  (false if unknown), `QueryRect(bounds)` and `QueryPoint(point)` return each item once, in ascending insertion
  order; edges are inclusive. Items are stored in every cell their bounds touch. Used only by `AreaRegistry` (building-area bounds).
  `PolygonOverlap.Overlaps` is the tolerance overlap test (decision 0012).
- `Assets/Sim/Roads/` (`LordsLegacy.Sim.Roads`, decision 0013):
  - Types: `RoadTypes` (code-built list; `road.dirt_path`: 2.5 m, 15% max grade) of `RoadTypeDef`. The speed
    multiplier is stored but nothing reads it.
  - Sampling: `ICurveSampler` with one implementation, `CatmullRomSampler` (centripetal; samples each span evenly,
    about 1 m apart, and hits every control point). `ControlPointFilter.Clean` applies RD-02. `CurveKind` has one value.
  - `RoadNetwork` (`SimWorld.Roads`) holds `RoadNode`s (position, attached segment ids) and `RoadSegment`s (immutable:
    id, node A/B, type id, curve kind, control points, samples, `LengthM`). Ids are never reused and iteration is in id
    order. `AddSegment` reuses a node at the exact end position. `SplitSegmentAt` removes the original id and issues
    two new ids (no-op at an end). `RemoveSegment` drops emptied nodes. `Version` rises on every change.
    Queries: `FindNearest` → `RoadHit` (a node in range beats a line point), `AreConnected`, `ComponentCount`,
    `ReachableSegmentCount`.
  - `RoadValidator.Validate(world, controlPoints, typeId, ignoreSegmentId = -1)` is pure. It samples the curve and
    returns a `RoadPreview` (samples, `InvalidRanges`, `PlannedJunction`s in order along the road, length, max grade,
    `RoadRefusal`). Refusal order: outside map, too steep, crosses itself, overlaps road, crosses area (last; building areas only). Junction kinds: `T`, `X`,
    `JoinNode`. The tool preview and `DrawRoad` both call it.
    RD-16 tangent continuity: `ICurveSampler.Sample(points, RoadTangent? start = null, RoadTangent? end = null)`; with
    none the output is the plain Catmull-Rom. When the first/last point is a `JoinNode` on a node with exactly one
    (non-loop, non-ignored) segment, `RoadValidator` re-samples with that segment's outward end direction (Hermite span
    at the pinned end) and reports it as `RoadPreview.StartTangent/EndTangent`. `DrawRoad` stores them on the new
    first/last `RoadSegment` (`StartTangent`/`EndTangent`); the old segment is untouched. T, X and 2+-segment nodes get none.
  - `RoadCutter` (internal) cuts a new road's samples and control points at its junctions.
- `Assets/Sim/Commands/` (`LordsLegacy.Sim.Commands`, decision 0011): `ISimCommand` (`Validate` must not mutate;
  `Apply` runs only after an accepted `Validate`), `CommandResult` (immutable: Accepted with created, removed and
  `RemovedTreeIds` (decision 0015), or Rejected with `CommandRejectCode` and text), `CommandLog` (append-only
  `(tick, command)` entries). Road commands:
  - `DrawRoad(controlPoints, typeId, curve)`: re-runs `RoadValidator`. If valid, splits existing segments at their T/X
    junctions, cuts the new road at every junction, adds one segment per piece, then removes standing trees within
    half the road width + `RoadTreeClearanceM` of each new centre line (RD-11). `Created` = new and split-half segment
    ids, `Removed` = split originals. Rejections: unknown type or too few points → InvalidArgument, overlap → Conflict,
    other → NotAllowed.
  - `DeleteRoadSegment(segmentId)`: removes one segment; unknown id → `NotFound`. Trees are not touched.
  - `MoveRoadNode(nodeId, position)` (RD-17): Preview with `RoadValidator.PreviewMove(world, nodeId, position)` →
    `NodeMovePreview` (one `SegmentMovePreview` per attached segment: new control points plus a `RoadPreview`; no mutation).
    Each segment keeps its other control points (RD-02 cleans the neighbour); at a node with exactly 2 non-loop segments
    both are pinned to one shared direction (`NodeMovePlanner`, tangents refined so the first sample steps agree within
    1°). Each is checked by `RoadValidator.ValidateMove` (attached segments ignored; any junction except a join at the
    segment's own end nodes → `RoadRefusal.WouldJoinRoad`, "would join a road", checked last). First failure rejects the
    whole move. On success `RoadNetwork.MoveNode` replaces the attached segments with new ids (node ids kept),
    `Removed` = old, `Created` = new, and trees near the new centre lines go (`RemovedTreeIds`). Unknown node → `NotFound`.
  - `SimWorld.Execute(cmd)` runs validate-then-apply immediately, works while paused, never changes `Tick`, and appends
    every call (accepted or rejected) to `CommandLog`. A rejected command changes nothing. Unity calls it only through `SimHost`.
- Pure C#, no `UnityEngine`. Tests live in `Sim.Tests/`, which compiles the same `Assets/Sim/**/*.cs`.
- Area commands (`Assets/Sim/Commands/`): `DesignateArea(shape, typeId)` re-runs `AreaValidator`; if valid it adds the
  area through `AreaRegistry` and, for types with `ClearsTrees` (building kinds), removes the trees inside the
  flattened outline (ids in `CommandResult.RemovedTreeIds`; the new area id is in `Created`). `DeleteArea(areaId)`
  removes it (unknown id → `NotFound`); trees do not return. Both work while paused. `RoadValidator` has a last check
  after "overlaps road": a corridor overlapping a building area by more than `OverlapToleranceM2` is refused with
  `RoadRefusal.CrossesArea` ("crosses <name>", `RoadPreview.CrossedAreaName`; `DrawRoad` rejects as `Conflict`).
  Work areas are not in the building index so never block roads. With no areas the road checks are unchanged.
- Not built yet: tasks, pathing, buildings, citizens, economy, saving. Decision 0005 (task types as data)
  describes future work with no code.

## Map pipeline

_Owner: the map and terrain tasks. Other tasks do not edit this section._

- Terrain and trees are baked into Sim map data (decision 0003). The Sim never asks Unity for
  terrain; an editor bake step writes a versioned map file that the Sim reads.
- `Assets/Sim/Terrain/`: `IHeightField` (HeightAt, SlopeDegAt, Contains, Bounds; out-of-bounds queries clamp)
  is the only terrain API Sim code uses. `HeightGrid` (metres, bilinear, central-difference slope; ~36 MB for
  6 km at 2 m) and `FlatHeightField` (no map) implement it. `Bounds2` is the rectangle type.
- `Assets/Sim/Map/` (`LordsLegacy.Sim.Map`): `MapData` (format version, map id, `HeightGrid`, `MapTree` list),
  `MapDataIO` (binary read/write via `System.IO`, from `byte[]` or `Stream`; unknown version, bad magic or
  truncation throw `MapDataFormatException`) and `SyntheticMap.CreateRamp()` (200x200 m test map, 5 trees, used by tests).
- `SimWorld.FromMapData(map, tuning)` uses the map's grid and trees. The map has no baked roads (RD-14, WORLD-08-A).
- File format: little-endian binary, magic `LLMP`, int32 format version (`MapData.CurrentVersion` = 1), map id,
  height grid, then trees. Other versions are rejected.
- Tree authoring: menu `Lords Legacy/Map/Scatter Forest Trees` (`ForestScatter`, Assets/Editor/MapGen/Forest) reads
  `Assets/Maps/TestMap/forest-region.json` and writes the seeded `TreeAuthoringData` asset
  `Assets/Maps/TestMap/TestMapTrees.asset` (`LordsLegacy.Game.Authoring`; ids 0..n-1, trunk 12-20 m).
  Unity terrain trees are not used.
- Terrain authoring: menu `Lords Legacy/Map/Generate Test Terrain` (`TestTerrainGenerator`) writes
  `Assets/Maps/TestMap/TestMapTerrain.asset`, the scene `TestMap.unity` and `forest-region.json`. Same seed, same result.
- Coordinates: Sim x/z equal Unity world x/z in metres; Sim height equals Unity world y. The terrain sits at
  world (0,0,0); the HeightGrid origin is (0,0).
- Size/resolution rule: terrain size ÷ (heightmapResolution − 1) must equal the bake cell size (2 m), so Unity
  height samples coincide with Sim grid nodes. Test map: 2,048 × 2,048 m, 1025 × 1025 heightmap.
- Flow: authoring assets → bake (menu `Lords Legacy/Map/Bake Test Map`, `MapBaker`, Assets/Editor/MapGen/Bake;
  needs TestMap.unity open; fails with a clear error if the terrain is moved or the rule is broken) →
  `Assets/StreamingAssets/Maps/testmap.lsmap` (committed plain binary, `*.lsmap binary`, no LFS) → runtime reads
  the bytes from `Application.streamingAssetsPath` and passes them to `MapDataIO.Read`. Tree ids in the file
  equal the TreeAuthoringData ids.
- **Re-baking after any terrain or tree change is a manual menu step**; commit the new `.lsmap`.
  Runtime tests use the committed file and never run the bake.

## Unity layer

_Owner: the Unity-layer tasks. Other tasks do not edit this section._

- `Assets/Game/` reads Sim state and draws it; it turns player input into Sim commands and holds
  no game rules (see `CLAUDE.md`).
- `SimHost` (`Assets/Game/Bootstrap/`, MonoBehaviour in the Game scene) owns `SimWorld` and `TickPacer` (decision 0006).
  `Initialize(SimTuning, MapData)`; if not initialised, `Start` reads `StreamingAssets/Maps/testmap.lsmap` (`TryLoadMap`
  logs an error naming the path and the bake menu item if missing). `Update` calls `Tick(Time.deltaTime)` when
  `AutoUpdate`. `Tick` runs `World.Step(Pacer.Advance(..))`, or resets the pacer while paused. Views read
  `World`, `Pacer`, `TickFraction` and `Stats`. Views send commands only through `Execute(ISimCommand)`,
  `TogglePause` and `TickPacer.SetSpeed`.
- `SimStats` (`LordsLegacy.Game`) is the only Unity-side measurement: `LastStepMs` and `TicksPerSecond`
  (ticks in the last real second). Sim code never reads it.
- Terrain: Unity Terrain built from `TestMapTerrain.asset` is drawn by Unity and is the collider for picking.
  It is not built from Sim data.
- Trees (`Assets/Game/Visuals/TreeRenderer`): draws every standing tree in `World.Trees` with the
  `visual.tree.generic` definition. Trees are grouped in 64 m cells, frustum-culled, and drawn with
  `Graphics.RenderMeshInstanced` in batches of 1023. Cells are rebuilt only when `TreeRegistry.Version` changes.
  There are no per-tree GameObjects and no colliders (decision 0008).
- Roads (`RoadRenderer`, `Assets/Game/Visuals/`): one child GameObject with a mesh per `RoadSegment`, built from its
  samples, the type width and the Sim terrain height + 0.05 m. `Sync()` (every frame) rebuilds only when
  `RoadNetwork.Version` changes: new segments get meshes, removed ones are destroyed. `ShowPreview(RoadPreview, width)`
  draws the draft as one mesh, red where `InvalidRanges` apply. No colliders (decision 0016).
- Areas (`AreaRenderer`, `Assets/Game/Visuals/`): one child object per confirmed area (tinted fill plus outline, draped
  on Sim terrain; building areas solid, work areas dashed blue), built from the flattened polygon. Rebuilt when
  `AreaRegistry.Version` changes. `ShowPreview(AreaPreview, AreaShape)` draws the draft (vertex dots, edge handles,
  red when invalid); `HidePreview()` hides it. Colours are constants in the class, not `VisualDefinition`s. No colliders.
- No DOTS/ECS code yet. Rendering is `Graphics.RenderMeshInstanced` and MonoBehaviours (decision 0002 is still provisional).
- Sun and sky (`Assets/Game/Lighting/`): `SunLightModel` is pure maths (Sim `GameTime` + `Daylight` → elevation,
  azimuth, colour, intensity). `SunLightController` writes the result to the directional light and ambient in
  `LateUpdate`. Steady light is `ViewSettings.SteadyLight` (set by the top bar): fixed sun, visual only.
- Top bar (`TopBarController`, UI Toolkit, `Assets/Game/UI/TopBar/`): date and time, pause, speed buttons from
  `Pacer.Speeds`, steady-light toggle, "running slower" notice (`SlowedDown`), snapping toggle. Sends commands through `SimHost`.
- Snapping (T-096): `ViewSettings.Snapping` (default On) and `SnapDistancePx` (12, area tools) and `RoadSnapDistancePx` (16, road tool) are presentation-only. The top bar
  button "Snapping: On/Off" and the Input System action `ToggleSnapping` (`SnappingHotkey`, bound to `<Keyboard>/alt`,
  a button action so it toggles once per press) both flip `Snapping`. Tools read it; the Sim never does (decision 0017).
- No-focus rule (ui.md UIK-01, UIK-02, `UiFocusRules` in `Assets/Game/UI/`): game UI never takes keyboard focus. Top bar,
  build bar, tool panel and debug overlay call `UiFocusRules.Apply(root)`, which sets `focusable = false` and stops
  NavigationMove/Submit/Cancel events at the panel root. Camera and tool keys read `Keyboard.current` directly.
  New game UI must call it too.
- Tools (`Assets/Game/Tools/`):
  - `ITool` (Id, HasDraft, Activate, Deactivate, Cancel, HandleEscape). `ToolController` keeps exactly one active tool:
    `Register(tool)`, `Toggle(id)` (second click deactivates; switching cancels and deactivates the old tool),
    `HandleEscape()` (tool first, deactivate only if it returns false), `IsPointerOverUi` / `IsPointerOverUiAt(screenPos)`. It sets
    `CameraRig.ToolActive`; the rig then refuses left/right mouse (`AcceptsMouseButton`), middle/scroll/keys/edge pan unchanged.
  - `RoadTool` (id `road`, registers itself in `OnEnable`) holds only draft points. It calls `RoadValidator` for the preview
    and `SimHost.Execute` for `DrawRoad` and `DeleteRoadSegment`. Holds no road rules. Rules: roads.md; input: ui.md.
  - `Tools/Shared/`: `PressTracker` (click, drag, double-click; 4 px drag threshold, 6 px double-click radius; time passed
    in), `SnapPicker` (best candidate by kind order within a pixel limit), `ToolHitTester` (draft point hits).
  - `AreaTool` (ids `test-area`, `test-work-area`): draft points with a parallel edge list. A click on an edge inserts a point
    (max 8), dragging an edge's middle handle makes it Bent through the cursor, double-clicking the handle straightens it.
    With `ViewSettings.Snapping` on, points snap (SnapPicker order: area vertex, road side line via `RoadSegment.SideLine`, area
    edge via `AreaRegistry.FindNearestSnap`) within `SnapDistancePx`; two consecutive points snapped to the same road side
    make that edge FollowsRoad. Snapping off: no snap, no FollowsRoad. T-166: `CurrentShape` also picks NearRoad (one end snapped to a road
    side, the other outside it within 10 m) and NearArea (same against another area's outline, via `SnapMarker.AreaId`); a handle
    double-click locks the edge Straight until an end moves. Validation stays in `AreaValidator`.
    Cursor (AR-20, T-164): every pointer move shows a 0.6 m `CursorMarker` sphere at the (snapped) ground point, green when
    snapped, hidden over UI and on deactivate. The ghost (AR-21, `AreaRenderer.ShowGhost`) is built but off behind
    `AreaTool.GhostEnabled` (default false) until polished.
- Build bar (`BuildBarView`, `Assets/Game/UI/BuildBar/`, UXML/USS): non-focusable buttons built from `ToolEntry.Defaults()`
  (Road, Test area and Test work area enabled; the rest greyed, tooltip "Coming later"). Raises `ToolClicked`.
- Tool panel (`ToolPanelView`, `Assets/Game/UI/ToolPanel/`, UXML/USS): status line, hint line, Cancel/Confirm.
  `Show(status, hint, isValid)`, `Hide()`, events `Cancelled` and `Confirmed`. Holds no tool rules. Placed in the Game
  scene by `GameSceneGenerator.CreateRoadTool`.
- `ViewSettings` (`Assets/Game/UI/`) holds presentation-only settings. The Sim never reads them.
- Camera (`CameraRig` on Main Camera): pan, scroll zoom and middle-drag orbit (yaw + tilt around the ground point at screen centre, pitch 10-85 degrees, camera held above terrain + clearance) over the terrain. Q/E and right mouse are not camera inputs. Reads ground height from the Sim
  terrain, not from the collider.
- Cursor ground readout (`Assets/Game/Picking/`): `GroundPicker` casts a ray from the camera (screen position or
  `Ray`) and keeps only the hit's x/z. Height and slope come from `SimHost.World.Terrain` at that x/z, never from the
  collider. `GroundSample` is the result. `GroundReadoutView` shows it in one label each frame. `ReadAt(x,z)` reads
  the Sim without a ray. `TryWorldToScreen` maps Sim points to the screen for tool hit tests.
- Debug overlay (`Assets/Game/UI/DebugOverlay/`): `DebugOverlayView` (UI Toolkit, F3 toggles) shows FPS, tree count,
  `SimStats` tick ms and ticks/s, the tree nearest the cursor ground point (`TreeRegistry.QueryRadius`), road node,
  segment and network counts, and the road segment under the cursor (`RoadNetwork.FindNearest` within 4 m) with its
  end nodes and reachable segment count. Also shows the area count (`AreaRegistry.Count`) and the area under the cursor
  (building area first, then work area, via `AreaRegistry.FindAt` on the ground point): name, type name and m² from the
  flattened polygon (`debug-area-count`, `debug-area-name`, `debug-area-type`, `debug-area-m2`; "Area --" when none).
  Read-only; one named label per value (`debug-*`, `debug-road-*`, `debug-seg-*`).
- Visuals (`LordsLegacy.Game.Visuals`, `Assets/Game/Visuals/`): every drawn thing gets its mesh and materials from a
  `VisualDefinition` asset referenced by string id (e.g. `visual.tree.generic`, `visual.road.dirt_path`). Sim data holds
  only the id (decision 0009). `VisualRegistry.asset` maps ids to definitions: `TryGet(id, out def)` for lookup,
  `Get(id)` logs an error and returns a built-in fallback placeholder for unknown ids. `IsPlaceholder` marks stand-in art;
  menu `LordsLegacy/Art/List placeholder visuals` (`PlaceholderVisualLister`) logs them (ART-03). The placeholder
  assets are written by `LordsLegacy/Art/Generate placeholder visuals` (`PlaceholderVisualGenerator`, idempotent).
  Swap art by editing the definition's mesh and materials.

## Data flow

_Owner: whichever task changes the flow between layers. Other tasks do not edit this section._

Player input → Unity layer → Sim command → `SimWorld.Execute` → Sim state → Unity layer renders.

Concretely at startup: `SimHost.Start` reads `StreamingAssets/Maps/testmap.lsmap` through `MapDataIO.Read`, calls
`Initialize(SimTuning, MapData)` and builds `SimWorld.FromMapData`. Each frame `SimHost.Update` converts real
seconds into whole ticks through `TickPacer.Advance` and calls `SimWorld.Step(n)`. The Unity side reads Sim
state (`World`, `Pacer.Fraction`) to draw and sends commands back only through `SimHost` methods. Sim state is never
written by views, and Sim code never reads Unity terrain (the baked map is the only terrain input).

Road drawing, as an example: `RoadTool` gets a click, snaps it (`RoadNetwork.FindNearest`, presentation only) and adds
it to the draft. `RoadValidator.Validate` gives a preview for `RoadRenderer.ShowPreview`. Confirm sends `DrawRoad` through
`SimHost.Execute`; the Sim validates, splits and adds segments, and clears trees. The next `RoadRenderer.Sync` sees the
new `RoadNetwork.Version` and builds meshes for the new segments only.

Area drawing follows the same path: `AreaTool` previews with `AreaValidator.Validate`, confirm sends `DesignateArea`
through `SimHost.Execute` (which also clears trees), and `AreaRenderer.Sync` sees the new `AreaRegistry.Version`.

## Scene

_Owner: the scene and prefab tasks. Other tasks do not edit this section._

- `Assets/Scenes/Game.unity` is written by `GameSceneGenerator` (Assets/Editor/Scenes). **To regenerate the scene run the
  menu item `LordsLegacy/Scenes/Generate Game scene`**, then commit the result. It rebuilds from an empty scene each run,
  and `SceneYamlNormalizer` gives its objects stable fileIDs, so two runs give the same file. Serialized references are wired in code.
- Contents: `Terrain`; `SimHost`; `Main Camera` (Camera + `CameraRig`); `Directional Light` (Light + `SunLightController`
  -> SimHost, light, TopBar); `TreeRenderer` (-> SimHost, `VisualRegistry.asset`, main camera); `GroundPicker`
  (-> main camera, SimHost); UIDocuments `TopBar` (`TopBarController`), `BuildBar` (`BuildBarView`, plus `ToolController`
  -> build bar, camera rig), `ToolPanel` (`ToolPanelView`), `GroundReadout` (`GroundReadoutView` -> picker) and
  `DebugOverlay` (`DebugOverlayView` -> SimHost, picker; F3 toggles); `RoadRenderer` (-> SimHost, `VisualRegistry.asset`);
  `RoadTool` (-> SimHost, picker, RoadRenderer, ToolPanel, ToolController, TopBar); `AreaRenderer` (-> SimHost) and two
  `AreaTool`s, `TestAreaTool` (`test-area`, `area.test`) and `TestWorkAreaTool` (`test-work-area`, `area.test_work`), each
  -> SimHost, picker, AreaRenderer, ToolPanel, ToolController, TopBar (`GameSceneGenerator.CreateAreaTools`, T-116). All UIDocuments use
  `Assets/Game/UI/PanelSettings.asset`. `GameSceneGenerator.CreateBuildBar` and `CreateRoadTool` wire these in code.
- Other editor tools (`Assets/Editor/`): `AgentFriendlyProjectSettings` forces text serialization and visible
  .meta files when the Editor loads; `CreatePanelSettings` (menu `Lords Legacy/Create Shared PanelSettings`) makes the
  shared PanelSettings asset. Scenes and placeholder assets are generated, not hand-edited (decision 0010).
- `GameSceneTests.GameSceneHasEveryM1PieceWired` (PlayMode) loads the scene and checks each piece and its references.
  `GameSceneTests.GameSceneHasM3RoadToolWired` does the same for BuildBarView, ToolPanelView, ToolController,
  RoadRenderer and RoadTool; the UIDocument list must match the generator. `GameSceneTests.GameSceneHasM4AreaToolsAndOverlayWired`
  checks AreaRenderer, the two AreaTool ids and the overlay's area labels (T-116).
