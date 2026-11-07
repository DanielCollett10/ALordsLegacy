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

- `Assets/Sim/Core/SimWorld.cs` (`LordsLegacy.Sim.Core`) is the root. It owns the tick clock, pause, `Terrain`
  (`IHeightField`) and `Trees` (`TreeRegistry`). Build it with `new SimWorld(tuning)` (flat ground, no trees) or
  `SimWorld.FromMapData(map, tuning)`. **One tick = one game minute.** `Step(n)` advances `Tick` by n and does
  nothing while `Paused`; `Pause()` and `Resume()` set it. `Now` is the `GameTime` for the current tick.
  The Sim has no notion of real seconds.
- `Assets/Sim/Time/` (namespace `LordsLegacy.Sim.Calendar`, to avoid clashing with `UnityEngine.Time`):
  - `GameCalendar`: tick → `GameTime` (year, date, weekday, time, season). 365-day year, no leap years,
    the year number increments on 1 January.
  - `Daylight`: seasonal sunrise and sunset from min/max daylight hours. `For(dayOfYear)` gives `SunTimes`;
    `IsDaylight` and `SunProgress` serve the lighting view. Pure, no state.
  - `TickPacer`: owns the selected speed (`SetSpeed`, `CurrentSpeed`, `Speeds`) and turns real seconds into
    whole ticks (`Advance`), carrying the remainder (`Fraction`). Over `MaxTicksPerFrame` it drops the excess
    and sets `SlowedDown`. `Reset()` clears the carry (the host calls it while paused). No pause state (decision 0006).
- `Assets/Sim/Tuning/SimTuning.cs` (`LordsLegacy.Sim.Tuning`) holds the balancing numbers defined so far. Defaults are
  auto-properties; derived values are computed properties. Read by code today: pace, speeds, start date, daylight,
  standard log length. Defined but not yet read by any system: walk and ox speed, trunk length range, starting oxen,
  rest-day switch. Road and area shared values (`RoadSampleSpacingM`, `RoadMinPointSpacingM`, `RoadTreeClearanceM`,
  `CurveFlattenSpacingM`, `SlopeSampleGridM`, `OverlapToleranceM2`, `SpatialIndexCellM`) are defined, read by roads/areas
  (not built yet). The Sim never hard-codes these. Food, markets, construction and population numbers from
  `docs/design/tuning.md` are not in code yet.
- Not built yet: tasks, pathing, buildings, citizens, economy, saving. Decision 0005 (task types as data) describes
  future work with no code.
- `Assets/Sim/Forestry/` (`LordsLegacy.Sim.Forestry`): `TreeRegistry` holds all trees keyed by `TreeId`, with a
  uniform 16 m spatial grid (`QueryRect`, `QueryRadius`, `LogYield`). `TreeRecord` holds id, x/z, usable trunk
  length and `State` (only `Standing` exists yet). Trees come from the baked map. `Remove(TreeId)` takes a tree out of
  the records and grid (false if unknown); a removed id can never be added again (BLD-10-D). `Version` increases on
  every Add or Remove, so views can rebuild only when it changes. `Count`, `All` and the queries show standing trees only.
  `TreeClearing` (static) removes the trees under a polygon (`RemoveInPolygon`, trunk inside or on the edge) or within a
  distance of a polyline (`RemoveNearPolyline`, inclusive), using geo-1 on trunk positions rounded to mm; both return the removed ids sorted.
- `Assets/Sim/Geometry/` (`LordsLegacy.Sim.Geometry`, decision 0012): integer-mm `PointMm`, `Segments` (exact
  classify/intersect), `Polygons` (area, winding, `IsSimple`, `Locate`, bounds), `Polylines` (length, nearest,
  min distance). Predicates are exact; distances are double mm.
