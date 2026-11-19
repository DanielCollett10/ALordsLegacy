# Architecture

The big picture: layers, data flow, patterns and rules. Per-system detail (types, files, tests,
gotchas) is in `docs/systems/`; the index is `docs/README.md`. Read `CLAUDE.md` first.

## Layers

| Layer | Folder | Assembly / namespace | Depends on | Tests |
|---|---|---|---|---|
| Simulation core | `Assets/Sim/` | `LordsLegacy.Sim` (`noEngineReferences`) | nothing | `Sim.Tests/` via `dotnet test` (compiles `Assets/Sim/**`) |
| Unity layer | `Assets/Game/` | `LordsLegacy.Game` | Sim, UnityEngine, Input System | `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` (editor lane, decision 0007) |
| Editor tooling | `Assets/Editor/` | `LordsLegacy.Editor`, `LordsLegacy.EditorTools` | Sim, Game, UnityEditor | by hand, editor lane |

The Sim is plain C#, deterministic and tick-based, and is the only source of game state and rules.
The Unity layer reads Sim state, draws it, and turns player input into Sim commands. It holds no rules.
This split lets the 2D view be swapped for 3D without touching the Sim.

## Simulation core

- **Root:** `SimWorld` (`Assets/Sim/Core/`) owns all state: the tick clock, pause, terrain, trees, roads,
  areas, footprints, operations, people, tasks, ledger, construction, nav grid, random and the command log.
  It is `partial`: each subsystem adds its own `SimWorld.<Area>.cs`.
- **Time:** one tick = one game minute. Only `Step(n)` advances `Tick`. Nothing reads real time.
- **Systems:** per-tick logic lives in `ISimSystem` implementations, run in one fixed phase order
  (`SimSystems.CreateDefault()`): **sync → routine → dispatch → steps → movement → operations**.
  Systems also get `OnCommandApplied` after an accepted command.
- **Commands:** every change from outside goes through `world.Execute(ISimCommand)`. It validates first
  (validation never mutates), applies only if accepted, works while paused, never changes `Tick`, and is
  logged. A rejected command changes nothing. A change with several parts is one batch command that validates
  all parts first (`DesignateOperation`, `PlaceModules`, decision 0037).
- **Homes** (`systems/homes.md`): `DesignatePlot` makes the plot, house site and an Empty `HomeRegistry` record in one Apply; `MoveInSystem` (operations phase) moves Homeless families into Built houses (HM-11). Saved by `HomesSection`. `HouseReport` (house panel) and `HouseholdLevelCounts` (top bar, via `GrowthReport.Levels`) are read only. `HouseRemoval` evicts the family when a house is marked and removes home and plot when it goes (HM-14, HM-14b; decision 0040).
- **Food and foraging** (`systems/food.md`, `systems/foraging.md`): food is ledger stock in larders and food stores. The
  Sunday allotment is a reservation, and the fetch trip is a task (decision 0042). Foragers deliver only to their own hut
  store (decisions 0043, narrowed by 0044). Routine phase: eating, `DepartureSystem`, `ArrivalSystem` (dawn: new family walks in with a month of food and moves in, decision 0045), the Sunday allotment and the Sunday trip. `GrowthReport` (read only) gives panels family status text, village counts and the current arrival reason; see systems/growth.md. Operations phase: bush regrowth and foraging.
- **Markets** (`systems/markets.md`): one market area holds stall slots. Granary families carry food from forager huts into
  the granary and from the granary to stalls (decision 0044). A stall's planks are carried by its granary family (T-518). The camp hands its food over once a granary supplies the market,
  then is removed. Sunday households draw from the camp before the handover and from stalls in range after it, never both (decision 0049).
