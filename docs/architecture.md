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
  `Terrain` (`IHeightField`), `Trees` (`TreeRegistry`), `Roads` (`RoadNetwork`), `Areas` (`AreaRegistry`), `Operations` (`OperationRegistry`) and `CommandLog`. All are
  never null. Build it with `new SimWorld(tuning)` (flat ground, no trees) or `SimWorld.FromMapData(map, tuning)`.
  Both take optional `RoadTypeCatalog` / `AreaTypeCatalog` arguments (decision 0019); the world exposes them as
  `RoadTypes` and `AreaTypes` (never null; null arguments give the static default definitions in `RoadTypes.All` /
  `AreaTypes.All`). All Sim code looks types up through the world (`world.RoadTypes.Find`, `RoadSegment.SideLine(side,
  catalog)`, `CorridorPolygon(catalog)`, `AreaFlattener.Flatten(..., roadTypes)`); the static `Find` methods and the
  catalog-less `SideLine`/`CorridorPolygon` overloads (`[Obsolete]`) remain only for the Unity layer until it migrates.
  **One tick = one game minute.** `Step(n)` advances `Tick` by n and does nothing while `Paused`; `Pause()` and
  `Resume()` set it. `Now` is the `GameTime` for the current tick. The Sim has no notion of real seconds.
  - **Partial convention (T-249):** `SimWorld` is `partial`. Each subsystem adds its state and helpers in
    `Assets/Sim/Core/SimWorld.<Area>.cs` (e.g. `SimWorld.Economy.cs`), so tasks do not all edit `SimWorld.cs`.
    Per-tick logic does not go into `Step` directly; it is an `ISimSystem`.
  - **Systems and fixed order (T-249, CLK-07):** `ISimSystem` has `Step(world)` (once per tick) and
    `OnCommandApplied(world, result)` (after an accepted command, from `Execute`). `Assets/Sim/Core/SimSystems.cs`
    holds the one ordered list (`SimSystems.CreateDefault()`), no reflection and no static mutable state. The world copies
    the list at construction, so the order is fixed for that world. Phase order, earliest first: **sync → routine →
    dispatch → steps → movement → operations** (the phase meanings are documented in `SimSystems.cs`). A new system
    goes in the block of its phase. `Step(n)` runs n ticks; each tick advances `Tick` by one, then runs every system's
    `Step` in list order. A paused world runs no systems on `Step`. `Execute` still notifies systems while paused,
    because commands work while paused. Rejected commands notify nobody.
    `SimWorld` takes an optional `systems` argument (null = `SimSystems.CreateDefault()`) so tests can supply their own order.
    Registered today: `NavSyncSystem` (sync), `RoutineSystem` (routine), `MovementSystem` and `DebugSendSystem` (movement).
    `DispatchSystem` (dispatch) and `StepRunnerSystem` (steps) are registered too (T-275). `WoodcuttingSystem` is the
    one operations-phase system (T-276, WC-10..15/25, TMB-05/07; decision 0031): for each Working woodcutting operation it keeps one Open
    `task.fell_tree` per idle adult worker of the assigned families, targeting the nearest standing unreserved tree to the
    hut access point inside `OperationStatus.WorkRegion`, reserved at creation. Steps = MoveTo + `ProcessDefs.Timber`
    effective stages (`TaskStep.StageId` carries the stage). `FellingOutcome` (called by `StepRunnerSystem` after each
    step) sets the tree Felled after fell, and on the last step Stump plus floor(trunk ÷ StandardLogLengthM) logs in an
    operation-owned ground pile at the tree. New tasks stop when log load + yield of unfinished tasks reaches storage
    capacity; open tasks are cancelled when the status is not Working.
  - **People (T-251, PPL-10..14):** `SimWorld.People` (`Assets/Sim/People/Population.cs`, partial in `SimWorld.People.cs`)
    holds `Family`, `Citizen` and `Ox` records by explicit int id, iterated in id order. Ids are never reused after
    removal. `Version` bumps on add/remove. `Citizen.AgeYears(nowTick, SimTuning.AgeingRate)` implements POP-27.
  - **Nav grid (T-256, decision 0022):** `SimWorld.Nav` (`Assets/Sim/Navigation/NavGrid.cs`, partial in
    `SimWorld.Nav.cs`, never null) is a grid of `NavCellSizeM` (2 m) cells over the terrain bounds with per-cell
    `IsBlocked` (centre slope over `MaxWalkSlopeDeg`, or centre inside a movement-blocking footprint) and
    `SpeedMultiplier` (highest road-corridor multiplier, else 1). `NavSyncSystem` is first in `SimSystems` and also runs on
    `OnCommandApplied`; the first `Sync` is a full build, later ones diff road segment / blocking footprint ids and
    recompute only the cells under added or removed items. `Version` bumps when any cell changes. A world with no map
    has a grid with no cells.
  - **Pathfinder (T-257, MOV-10..13):** `GridPathfinder.Find(nav, PathPoint from, PathPoint to)` (`IPathfinder` for
    injection) runs 8-neighbour A* over `NavGrid` (no corner cutting; step cost = length ÷ mean of the two cells'
    multipliers; heuristic ÷ top multiplier; ties by f, h, cell index). Blocked ends snap to the nearest walkable
    centre within 20 m. Result `PathResult` (`Ok`, `Points` in metres, `TravelMinutes` at 1 m/min) is smoothed over
    straight lines crossing only equal-multiplier walkable cells.
  - **Movement (T-258, MOV-12/14/15):** `Citizen` and `Ox` implement `IMovable` (position, `FacingRad`,
    `PreviousX/Z`, `CurrentMultiplier`, `Move`). `Move` is a `MoveOrder` (target, `Path`, `SegmentIndex`, `ProgressM`,
    `Status` Pending/Moving/Done/Failed, `FailReason`); the dispatcher sets it, `MovementSystem` (movement slot in
    `SimSystems`) advances it by walk/ox speed × the multiplier of the cell under the agent each tick. When `Nav.Version`
    changes, agents on blocked cells move to the nearest walkable point and unfinished orders re-plan from where the agent
    stands. Finished orders stay on the agent until their owner clears them. `Activity` is not touched.
  - **Daily routine (T-259, PPL-24/27/30..32):** `RoutineSystem` (`Assets/Sim/People/`, routine slot, before movement)
    keeps a per-citizen state (Asleep / Milling / GoingHome) and only issues `MoveOrder`s. At `Daylight` sunrise plus a
    seeded 0–`WakeStaggerMaxMinutes` delay a sleeper mills: walk to a seeded walkable point within `MillRadiusM` of
    `Camp.Fire` (family home if no camp), stay `MillStayMinMinutes`–`MillStayMaxMinutes`, repeat. At sunset citizens walk
    home and become Sleeping on arrival. Oxen stay Waiting. A citizen with an order set by someone else is skipped until
    that order finishes, so later phases (work, debug send) can take over. Milling shows as `MillingAbout` (walking
    to a spot included); only the trip home is `Walking`.
  - **Debug send-to (T-260, PPL-33; decision 0026):** `Citizen.DebugSend` (`DebugSendState`: the walk `Order` and
    `LingerUntilTick`) is set by `SendCitizenTo(citizenId, PathPoint)` (`Assets/Sim/Commands/`; unknown id → `NotFound`,
    no route from where the citizen stands → `NotAllowed` "No route", non-finite point → `InvalidArgument`). While it is set,
    `RoutineSystem` skips the citizen and drops its routine state. `DebugSendSystem` (movement slot, after
    `MovementSystem`) sets the linger on arrival (`DebugLingerMinutes`, 30), then clears `DebugSend` so the routine starts
    afresh (by day it mills, at night it walks home). A walk that fails mid-way ends the send at once. Works while paused. Unity calls it only from `DebugSendController` (Editor and development builds).
  - **Task board (T-274, TSK-01..06, decision 0005):** `SimWorld.Tasks` (`Assets/Sim/Tasks/`, partial in
    `SimWorld.Tasks.cs`, never null) is a `TaskBoard` of `TaskRecord`s built from `TaskDef`s (id, explicit `TaskStep`
    list of MoveTo / Work(duration) each with a heavy flag, `EligibilityPolicy`, `RequiredResource`). State machine
    Open → Assigned → Active → Done, or Cancelled; illegal transitions return false. A task reserves trees in
    `Trees` using its own id as owner; `Cancel` and completion release them. Queries `ByOwner`, `ByAssignee`,
    `InState`, `IsBusy(citizenId)` (holds an Assigned/Active task); `Version` bumps on change.
  - **Dispatcher and step runner (T-275, TSK-07/08/12/13, PPL-44/45, WC-26):** `DispatchSystem` runs when
    `Tick % SimTuning.DispatchIntervalMinutes (10) == 0`. Idle = awake, no debug send, no task (`IsBusy`), and not mid-walk
    unless milling. Pass 1: workers (id order) of the family whose `WorkplaceId` = task owner; pass 2: helper families
    (no workplace) for `AssignedThenHelpers`/`HelpersOnly`. An assigned family never takes other work. Heavy tasks need
    age 14+. Best task = highest priority, then shortest straight-line distance to the target tree, then id. It reserves
    the tree, then `Assign` + `Start`. Tasks needing an ox stay open (no ox handling yet); a task whose tree is gone is
    cancelled. `StepRunnerSystem` runs Active tasks in id order: MoveTo issues a `MoveOrder` to the tree and waits (Walking);
    Work adds one progress tick per tick (Working). Target gone, no assignee or a Failed move cancels the task and frees
    the worker. `RoutineSystem` ignores busy citizens and resumes when the task ends.
  - **Working day (T-277, PPL-21/24..26/45):** `SimWorld.WorkDay` (`Assets/Sim/People/WorkDay.cs`) plans shifts for adult
    members of families with a `WorkplaceId`; everyone else is always "on shift" (M7 routine). Work ends at sunset
    (Sunday: minus `SundayMealMinutes`). Depart tick = max(sunrise − commute, previous work end + commute +
    `MinSleepHours`), commute = path travel time to the task's target tree, planned once per morning per citizen
    (a worker already awake leaves at once). `DispatchSystem.IsIdle` lets sleeping assigned workers take tasks at night.
    Off shift, `StepRunnerSystem` skips the worker's Active task (progress and reservations kept) and `RoutineSystem`
    walks the worker home to sleep; on shift the step runner resumes (a task paused mid-Work first walks back to the
    tree). Assigned workers with no task mill about near their family home.
  - **Footprints (T-252, MOV-03/04):** `SimWorld.Footprints` (`Assets/Sim/Footprints/`, partial in `SimWorld.Footprints.cs`)
    holds `Footprint` records (oriented rectangle: CCW `Polygon`, `AccessPoint` 1 m outside the front edge at local -Z)
    of a `FootprintTypeDef` (size, `BlocksMovement`, `ClearsTrees`; `footprint.camp` is 24x16 m, walkable, clears trees).
    `FindAt`, `QueryRect`, `Ids`, and `Version` (bumps on add/remove).
    `FootprintTypeDef` also has `LogCapacity` / `OxCapacity` (0 by default). `ModuleTypes` (`Assets/Sim/Footprints/`,
    T-267) holds the woodcutting module types: hut 6×4 (blocks), log storage 8×6 (20 logs), oxen stand 3×3 (1 ox).
  - **Operation types (T-267, woodcutting.md WC-01..WC-11):** `Assets/Sim/Operations/` (`LordsLegacy.Sim.Operations`)
    holds `OperationTypeDef` (ids of the building and work area types, hut, log storage, optional oxen stand, family
    slots, default work radius) and `OperationTypes` (`Woodcutting`, `Find`, `FindByBuildingArea`, `All`). Static lookups,
    not on the world catalogs (decision 0019 says they should be; open question for the human). The woodcutting area
    types live in `AreaTypes`.
  - **Operations (T-268, WC-05/15/25):** `SimWorld.Operations` (`OperationRegistry`, partial in `SimWorld.Operations.cs`,
    never null) holds `Operation` records (id, type, area id, module footprint ids, family ids, ox ids, optional work
    area id, paused flag); every change bumps `Version`. `DesignateArea` of a building area type that has an operation
    type creates one; `DeleteArea` removes it and its module footprints (ledger locations are not cleaned up).
    `OperationStatus.Of(world, op)` is computed, never stored: Working or the first of Paused, MissingHut,
    MissingStorage, NoWorkers (no member aged 14+), NoTrees (`TreeRegistry.AnyStandingIn` on the work polygon or the
    default circle round the hut), StorageFull. `OperationStatus.LogLoad` is the single place the storage-full sum
    lives (own ground piles plus own storages, against total storage capacity).
    **T-292 (WC-25, WC-37, MVP-1-08/10/11):** `LogLoad` now also counts logs on the operation's ox carriers (assigned oxen
    and oxen of its tasks) and reserved incoming room, but only the part not already backed by logs in piles or on carriers
    (`incoming − reserved pile stock − carried`, floored at 0), so a haul is never counted twice. Felling stops at capacity and
    restarts when logs leave storage or a second storage raises capacity (to 40). A wind-down that drops logs releases the
    haul's room at once.
  - **Module placement (T-269, WC-03/04/07/08):** `ModulePlacement.Preview(world, operationId, moduleTypeId, centre,
    rotationDeg)` (`Assets/Sim/Operations/`, pure) returns a `ModulePlacementResult` (`Ok`, `Refusal`: UnknownOperation,
    OutsideArea, OverlapsModule, NotAllowedType, TooMany, plus `Text`). Fully inside = footprint area missing from the
    area polygon ≤ 0.001 m² and no corner more than 2 mm outside; touching edges is fine. `PlaceModule` re-runs it,
    adds the footprint (`FootprintRegistry.NextId()`), lists it on the operation, clears trees under it (ids in
    `RemovedTreeIds`); built at once. `RemoveModule(footprintId)` removes it (unknown → NotFound). Blocking modules push
    agents aside through the existing nav sync + `MovementSystem`.
  - **Operation commands (T-270, WCO-05/10, POP-05, WC-12):** `AssignFamily` / `UnassignFamily` (`Assets/Sim/Commands/`)
    set and clear `Family.WorkplaceId` (= operation id) and the operation's family list; assign is refused (NotFound /
    NotAllowed) for unknown ids, a family that already has a workplace, or no free slot. Unassign cancels the
    operation's unfinished tasks held by the family's members via `TaskBoard.Cancel` (reservations released).
    `PauseOperation`, `ResumeOperation`, `SetWorkArea` (area must be the type's work area type) and `UseDefaultWorkArea`
    are in `OperationSettings.cs`. `DeleteArea` resets any operation linked to the deleted work area to the default.
    Nothing assigns automatically.
  - **Inventory ledger part 1 (T-273, INV-01/11, LED-01/03/09; decision 0032):** `SimWorld.Inventory` (`Assets/Sim/Inventory/`, partial in
    `SimWorld.Inventory.cs`) is a `Ledger` of `LedgerLocation`s (`GroundPile` at a point with optional owner operation id,
    or `Storage` bound to a module id). Items enter only via `Produce(location, item, n)` / `ProduceOnGround`, which merges
    into the nearest same-owner pile within 1 m. `TotalOf(item)`, per-location counts, `Version`, and `Audit()` (null =
    consistent; compares location sums with the produced journal). `Items.Log` is `item.log` (category Log).
  - **Inventory ledger part 2 (T-286, LED-02..06, INV-02/05, WC-37):** locations add `Carrier` (`AddCarrier(cap)`, an ox) and
    `Stockpile` (`AddStockpile()`, unlimited). `Storage` capacity comes from the module type (`GetOrAddStorage(id, type)`);
    `Produce` throws past capacity. Per location/item: total, `ReservedOf`, `IncomingOf`, `AvailableOf` (= total − reserved),
    `FreeSpace` (= capacity − total − incoming). `ReserveStock` / `ReserveCapacity(loc, item, n, owner)` return a reservation
    id (0 = refused); `Release(id)`; `CancelOwner(owner)`. `Transfer(from, to, item, n, owner?)` is atomic (false and no change
    on failure) and consumes the matching stock/capacity reservations. `SimWorld.OperationPool(opId, item)` sums the operation's
    own storage modules only. `Audit()` also checks reservations and capacity.
  - **Haul tasks (T-290, WC-21..24/26, WCO-07/08, LED-05, TSK-09):** `HaulSystem` (`Assets/Sim/Operations/`, last in the
    operations block) opens `task.haul_logs` (priority 1, `RequiredResource.Ox`) for each own ground pile holding ≥
    `OxLoadLogs` unreserved logs, or fewer when no standing tree and no unfinished fell task remains. At creation it
    reserves the pile's logs and room in the operation storage nearest the pile (one taking the whole load, else the
    nearest with any room); each tick an Open haul without an ox asks `OxPool.FindFor` and sets the ox Reserved. Steps:
    FetchOx, LeadTo pile, LoadOx, LeadTo storage, UnloadOx, ReturnOx. `DispatchSystem` hands an ox task out only once it
    has an ox, to the idle eligible worker nearest the ox (a worker mid-tree is busy); a haul with no ox waits, so felling
    comes first. `OperationStatus.Reported` gives `WaitingForOx` while a haul waits for an ox; `Of` (which gates felling)
    is unchanged. `HaulSystem` also releases ledger reservations and Reserved oxen of haul tasks cancelled elsewhere.
  - **Early fetch and sunset (T-291, WC-23/35):** `HaulSystem.Early.cs` opens a haul for an Active Fell task whose tree
    yields a full load once `FellMinutesLeft` ≤ the nearest idle worker's walk to the ox + the ox's led walk to the tree
    (ox speed). That haul (`TaskRecord.AwaitsTaskId` = the fell task) reserves ox and storage room at once, waits at the
    pile before LoadOx, and reserves the pile's stock when the fell task is Done (cancelled fell → cancelled haul). At
    the end of work (`WorkDay` window end) a haul still in FetchOx/LeadTo/LoadOx is wound down by the step runner
    (`TaskRecord.WindingDown`; `RoutineSystem` leaves the worker alone meanwhile): the carried logs are unloaded where the
    ox stands after `OxUnloadMinutes` into a new operation-owned ground pile, the ox is led to its rest spot, all the
    task's reservations are released and the task is Cancelled; the worker then walks home. `DispatchSystem` hands out no
    ox task between the end of work and midnight. Next morning `OpenTasks` opens a normal haul for the dropped pile.
  - **Processes (T-272, TMB-01/02, TSK-14, MVP-1-12):** `Assets/Sim/Processes/` (`LordsLegacy.Sim.Processes`) holds
    `ProcessDef` (id, ordered `StageDef`s), `StageDef` (id, minutes, per-item, heavy, enabled, animation key) and
    `ProcessDefs.Timber` (fell 90, delimb 30 off, buck 15 per log). `EffectiveStages(logCount)` returns the enabled
    stages with a disabled stage's time added to the next enabled one (trailing ones to the last enabled), so the total
    is the same either way. `WoodcuttingSystem` turns the effective stages into a fell task's steps and `FellingOutcome`
    matches the fell stage id. Static lookup, like `OperationTypes`.
  - **Start layout (T-253, CMP-01..07):** `StartLayout` (`Assets/Sim/Start/`) is pure data: camp centre/rotation, fire,
    stockpile, ox stand and tent spots relative to the camp (rotated with it), trunk road control points, families with
    citizens, ox count. `StartLayoutApplier.Apply(world, layout)` adds the camp footprint (clearing trees under it), draws
    the trunk road via `world.Execute(DrawRoad)` (throws `InvalidOperationException` with the refusal text if refused),
    adds families (home = tent spot), citizens at home and oxen at the stand. `SimWorld.Camp` (`CampInfo`,
    `SimWorld.Start.cs`) exposes the footprint id and world-space fire, stockpile and stand points; null until applied.
    The oxen count comes from the layout (`OxCount`), not `SimTuning.StartingOxen`. Decision 0027.
    T-287 (CON-08, INV-10, WC-37): `StartLayout.Stock` (`StartStock` list, default 20 `item.log`) is produced into a ledger
    `Stockpile` location at the stockpile point; `SimWorld.Camp.StockpileLocation` exposes it. A stockpile is not a storage
    module, so no operation pool counts it.
  - **Random (T-250, CLK-07):** `SimWorld.Random` (`Assets/Sim/Core/SimRandom.cs`, partial in `SimWorld.Random.cs`) is the
    world's one deterministic source, never null. It is a PCG-XSH-RR 64/32 written in Sim code (no `System.Random`). It is
    seeded from `SimTuning.WorldSeed` (default 12345). `NextInt(min, max)` is max-exclusive and unbiased, `NextDouble()` is in
    [0, 1), `NextUInt()` gives 32 bits. Its whole state is one `ulong` (`State`); a save stores that and calls `Restore`.
    Routine choices draw from it, time alone does not.