- `Assets/Sim/Roads/` (`LordsLegacy.Sim.Roads`, decision 0013): `RoadTypeDef` / `RoadTypes` (`road.dirt_path`),
  `CurveKind`, `ICurveSampler` and `CatmullRomSampler` (centripetal; per span it samples evenly by arc length so
  samples are ~`RoadSampleSpacingM` apart and hit every control point). `ControlPointFilter.Clean` applies RD-02
  (`RoadMinPointSpacingM`).
  `RoadNetwork` (`SimWorld.Roads`, never null) holds `RoadNode`s and `RoadSegment`s (type id, curve kind, control
  points, samples, length m), with ascending-id iteration and ids never reused. `AddSegment` reuses a node at the exact
  end position. `SplitSegmentAt` cuts the samples and control points as decision 0013 says, removes the original id and
  issues two new ones (no-op at an end). `RemoveSegment` drops emptied nodes. `Version` rises on every change;
  `AreConnected`, `ComponentCount`, `ReachableSegmentCount`; `FindNearest` returns a `RoadHit` (None/End/Line), preferring
  a node in range over a line point.
  `RoadValidator.Validate(world, controlPoints, typeId, ignoreSegmentId = -1)` is the one check used by the tool preview
  and the future command. It samples the curve, then returns a `RoadPreview` (samples, `InvalidRanges` of too-steep
  sample indices, `PlannedJunction`s ordered along the new road, length, max grade, `RoadRefusal` reason). Reasons in
  order: outside map, too steep, crosses itself, overlaps road. Junctions: `T` (a new end within half a width of a
  centre line, snapped to the nearest point), `JoinNode` (that point is an existing segment end, no split) and `X`
  (centre lines cross). Overlap ignores samples within one width of a junction and the stretch next to it that is still
  moving away from the road. A flat (no-map) world counts every point as inside. It never changes the world.
- `DrawRoad` (Commands, with internal `RoadCutter`) re-runs `RoadValidator` (rejects with its reason: unknown type / too few
  points → InvalidArgument, overlap → Conflict, other → NotAllowed). If valid it splits each existing segment at its T/X
  junction (`SplitSegmentAt`), then cuts the new road's samples at every junction (cut point added as a sample and as a
  control point on both sides; all original samples kept; a T/JoinNode end is snapped by adding the node position as a new
  first/last sample and control point) and adds one segment per piece. `Created` = new segments plus split halves,
  `Removed` = the split originals (both segment ids). After the pieces are added, every standing tree within half the
  road width + `RoadTreeClearanceM` of each new segment's centre line is removed (RD-11, BLD-10-B, via
  `TreeClearing.RemoveNearPolyline`); those ids are `RemovedTreeIds` on the result (decision 0015).
- `DeleteRoadSegment(segmentId)` (Commands) removes one segment with `RoadNetwork.RemoveSegment`; nodes left with no
  segments go with it. Unknown id → `NotFound`. Trees are not touched and never come back (RD-12, BLD-10-D).
  Works while paused, like every command.
- `Assets/Sim/Commands/` (`LordsLegacy.Sim.Commands`, decision 0011): `ISimCommand` (`Validate` must not mutate;
  `Apply` runs only after an accepted `Validate`), `CommandResult` (immutable: Accepted with created/removed segment ids and removed tree ids (`RemovedTreeIds`, decision 0015),
  or Rejected with `CommandRejectCode` and text), `CommandLog` (append-only `(tick, command)` entries).
  `SimWorld.Execute(cmd)` runs validate-then-apply immediately, works while paused, never changes `Tick`, and appends
  every call (accepted or rejected) to `SimWorld.CommandLog` with the current tick. A rejected command changes nothing.
  Unity calls `Execute` only through `SimHost`.
- Pure C#, no `UnityEngine`. Tests live in `Sim.Tests/`, which compiles the same `Assets/Sim/**/*.cs`.

## Map pipeline

_Owner: the map and terrain tasks. Other tasks do not edit this section._

- Terrain and trees are baked into Sim map data (decision 0003). The Sim never asks Unity for
  terrain; an editor bake step writes a versioned map file that the Sim reads.
- `Assets/Sim/Terrain/`: `IHeightField` (HeightAt, SlopeDegAt, Contains, Bounds; out-of-bounds queries clamp)
  is the only terrain API Sim code uses. `HeightGrid` (metres, bilinear, central-difference slope; ~36 MB for
  6 km at 2 m) and `FlatHeightField` (no map) implement it. `Bounds2` is the rectangle type.
- `Assets/Sim/Map/` (`LordsLegacy.Sim.Map`): `MapData` (format version, map id, `HeightGrid`, `MapTree` list),
  `MapDataIO` (binary read/write via `System.IO`, from `byte[]` or `Stream`; unknown version, bad magic or
  truncation throw `MapDataFormatException`) and `SyntheticMap.CreateRamp()` (200x200 m test map, 5 trees).
- `SimWorld.Terrain` and `SimWorld.Trees` are never null. `new SimWorld(tuning)` gives `FlatHeightField` and no
  trees; `SimWorld.FromMapData(map, tuning)` uses the map's grid and trees.
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
  `World`, `Pacer`, `TickFraction` and `Stats`, and send commands only through `TogglePause` and `TickPacer.SetSpeed`.
- `SimStats` (`LordsLegacy.Game`) is the only Unity-side measurement: `LastStepMs` and `TicksPerSecond`
  (ticks in the last real second). Sim code never reads it.