- **Workshops** (`systems/workshops.md`, decisions 0046, 0047): hewing station, sawpit, weaver and wattle yard are data (`WorkshopDef`, a recipe of one or more inputs, decision 0051) run by one `WorkshopSystem`. `WorkshopReport` (panel text, stock, store, family) and `GoodsStats` (village Logs, Beams, Planks; reserved counts, workshop stock and ox loads do not) are read only. `StorageYardReport` (T-547) is the read-only yard panel model: per-item stock, families, computed ST-45 status and the ST-10 market text; its village totals come from `GoodsStats.GeneralByItem`.
- **M19 baskets** (`systems/coppice.md`, `systems/kit.md`, decision 0052): coppice hut rods go by hand to the weaver and wattle yard; baskets go to forager hut and granary kit stores, and a borrowed basket doubles that trip's carry (`KitFetchSystem`, `KitLoans`). Panels reach the yard. `Scenarios/BasketChainScenarioTests` proves the chain from the start layout.
- **Thatch** (`systems/thatch.md`): reed beds are bush kinds. `ThatchingSystem` cuts them into stooks, `CuringSystem` dries
  the stooks, and `SheafCollecting` carries the dry sheaves to the thatcher's hut store. Houses draw that thatch plus yard panels (end-to-end test: `Scenarios/ThatchChainScenarioTests`).
- **Storage yard** (`systems/storage.md`, decisions 0054, 0055): a General goods store. Yard families plan hand and ox trips
  from an ordered list of `IYardJobSource`s. The workshop input reserve (`InputReserve`) gates fetches by storage total.
- **Passage and settlement log** (decision 0045): `Family.Passage` (None, Arriving, Leaving) marks families walking in or out;
  readers check `IsInPassage`. `SettlementLog` on `SimWorld` keeps the last 20 (tick, text) entries, saved in the Core section.
- **Two-step removal:** a built module is marked, then a task takes it down over time (decision 0039). Unbuilt things are removed at once.
- **Queries:** tools and views ask pure queries before committing (`RoadValidator.Validate`,
  `AreaValidator.Validate`, `ModulePlacement.Preview`, `PlacementRules`). They return previews and refusal
  reasons, never state.
- **Versions:** registries bump a `Version` counter on change. Views rebuild only when it moves.
- **Stable ids:** ints for trees, roads, areas, footprints, operations, families and tasks. Never reused.
- **Data, not code:** task defs, process defs, operation types, footprint types and road/area type
  catalogs are data. Visual ids are strings the Sim stores and the Unity layer resolves.
- **Derived, not stored:** operation status (`OperationStatus.Of`) is computed each time.
- **Items move only through the ledger:** `Produce`, `Transfer`, `ReserveStock`/`ReserveCapacity`, `Consume`.
  Each keeps journals, and `Audit()` checks that the sums agree.
- **Determinism:** no static mutable state, no `DateTime.Now`, no `System.Random`. `SimRandom` is a seeded
  PCG in Sim code. Geometry is integer millimetres (`PointMm`), with distances as doubles.
- **Tuning:** every balancing number is in `SimTuning`. Sim code should never hard-code one. Known exceptions: module recipes (`ModuleTypes`) and the weaver and wattle recipes (`WorkshopDef`), tracked as M19 and M20 debt. The save stores the
  tuning the world was created with.

## Data flow

Startup: `SimHost.Start` reads `StreamingAssets/Maps/testmap.lsmap` → `MapDataIO.Read` →
`SimWorld.FromMapData(map, tuning, catalogs)` → `StartLayoutApplier` applies the camp, trunk road, families, oxen and
starting meat (via `Execute`). A new game also scatters forage bushes (`BushScatter`); a load replaces them from the save.

Each frame: `TickPacer.Advance(real seconds)` → `SimWorld.Step(n)` → systems run each tick → views read state.

Input: a tool or picker builds an `ISimCommand` → `SimHost.Execute` → `SimWorld.Execute` → `CommandResult`
(Accepted with created/removed ids, or Rejected with a code and text) → the view shows the text on refusal.

Drawing: renderers read `World` and lerp positions by `SimHost.TickFraction` (current position while paused).
They never write Sim state.

Save/load: `SimSave.Write` / `SimSave.Read` (sections, decision 0034). Load sets `PendingLoad` and reloads the
Game scene, so renderers always start from a fresh world.