- `Assets/Sim/Placement/` (`LordsLegacy.Sim.Placement`, placement.md PL-01..05, PL-07, decision 0028): `PlacementRules.CanPlaceAreaVertex(world,
  areaTypeId, point)` and `CanPlaceRoadPoint(world, point)` answer whether one (already snapped) point may be placed. They
  return a `PlacementResult` (`Ok`, or `Reason` = `InsideArea` with `AreaName`, `OnRoad`, `OutsideMap`; `Text` for the UI).
  Pure queries, no state change. "Inside" means strictly inside: within `BoundaryToleranceMm` (50 mm) of an area edge or
  road side line counts as boundary and is Ok. Work-area vertices are Ok anywhere on the map; road points are refused only
  inside a building area. The tools only ask; shape checks (AR-15, AR-16, RD-13) still run on the finished shape.
- `Assets/Sim/Time/` (namespace `LordsLegacy.Sim.Calendar`, to avoid clashing with `UnityEngine.Time`):
  - `GameCalendar`: tick → `GameTime` (year, date, weekday, time, season). 365-day year, no leap years,
    the year number increments on 1 January.
  - `Daylight`: seasonal sunrise and sunset from min/max daylight hours. Pure, no state. Returns `SunTimes`.
  - Value types: `GameTime`, `Season`, `Weekday`, `SunTimes`.
  - `TickPacer`: owns the selected speed (`SetSpeed`, `CurrentSpeed`, `Speeds`) and turns real seconds into
    whole ticks (`Advance`), carrying the remainder (`Fraction`). Over `MaxTicksPerFrame` it drops the excess
    and sets `SlowedDown`. `Reset()` clears the carry (the host calls it while paused). No pause state (decision 0006).