- Terrain: Unity Terrain built from `TestMapTerrain.asset` is drawn by Unity and is the collider for picking.
  It is not built from Sim data.
- Trees (`Assets/Game/Visuals/TreeRenderer`): draws every standing tree in `World.Trees` with the
  `visual.tree.generic` definition. Trees are grouped in 64 m cells, frustum-culled, and drawn with
  `Graphics.RenderMeshInstanced` in batches of 1023. When `TreeRegistry.Version` changes, only the cells whose
  standing tree ids differ are rebuilt (empty cells are dropped); `Invalidate()` and a mesh or scale change rebuild all. Each tree is scaled to its trunk length and yawed from its id.
  There are no per-tree GameObjects and no colliders (decision 0008).
- No DOTS/ECS code yet. Rendering is `Graphics.RenderMeshInstanced` and MonoBehaviours (decision 0002 is still provisional).
- Sun and sky (`Assets/Game/Lighting/`): `SunLightModel` is pure maths (Sim `GameTime` + `Daylight` → elevation,
  azimuth, colour, intensity). `SunLightController` writes the result to the directional light and ambient in
  `LateUpdate`. Steady light is `ViewSettings.SteadyLight` (set by the top bar): fixed sun, visual only.
- Top bar (`TopBarController`, UI Toolkit, `Assets/Game/UI/TopBar/`): date and time, pause, speed buttons from
  `Pacer.Speeds`, steady-light toggle, "running slower" notice (`SlowedDown`). Sends commands through `SimHost`.
- Snapping (T-096): `ViewSettings.Snapping` (default On) and `SnapDistancePx` (12) are presentation-only. The top bar
  button "Snapping: On/Off" and the Input System action `ToggleSnapping` (`SnappingHotkey`, bound to `<Keyboard>/alt`, a
  button action so it toggles once per press, not on hold) both flip `Snapping`; the label follows. Drawing tools read
  it; the Sim never does.
- No-focus rule (ui.md UIK-01, UIK-02, `UiFocusRules` in `Assets/Game/UI/`): game UI never takes keyboard focus. Top bar
  and debug overlay call `UiFocusRules.Apply(root)`, which sets `focusable = false` on every element and stops
  NavigationMove/Submit/Cancel events at the panel root (trickle-down), so WASD/arrows never move a UI selection.
  Camera and tool keys read `Keyboard.current` directly and are unaffected. New game UI must call it too.
- Build bar and tools: `BuildBarView` (`Assets/Game/UI/BuildBar/`, UXML/USS) builds non-focusable buttons from `ToolEntry` list
  (`ToolEntry.Defaults()`; disabled ones greyed, future ones tooltip "Coming later") and raises `ToolClicked`.
  `ToolController` (`Assets/Game/Tools/`) keeps one `ITool` active: `Toggle(id)` (second click deactivates; switching calls
  Cancel+Deactivate on the old tool), `HandleEscape()` (tool first, deactivate only if it returns false), `IsPointerOverUi`
  (bar hover). It sets `CameraRig.ToolActive`; the rig then refuses left/right mouse (`AcceptsMouseButton`), middle/scroll/keys/edge pan unchanged.
  Tools register with `ToolController.Register`; no concrete tool exists yet.
- Tool panel (T-097, `ToolPanelView`, `Assets/Game/UI/ToolPanel/`, UXML/USS, ui.md "Tool panel"): a reusable panel above the
  build bar with a status line, a hint line and Cancel/Confirm. `Show(status, hint, isValid)` (Confirm disabled while invalid),
  `Hide()` (hidden by default), events `Cancelled` and `Confirmed`. Buttons are non-focusable (UiFocusRules). Holds no tool
  rules; a tool calls it while it has a draft. Not yet placed in the Game scene (no tool uses it yet).
- Road tool (T-100, `RoadTool` in `Assets/Game/Tools/`, id `road`): left click adds a point via `GroundPicker` + `PressTracker`; with 2+ points it
  runs `RoadValidator` and `RoadRenderer.ShowPreview`; Enter or panel Confirm sends `DrawRoad` through `SimHost.Execute`. Accepted clears the
  draft; rejected shows the reason and keeps it. A right click (not drag) or Escape cancels. The tool panel shows while the tool is active.
  T-101: a left press within the hit radius of a placed point (`ToolHitTester` vertex hits, screen positions from
  `GroundPicker.TryWorldToScreen`) and a drag (`PressTracker`) moves that point live; the preview follows. Backspace
  removes the last point. A double click replaces the point its first click added with one final point at the cursor, then confirms.
  `GameSceneGenerator.CreateRoadTool` adds `RoadRenderer`, `ToolPanel` and `RoadTool` to the scene; the tool registers itself with `ToolController`.
