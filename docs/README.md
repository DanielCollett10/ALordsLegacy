# Documentation index

Start with [`architecture.md`](architecture.md) for the big picture and [`../CLAUDE.md`](../CLAUDE.md) for the rules.

- [`architecture.md`](architecture.md): layers, data flow, patterns and rules.
- [`design/`](design/README.md): the designer's rules and why (owned by the human; agents read it).
- [`decisions/`](decisions/): short records of technical decisions, `NNNN-title.md`. Read before re-arguing one.
- [`milestones/`](milestones/): what each finished milestone shipped and what it left open.
- [`test-runs/`](test-runs/): recorded results of full suite runs for a milestone.
- [`unity-mcp.md`](unity-mcp.md): how agents drive the Unity Editor.

## Systems

One line per system. The doc says how it fits together; folders are where the code lives.

| System | Purpose | Doc | Folders |
|---|---|---|---|
| Sim core | Root `SimWorld`, fixed system order, seeded random, balancing numbers. | [systems/core.md](systems/core.md) | `Assets/Sim/Core`, `Assets/Sim/Tuning` |
| Time | Ticks to game calendar, sunrise and sunset, real-time pacing. | [systems/time.md](systems/time.md) | `Assets/Sim/Time` |
| Map and terrain | Baked terrain and tree data the Sim reads; the bake pipeline. | [systems/map-and-terrain.md](systems/map-and-terrain.md) | `Assets/Sim/Terrain`, `Assets/Sim/Map`, `Assets/StreamingAssets/Maps` |
| Geometry | Exact integer-mm polygons, polylines, boolean ops, spatial index. | [systems/geometry.md](systems/geometry.md) | `Assets/Sim/Geometry`, `Assets/Sim/ThirdParty` |
| Commands | The only way to change Sim state: validate, apply, log. | [systems/commands.md](systems/commands.md) | `Assets/Sim/Commands` |
| Roads | Freeform road network, validation, cutting and node moves. | [systems/roads.md](systems/roads.md) | `Assets/Sim/Roads` |
| Areas | Drawn building and work areas, flattening, trimming, snapping. | [systems/areas.md](systems/areas.md) | `Assets/Sim/Areas` |
| Growth | Families walk out after 30 hungry days and walk in when a house and a month of food allow; passage state, settlement log, panel read model. | [systems/growth.md](systems/growth.md) | `Assets/Sim/Growth`, `Assets/Sim/Arrivals`, `Assets/Sim/Roads/MapExits.cs` |
| Placement | Pure yes or no for placing an area vertex, a road point or a standalone structure (granary, hewing station, sawpit). | [systems/placement.md](systems/placement.md) | `Assets/Sim/Placement` |
| Forestry | Standing trees, felling state, reservations, clearing. | [systems/forestry.md](systems/forestry.md) | `Assets/Sim/Forestry` |
| Navigation | Walkable grid, A* pathfinding, movement orders, ox following. | [systems/navigation.md](systems/navigation.md) | `Assets/Sim/Navigation` |
| People | Families, citizens, oxen, daily routine, working day, debug send. | [systems/people.md](systems/people.md) | `Assets/Sim/People` |
| Tasks | Work units with steps and states; dispatch and step running. | [systems/tasks.md](systems/tasks.md) | `Assets/Sim/Tasks` |
| Operations | Workplaces (woodcutting, foraging camp, granary): modules, families, felling, hauling, status. | [systems/operations.md](systems/operations.md) | `Assets/Sim/Operations`, `Assets/Sim/Processes` |
| Foraging | Forage bushes, foragers aged 8+ picking berries and mushrooms into their own hut store. | [systems/foraging.md](systems/foraging.md) | `Assets/Sim/Foraging`, `Assets/Sim/Core/SimWorld.Foraging.cs` |
| Food | Larders, the Sunday allotment and fetch trip, daily eating and Hungry, granary food. | [systems/food.md](systems/food.md) | `Assets/Sim/Food` |
| Markets | Market area, stalls, granary families stocking stalls, camp handover and removal, Sunday allotment from stalls. | [systems/markets.md](systems/markets.md) | `Assets/Sim/Markets`, `Assets/Sim/Geometry/StallGrid.cs`, `Assets/Game/Tools/MarketTool.cs` |
| Workshops | Hewing station and sawpit: one data-driven system, stock and store, status, panel read model, village timber totals. | [systems/workshops.md](systems/workshops.md) | `Assets/Sim/Operations/Workshops`, `Assets/Sim/Inventory/GoodsStats.cs` |
| Footprints | Placed buildings, stands and standalone structures: rectangles, access points, recipes. | [systems/footprints.md](systems/footprints.md) | `Assets/Sim/Footprints` |
| Inventory | Ledger of items (logs, food, planks, beams) by location, with reservations, journals and audit. | [systems/inventory.md](systems/inventory.md) | `Assets/Sim/Inventory` |
| Construction | Sites that reserve every recipe item, take logs by ox and planks by hand, and build over time. | [systems/construction.md](systems/construction.md) | `Assets/Sim/Construction`, `Assets/Sim/Operations/ConstructionSystem.cs` |
| Homes | House plots along roads, a house built from logs, one homeless family moves in and out. | [systems/homes.md](systems/homes.md) | `Assets/Sim/Homes`, `Assets/Sim/Commands/DesignatePlot.cs`, `Assets/Sim/Areas/HousePlacement.cs` |
| Start layout | The first camp, built through the normal commands. | [systems/start-layout.md](systems/start-layout.md) | `Assets/Sim/Start`, `Assets/Game/Config` |
| Save and load | Byte-exact save sections, map check, Esc menu, autosave, scene reload. | [systems/save.md](systems/save.md) | `Assets/Sim/Save`, `Assets/Game/Saving`, `Assets/Game/UI/GameMenu` |
| Unity host | `SimHost` owns the world and pacer; SimConfig and start-layout assets. | [systems/unity-host.md](systems/unity-host.md) | `Assets/Game/Bootstrap`, `Assets/Game/Config` |
| Unity visuals | Instanced drawing of Sim state, ground draping, art by id, sun light. | [systems/unity-visuals.md](systems/unity-visuals.md) | `Assets/Game/Visuals`, `Assets/Game/Lighting` |
| Unity tools and input | Build tools, snapping, picking, camera, debug send. | [systems/unity-tools.md](systems/unity-tools.md) | `Assets/Game/Tools`, `Assets/Game/Picking`, `Assets/Game/Camera`, `Assets/Game/Debugging` |
| Unity UI | UI Toolkit panels: top bar, message log, build bar, operation and citizen panels, debug overlay. | [systems/unity-ui.md](systems/unity-ui.md) | `Assets/Game/UI` |
| Editor tools | Menus that bake, generate and wire assets and scenes. | [systems/editor-tools.md](systems/editor-tools.md) | `Assets/Editor` |

Tests: Sim tests in `Sim.Tests/` (`dotnet test Sim.Tests/Sim.Tests.csproj`). Unity tests in `Assets/Tests/EditMode` and
`Assets/Tests/PlayMode` (editor lane only). Each system doc lists its tests.