- `Assets/Sim/Tuning/SimTuning.cs` (`LordsLegacy.Sim.Tuning`) holds the balancing numbers. Read by code today: pace,
  speeds, start date, daylight, walk and ox speed, ageing rate, milling and debug linger, world seed, standard log
  length, navigation cell size and max slope, road sample spacing, road minimum point spacing, road tree
  clearance, the area values (curve flattening, slope grid, overlap tolerance, spatial index cell), and Sunday meal and
  minimum sleep (`WorkDay`, T-277). Defined but read by nothing yet: trunk length range, starting oxen, rest-day switch
  (see `tuning.md`). Food,
  market, construction and population numbers are not in code yet. The Sim never hard-codes these.
- `Assets/Sim/Forestry/` (`LordsLegacy.Sim.Forestry`): `TreeRegistry` holds all trees keyed by `TreeId`, with a
  uniform 16 m spatial grid (`QueryRect`, `QueryRadius`, `LogYield`). `TreeRecord` holds id, x/z, usable trunk
  length, `State` (Standing → Felled → Stump, via `SetState`, forward only, bumps `Version`) and `ReservedBy`.
  `Reserve`/`Release` claim a standing tree for a worker; `Remove` and leaving Standing cancel it.
  `NearestUnreservedStanding(x, z, region)`, `AnyStandingIn`/`CountStandingIn` take a `TreeRegion` (circle or polygon);
  `Felled` and `Stumps` list the other states for the renderer. Trees come from the baked map. `Remove(TreeId)` takes a tree out of
  the records and grid (false if unknown); a removed id can never be added again (BLD-10-D). `Version` increases on
  every Add or Remove, so views rebuild only when it changes. `Count`, `All` and the queries show standing trees only.
  `TreeClearing` (static) removes the trees under a polygon (`RemoveInPolygon`) or within a distance of a polyline
  (`RemoveNearPolyline`), using geo-1 on trunk positions rounded to mm; both return the removed ids sorted.