- `ViewSettings` (`Assets/Game/UI/`) holds presentation-only settings. The Sim never reads them.
- Camera (`CameraRig` on Main Camera): pan, zoom and rotate over the terrain. Reads ground height from the Sim
  terrain, not from the collider.
- Cursor ground readout (`Assets/Game/Picking/`): `GroundPicker` casts a ray from the camera (screen position or
  `Ray`) and keeps only the hit's x/z. Height and slope come from `SimHost.World.Terrain` at that x/z, never from the
  collider. `GroundSample` is the result (`Format()` gives the label text). `GroundReadoutView` (script in
  `Assets/Game/Picking/`, UI Toolkit assets in `Assets/Game/UI/GroundReadout/`) shows it in one label each frame.
  `ReadAt(x,z)` reads the Sim without a ray.
- Debug overlay (`Assets/Game/UI/DebugOverlay/`): `DebugOverlayView` (UI Toolkit, F3 toggles) shows FPS, tree count,
  `SimStats` tick ms and ticks/s, and the tree nearest the cursor ground point (id, trunk, log yield) via
  `TreeRegistry.QueryRadius`. Read-only; one named label per value (`debug-*`).
- Visuals (`LordsLegacy.Game.Visuals`, `Assets/Game/Visuals/`): every drawn thing gets its mesh and materials from a
  `VisualDefinition` asset referenced by string id (e.g. `visual.tree.generic`). Sim data holds only the id (decision 0009).
  `VisualRegistry.asset` maps ids to definitions: `TryGet(id, out def)` for lookup, `Get(id)` logs an error and
  returns a built-in fallback placeholder for unknown ids. `IsPlaceholder` marks stand-in art; menu
  `LordsLegacy/Art/List placeholder visuals` (`PlaceholderVisualLister`) logs them (ART-03). The placeholder
  assets are written by `LordsLegacy/Art/Generate placeholder visuals` (`PlaceholderVisualGenerator`, idempotent);
  the tree material has GPU instancing enabled. Swap art by editing the definition's mesh and materials.

## Data flow

_Owner: whichever task changes the flow between layers. Other tasks do not edit this section._

Player input → Unity layer → Sim command → `SimWorld.Step` → Sim state → Unity layer renders.

Concretely at startup: `SimHost.Start` reads `StreamingAssets/Maps/testmap.lsmap` through `MapDataIO.Read`, calls
`Initialize(SimTuning, MapData)` and builds `SimWorld.FromMapData`. Each frame `SimHost.Update` converts real
seconds into whole ticks through `TickPacer.Advance` and calls `SimWorld.Step(n)`. The Unity side reads Sim
state (`World`, `Pacer.Fraction`) to draw and sends commands back only through `SimHost` methods. Sim state is never
written by views, and Sim code never reads Unity terrain (the baked map is the only terrain input).

## Scene

_Owner: the scene and prefab tasks. Other tasks do not edit this section._

- `Assets/Scenes/Game.unity` is written by `GameSceneGenerator` (Assets/Editor/Scenes). **To regenerate the scene run the
  menu item `LordsLegacy/Scenes/Generate Game scene`**, then commit the result. It rebuilds from an empty scene each run,
  and `SceneYamlNormalizer` gives its objects stable fileIDs, so two runs give the same file. Serialized references are wired in code.
- Contents: `Terrain`; `SimHost`; `Main Camera` (Camera + `CameraRig`); `Directional Light` (Light + `SunLightController`
  -> SimHost, light, TopBar); `TreeRenderer` (-> SimHost, `VisualRegistry.asset`, main camera); `GroundPicker`
  (-> main camera, SimHost); UIDocuments `TopBar` (`TopBarController`), `GroundReadout` (`GroundReadoutView` -> picker) and
  `DebugOverlay` (`DebugOverlayView` -> SimHost, picker; F3 toggles). All UIDocuments use
  `Assets/Game/UI/PanelSettings.asset`.
- Other editor tools (`Assets/Editor/`): `AgentFriendlyProjectSettings` forces text serialization and visible
  .meta files when the Editor loads; `CreatePanelSettings` (menu `Lords Legacy/Create Shared PanelSettings`) makes the
  shared PanelSettings asset. Scenes and placeholder assets are generated, not hand-edited (decision 0010).
- `GameSceneTests.GameSceneHasEveryM1PieceWired` (PlayMode) loads the scene and checks each piece and its references.