Example, drawing a road: `RoadTool` previews with `RoadValidator`, confirm sends `DrawRoad`, the Sim splits
segments and clears trees, `RoadRenderer` sees the new `RoadNetwork.Version` and builds the new segments.

## Unity layer

- **Host:** `SimHost` (`Assets/Game/Bootstrap/`) owns the `SimWorld` and `TickPacer`. It builds the world once
  from `SimConfig` (optional asset, decision 0019) and the start layout asset. It is the only place views
  reach the Sim (`Execute`, `TogglePause`, `SetSpeed`).
- **Visuals:** instanced drawing (`Graphics.RenderMeshInstanced`, batches of 1023), no per-agent or per-tree
  GameObjects or colliders (decision 0008). Roads and areas use one mesh per segment or area (decision 0016).
  Ground visuals drape on Sim terrain through `TerrainDrape` and `GroundOverlay` (decision 0020).
- **Art by id:** the Sim stores a visual id; `VisualRegistry` maps it to a `VisualDefinition`. Placeholder art
  is generated by an editor menu (decision 0009). Agents never make final models: every new placeholder id gets
  a row in `design/placeholder-art.md` in the same task.
- **UI:** UI Toolkit only (`.uxml`/`.uss` or panels built in code). Every game panel calls `UiFocusRules.Apply`, so
  game UI never takes keyboard focus (UIK-01/02).
- **Tools:** `ToolController` keeps one active tool. Tools hold draft and view state only, and call the Sim for rules.
- **Presentation-only settings:** snapping and `ViewSettings` are read by tools and views, never by the Sim (decision 0017).
- **Terrain reads:** heights and slopes come from the Sim terrain, never from the Unity Terrain collider.

## Map pipeline

Authoring assets (terrain, tree data, start layout) → editor bake menu → `Assets/StreamingAssets/Maps/testmap.lsmap`
(committed binary) → runtime reads the bytes and `MapDataIO.Read` parses them (decision 0003). The Sim never asks Unity
for terrain. Re-baking after any terrain or tree change is a manual menu step, and the new file is committed. A save
records the map hash and is refused if it does not match (SV-30).

## Scenes and generated assets

Scenes, prefabs and placeholder assets are written by editor scripts, not hand-edited (decision 0010):
`GameSceneGenerator` (`LordsLegacy/Scenes/Generate Game scene`) rebuilds `Assets/Scenes/Game.unity` each run and wires
serialized references in code. `AgentFriendlyProjectSettings` forces text serialization and visible `.meta` files on
editor load. Each `GameSceneTests.GameSceneHas...` PlayMode test checks the wiring for a milestone.

## Rules for every change (from CLAUDE.md)

- C# 9. Namespaces `LordsLegacy.Sim.*` in Sim, `LordsLegacy.Game.*` in Unity. No file-scoped namespaces, no global usings.
- Sim code has no static mutable state, no `DateTime.Now`, and no unseeded randomness.
- Every new Sim behaviour gets an NUnit test in `Sim.Tests/`.
- New Sim state goes into a save section in the same task, covered by `SimSaveRoundTripTests` (decision 0034).
- Small files and classes. Public API has XML doc comments.
- Commit messages start with the task ID. Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`, `UserSettings/`.
- Record a decision that future agents should not re-argue in `docs/decisions/NNNN-title.md` (10–20 lines).
- When you change how things fit together, update this file and the affected `docs/systems/` file.

## Not built yet

The Supplies market on the storage yard (ST-10) is a greyed placeholder: no household buys General goods yet (`systems/storage.md`). Also not built: tier 2 houses that use beams (PL-11b); towns and several markets (MKT-01, MKT-07); spoilage (FOOD-A11); extra houses
and backyards; grown grain from fields (MAT-31) and carts (MAT-35); artisan build steps (PPL-42); a walk-to-site step for builders; clearing trees as a job (BLD-10-C). The design
status is in `docs/design/`, and open gaps per milestone are in `docs/milestones/`.