- `Assets/Sim/Areas/` (`LordsLegacy.Sim.Areas`, decision 0014): `AreaTypeDef`/`AreaTypes` (`area.test` Building,
  `area.test_work` Work; limits per `tuning.md`; M8 adds `area.woodcutting` Building 150–5,000 m² and `area.work_woodcutting`
  Work), `AreaShape` (vertices plus one `AreaEdge` per vertex: Straight,
  Bent with an on-curve handle, FollowsRoad and NearRoad (AR-24), both with segment id and `RoadSide`) and
  `AreaFlattener.Flatten(shape, roads)`,
  which gives a CCW `PointMm` polygon: bent edges are quadratics sampled every `CurveFlattenSpacingM`, road edges copy
  the road side line between the vertices' nearest positions, a missing segment falls back to Straight. NearRoad
  does the same but pushes each side sample outward (away from the road) by a distance interpolated by arc length
  from vertex A's perpendicular distance to vertex B's, so both vertices stay put (`FollowsRoad` = both distances 0;
  helpers in internal `NearRoadGeometry`). `AreaTypeDef.NearRoadReachM` (10) is the vertex reach limit. `NearArea` (AR-25, area id; internal `NearAreaGeometry`) does
  the same along another area's flattened outline (shorter way round); `AreaFlattener.Flatten(shape, roads, areas, spacingM)` takes the registry.
  `AreaTool.ChooseNearRoad` returns Straight when NearRoad/NearArea/FollowsRoad
  don't apply (T-199, AR-33). The flattener guarantees AR-30 for every edge kind: the outline passes through every shape vertex in
  order and each edge's run starts at its vertex and ends at the next. A NearRoad/NearArea edge whose far vertex's nearest guide point
  lies behind the snapped vertex (dot(P-A, B-A) < 0) flattens to the plain straight segment (AR-33). Pinned by `FlattenInvariantTests`.
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
  outline), outside the map (any flattened point). Building kinds then get AR-34 trimming (internal `AreaTrim`, T-205):
  the outline minus every building area it overlaps by more than `OverlapToleranceM2` (`PolygonBoolean.Difference`, pieces under
  the tolerance dropped). Exactly one piece with every shape vertex within 5 cm of it becomes the preview polygon/m² (and the
  stored outline) and `AreaPreview.FittedAround` lists the neighbour names (id order); a split, a hole, or a lost vertex gives
  `OverlapsArea` naming the lowest-id neighbour. T-206 (AR-34 point 5): the same step also subtracts every overlapping road
  corridor (`RoadSegment.CorridorPolygon`); `FittedAround` then ends with `"road"` (once). A split/hole/lost vertex that only
  happens once corridors are subtracted gives `CrossesRoad`; if the areas alone already cause it, `OverlapsArea` wins.
  Work kinds are never trimmed. Then size, and building kinds run `AreaPlacementChecks` (internal):
  slope (AR-14), overlap with another building area (AR-15), road corridor crossing (AR-16). Work kinds stop after size.
  `DesignateArea` and `DeleteArea` are in `Commands/` (see below).
- `Assets/Sim/Geometry/` (`LordsLegacy.Sim.Geometry`, decision 0012): integer-mm `PointMm`, `Segments` (exact
  classify/intersect), `Polygons` (area, winding, `IsSimple`, `Locate`, bounds, `Crossings` = where non-adjacent edges cross or touch, one point per pair, T-201; `Segments.MeetingPoint` gives the point for one pair), `Polylines` (length, nearest,
  min distance, `PointsEvery(line, spacingM)` points at arc length 0, s, 2s… rounded to mm, empty for zero-length
  lines, throws for spacing ≤ 0; `PointsEveryWithin(line, spacingM, centre, radiusM)` the inclusive subset; snap dots, AR-22), `Triangulator.EarClip` (n−2 integer-exact triangles; collinear vertices become zero-area triangles).
  Predicates are exact; distances are double mm.
  `PolygonBoolean.Difference(subject, clips, out holes)` (decision 0021, T-204) subtracts clip polygons from a subject
  via the vendored Clipper2 (`Assets/Sim/ThirdParty/Clipper2/`, pin and compatibility edits in its `VERSION.txt`).
  It returns CCW outer pieces and reports holes (CCW, belonging to the piece that contains them) through `holes`.
  Clipper types stay private; integer mm in and out, with new crossing vertices rounded to the nearest mm.
  `SpatialGrid<T>` (decision 0012, GEO-16) is a uniform-grid index over `BoundsMm` with a cell size in metres
  (rounded to mm, `SimTuning.SpatialIndexCellM` is the value to pass). `Insert(item, bounds)`, `Remove(item)`
  (false if unknown), `QueryRect(bounds)` and `QueryPoint(point)` return each item once, in ascending insertion
  order; edges are inclusive. Items are stored in every cell their bounds touch. Used only by `AreaRegistry` (building-area bounds).
  `PolygonOverlap.Overlaps` is the tolerance overlap test (decision 0012).
- `Assets/Sim/Roads/` (`LordsLegacy.Sim.Roads`, decision 0013):
  - Types: `RoadTypes` (code-built list; `road.dirt_path`: 2.5 m, 15% max grade, 1.25× speed) of `RoadTypeDef`.
    `NavGrid` reads the speed multiplier (MOV-08).
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
    first/last `RoadSegment` (`StartTangent`/`EndTangent`, decision 0029); the old segment is untouched. T, X and 2+-segment nodes get none.
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
- Ox pool (T-288): `Ox.State` (`OxState`: Free, Reserved(task), Led(citizen), Resting(spot, stand id)).
  `OperationRegistry.AssignOx` refuses an ox assigned to any operation (`FindOxOwner`). `OxPool(world)` has
  `FindFor(opId, from)` (assigned first, then nearest free pool ox), stand `Occupancy`/`HasRoom` (module stand holds
  `OxCapacity` = 1; camp stand unlimited) and `RestSpot`/`Rest` (WC-31/32).
- Ox steps and follow movement (T-289, MOV-16, WCO-06): `MovementSystem.Attach(follower, leader, offsetM = 1.5)` /
  `Detach(follower)` (static; the tie is `IMovable.Follow`, a `FollowLink`). While attached the leader moves at
  min(own, follower speed) × its cell multiplier; after all agents have moved the follower is placed `offsetM` behind
  the leader's facing (shrunk to 75/50/25 % then onto the leader if the cell is blocked or off the map) with the
  leader's facing. New `StepKind`s with `TaskStep` factories: `FetchOx` (walk to the ox, Attach, ox `Led`), `LeadTo(point)`
  (the handler's `MoveOrder`), `LoadOx(loc, n, ticks)` / `UnloadOx(loc, n, ticks)` (Work, then `Ledger.Transfer` between the
  location and `world.OxCarrier(ox)`, owner = task id), `ReturnOx` (lead to `OxPool.RestSpot`, Detach, ox `Resting`).
  `TaskRecord.OxId` is set by `TaskBoard.SetOx`. `StepRunnerSystem` (partial, `StepRunnerSystem.Ox.cs`) cancels the task if
  the ox is removed or its operation assignment changed, dropping the carrier's logs as an operation-owned ground pile
  where the ox stood and setting the ox `Free`. Tuning: `OxLoadLogs` 4, `OxLoadMinutes` 10, `OxUnloadMinutes` 10.
- Not built yet: oxen hauling tasks (Haul task creation, reservations, Waiting for ox / Storage full) (M9), construction and odd jobs (M10), food, markets,
  economy, saving. Decision 0005 is implemented for `TaskDef` (MoveTo/Work steps); only `task.fell_tree` is defined so far.

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
- `TerrainDrape` (`Assets/Game/Visuals/`, decision 0020) is the one shared helper for ground visuals: `Point`, `Ribbon`
  (a polyline as a dashed or solid strip, at most 1 m between points) and `Fill` (a polygon as triangles of at most 2 m).
  Each point sits at Sim terrain height + a lift; straight lines stay straight in plan view. `RoadRenderer`,
  `AreaRenderer` and the area/road drafts use it. Any new ground visual must use it.
- `GroundOverlay.Material(texture = null)` (`Assets/Game/Visuals/`, shader `LordsLegacy/GroundOverlay`, decision 0020):
  use it for anything drawn on the terrain (roads, area outlines/fills, drafts, markers). Renderers fall back to
  `Sprites/Default` only if the overlay shader or its material is missing; that is not the normal path.
  Unlit, alpha blended, vertex colour x optional texture, pulls geometry towards the camera by a bias growing with
  view distance so terrain LOD doesn't cover it. Materials are cached; don't destroy them. The shader ships through
  the `Assets/Game/Resources/GroundOverlay.mat` material (loaded with `Resources.Load`).
- `SimHost` (`Assets/Game/Bootstrap/`, MonoBehaviour in the Game scene) owns `SimWorld` and `TickPacer` (decision 0006).
  It has a serialized `SimConfig` (`Assets/Game/Config/SimConfig.cs`, asset `SimConfig.asset`, decision 0019): when set,
  `Start` builds `SimTuning` and the road/area catalogs from it once (edits apply on the next Play); when null it uses
  the code defaults. Unity code reads types from `World.RoadTypes`/`World.AreaTypes`, never the static lists.
  `LordsLegacy/Config/Create default SimConfig` creates the asset (idempotent, never overwrites).
  `Initialize(SimTuning, MapData)` (catalogs from the config) or `Initialize(SimTuning, MapData, RoadTypeCatalog, AreaTypeCatalog)`; if not initialised, `Start` reads `StreamingAssets/Maps/testmap.lsmap` (`TryLoadMap`
  logs an error naming the path and the bake menu item if missing). `Update` calls `Tick(Time.deltaTime)` when
  `AutoUpdate`. `Tick` runs `World.Step(Pacer.Advance(..))`, or resets the pacer while paused. Views read
  `World`, `Pacer`, `TickFraction` and `Stats`. Views send commands only through `Execute(ISimCommand)`,
  `TogglePause` and `TickPacer.SetSpeed`.
- Start layout (T-261): `StartLayoutConfig` (`Assets/Game/Config/`, ScriptableObject) converts to the Sim `StartLayout` (`ToLayout()`).
  `SimHost` has a serialized `_startLayout`; `Initialize` applies it with `StartLayoutApplier` after building the world (null = no camp).
  Menu `LordsLegacy/Map/Generate Start Layout` (`StartLayoutGenerator`) writes `Assets/Maps/TestMap/StartLayout.asset`
  (flat spot at most 5 degrees west of the forest edge, 50+ trees within 50 m, north-south trunk road past the camp; idempotent).
  `GameSceneGenerator.CreateSimHost` wires the asset into SimHost and fails if the asset is missing.
- Operation panel and selection order (T-281, `Assets/Game/UI/OperationPanel/`): `AgentPicker` has one pick order with no tool
  active: citizen/ox within the pick radius, else a module under the cursor, else the building area, else a linked work area
  (`TryPickOperation`). Modules and areas select an *operation* (`HasOperation`/`SelectedOperationId`), which opens
  `OperationPanelView` (UIDocument built in code). It shows status cause, modules, 2 family slots (Assign list of unassigned
  families, Remove), worker tasks, ground logs, `CountStandingIn` trees, Pause/Resume, Draw work area
  (`ToolController.BeginWorkAreaDraw` → `AreaTool.WorkAreaFor`, confirm sends `SetWorkArea`) and Use default. Oxen tab and
  stock rows are disabled ("Coming in M9"). All actions go through `SimHost.Execute`; refusals show their text.
- Picking and citizen panel (T-263, `Assets/Game/Picking/`, `Assets/Game/UI/CitizenPanel/`): `AgentPicker` turns a left click
  (no tool active, not over UI) into a selected citizen or ox by screen-space distance to the interpolated positions; it only
  reads the Sim. `SelectionRing` marks the selection. `CitizenPanelView` (a UIDocument built in code, no UXML) shows name,
  age, family, activity and position of the selected citizen, read-only, and calls `UiFocusRules.Apply`.
- `SimStats` (`LordsLegacy.Game`) is the only Unity-side measurement: `LastStepMs` and `TicksPerSecond`
  (ticks in the last real second). Sim code never reads it.
- Terrain: Unity Terrain built from `TestMapTerrain.asset` is drawn by Unity and is the collider for picking.
  It is not built from Sim data.
- Trees (`Assets/Game/Visuals/TreeRenderer`): draws every standing tree in `World.Trees` with the
  `visual.tree.generic` definition. Trees are grouped in 64 m cells, frustum-culled, and drawn with
  `Graphics.RenderMeshInstanced` in batches of 1023. Cells are rebuilt only when `TreeRegistry.Version` changes.
  There are no per-tree GameObjects and no colliders (decision 0008).
- People and camp (T-262): `PeopleRenderer` draws every citizen (adult/child by `AgeYears` < 16) and ox with
  `RenderMeshInstanced` (no GameObjects, no colliders), lerping `PreviousX/Z`→`X/Z` by `SimHost.TickFraction` (current position
  while paused), height from Sim terrain, yaw from `FacingRad`. Placeholder walk bob (`WalkBob`): steps per real second =
  metres walked per tick × game minutes per real second × `ViewSettings.WalkStepsPerMetre`; none when still or paused.
  `CampRenderer` draws tents (family homes), fire, log pile and tie post from `World.Camp`, rebuilding when
  `FootprintRegistry.Version`, the camp or the family count changes. Visuals `visual.citizen.adult|child`, `visual.ox`,
  `visual.camp.tent|fire|logpile|tiepost` are written by `PlaceholderVisualGenerator`; box sizes are in `VisualSizes`.
- Woodcutting visuals (T-280): `ModuleRenderer` draws placed modules instanced from `visual.module.woodcutting_hut|log_storage|oxen_stand`
  stretched to the footprint (rebuilt on `FootprintRegistry.Version`). `TreeRenderer` also draws `visual.tree.stump` and
  `visual.tree.felled` (lying trunk) for `TreeRegistry.Stumps`/`Felled` (rebuilt on `TreeRegistry.Version`); they are not standing trees.
  `LogPileRenderer` draws one `visual.item.log` per log in each ground pile, stacked 4 per layer (rebuilt on `Ledger.Version`);
  logs in storage modules are not drawn. All are written by `PlaceholderVisualGenerator`; no colliders.
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
  - `RoadTool` (id `road`, registers itself in `OnEnable`) holds view state only: the draft points and any node drag. It calls
    `RoadValidator` for the preview and `RoadValidator.PreviewMove` for a node drag, `PlacementRules.CanPlaceRoadPoint` (asked
    after snapping), and `SimHost.Execute` for `DrawRoad`, `DeleteRoadSegment` and `MoveRoadNode` (left-drag a confirmed node,
    RD-17). Holds no road rules. Rules: roads.md; input: ui.md.
  - `Tools/Shared/`: `PressTracker` (click, drag, double-click; 4 px drag threshold, 6 px double-click radius; time passed
    in), `SnapPicker` (best candidate by kind order within a pixel limit), `SnapMarkers` (`SnapMarkerBuilder`: corner, road-side dot and area-edge dot markers, AR-22), `ToolHitTester` (draft point hits).
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
  - `ModuleTool` (id `module`, T-279, WC-03/04): holds the operation id and a queue of module types. A ghost (`TerrainDrape.Fill`,
    green/red) follows the cursor; Q/E rotate 15° (Shift 1°). With `ViewSettings.Snapping` on, `Tools/Shared/ModuleSnap` (pure,
    presentation only, decision 0017) snaps a side flush to an area edge within `SnapDistancePx` and turns to it (3 mm inset).
    `ModulePlacement.Preview` judges the ghost (red + refusal text in the panel); click or Confirm sends `PlaceModule`; right-click on a
    module of the operation sends `RemoveModule`. Panel buttons (`ToolPanelView.SetActions`/`ActionClicked`): Add hut (only if none),
    Add log storage, Add oxen stand. The tool stays active after the queue empties; Esc/Cancel ends it.
- Build bar (`BuildBarView`, `Assets/Game/UI/BuildBar/`, UXML/USS): non-focusable buttons built from `ToolEntry.Defaults()`
  (Road, Test area, Test work area and Woodcutter enabled; the rest greyed, tooltip "Coming later"). Raises `ToolClicked`.
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
  Read-only; one named label per value (`debug-*`, `debug-road-*`, `debug-seg-*`). T-264 adds `debug-citizen-count` and the
  nav cell under the cursor (`debug-nav-cell`, `debug-nav-blocked`, `debug-nav-mult`, from `World.Nav`).
- Debug send-to (T-264, PPL-33, `Assets/Game/Debugging/`): `DebugSendController` (Editor and development builds only,
  `DebugAvailable`): right click on the ground with a citizen selected and no tool active sends `SendCitizenTo` via
  `SimHost.Execute`; a refusal shows in `CitizenPanelView` (`citizen-message`, cleared on selection change).
  `DebugPathOverlay` drapes the selected citizen's remaining path (`TerrainDrape.Ribbon`, `GroundOverlay`). Both are
  created by `GameSceneGenerator.CreateAgentSelection`.
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

Start layout (camp and people): `SimHost.Initialize` builds the world from the map, then applies `StartLayoutConfig.ToLayout()`
through `StartLayoutApplier` (camp footprint, trunk road via `Execute(DrawRoad)`, families, citizens, oxen). Each tick
`SimSystems` runs its phases in order: nav sync, routine, dispatch, steps, movement (incl. debug send), operations.
Felling (M8): `WoodcuttingSystem` opens a `task.fell_tree` per idle worker → `DispatchSystem` assigns it → `StepRunnerSystem`
walks to the tree and runs each timber stage → `FellingOutcome` sets the tree Felled, then Stump, and drops the logs in an
operation-owned ground pile (`LogPileRenderer` draws them). Modules: `ModuleTool` → `ModulePlacement.Preview` (ghost) →
`PlaceModule` through `SimHost.Execute`; built at once (WC-07). `PeopleRenderer` and `CampRenderer` read `World.People` and
`World.Camp` and lerp by `TickFraction`. A click goes `AgentPicker` → selection → `CitizenPanelView`; a debug right click
goes `DebugSendController` → `SimHost.Execute(SendCitizenTo)` → `Citizen.DebugSend` → `RoutineSystem` skips it, `MovementSystem` walks it.

## Scene

_Owner: the scene and prefab tasks. Other tasks do not edit this section._

- M7 pieces (T-262..T-265, all wired in code by `GameSceneGenerator`): `SimHost._startLayout` -> `Assets/Maps/TestMap/StartLayout.asset`;
  `PeopleRenderer` and `CampRenderer` (`CreatePeopleRenderers`, -> SimHost, `VisualRegistry.asset`); `CreateAgentSelection` adds
  `AgentPicker` (-> SimHost, picker, ToolController), `SelectionRing`, UIDocument `CitizenPanel` (`CitizenPanelView`, no UXML),
  `DebugPathOverlay` and `DebugSendController`.
- M8 pieces (T-284, wired by `GameSceneGenerator`): `CreateAreaTools` adds `ModuleTool` (-> SimHost, picker, ToolPanel, TopBar,
  ToolController) next to the four `AreaTool`s (test, test-work, woodcutter, woodcutter-work); `CreatePeopleRenderers` adds
  `ModuleRenderer` and `LogPileRenderer` (-> SimHost, `VisualRegistry.asset`); `CreateAgentSelection` adds UIDocument
  `OperationPanel` (`OperationPanelView`, built in code, -> SimHost, AgentPicker, ToolController). The work area overlay
  (`WorkAreaRenderer`) is added to that object by `OperationPanelView.OnEnable`, so it is not a scene object of its own.
  Sim side: Operations, Tasks, Inventory and Processes are described under Simulation core; Unity side under Unity layer.
  `GameSceneTests.GameSceneHasM8WoodcuttingWired` checks these pieces and their references.
- `Assets/Scenes/Game.unity` is written by `GameSceneGenerator` (Assets/Editor/Scenes). **To regenerate the scene run the
  menu item `LordsLegacy/Scenes/Generate Game scene`**, then commit the result. It rebuilds from an empty scene each run,
  and `SceneYamlNormalizer` gives its objects stable fileIDs, so two runs give the same file. Serialized references are wired in code.
- Contents: `Terrain`; `SimHost` (`_config` -> `Assets/Game/Config/SimConfig.asset`, wired by `GameSceneGenerator.CreateSimHost`); `Main Camera` (Camera + `CameraRig`); `Directional Light` (Light + `SunLightController`
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
  `GameSceneTests.GameSceneHasM7PeopleWired` checks SimHost.StartLayout, PeopleRenderer, CampRenderer, CitizenPanelView (+ UIDocument),
  AgentPicker, DebugSendController and that citizens exist after start (T-265).
