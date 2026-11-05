# Technical Spec — Medieval Settlement & Regional Economy Simulation

Nov 5, 2026 · @Dan

## 0. Document control

This spec defines how the game is built; the [GDD](https://claude.ai/artifact/XvrnyAUBG52z34Jy4V7E7N) defines what it is and how it plays. Where they conflict, the GDD wins on gameplay and this spec wins on implementation.

**Status:** v0.2, 5 Nov 2026. Ready for lead and reviewer assessment in Agent NOC. Sections 6–9 (tasks, inventory, logistics, area geometry) are the priority for MVP-0.5 and MVP-1; prototypes P3 and P4 come before MVP-0.5.

- Uses the GDD's tags (DECIDED, PROPOSED, PLACEHOLDER, OPEN) with the same agent freedoms.
- Rules reference GDD IDs where they implement them (e.g. implements `WCO-06`).
- ID prefixes: `ENG` engine · `ARC` architecture · `CLK` clock · `DEF` data definitions · `CMD` commands · `TSK` tasks · `LED` inventory ledger · `L`GS logistics system · `GEO` area geometry · `NAV` roads and navigation · `PRS` presentation · `SAV` saving · `PRF` performance · `DBG` debug tools · DEV workflow and testing · UI interface · PLT platform and content.
- Code structure detail (namespaces, assemblies, naming) is left to the lead agent, within these rules.

## 1. Engine and packages

Unity 6, newest LTS at project start, with ECS for the simulation and GameObjects for UI, camera, tools and close-up characters.

| ID | Rule | Tag |
| --- | --- | --- |
| ENG-01 | Use the newest Unity 6 LTS release available at project kickoff, and pin it. Upgrades only at milestone boundaries | DECIDED (exact version OPEN) |
| ENG-02 | Entities, Collections, Mathematics, Burst and Jobs for the simulation core; Entities Graphics for large instanced sets (trees, distant characters) | DECIDED |
| ENG-03 | GameObjects (MonoBehaviours) for UI, camera, input, drawing tools and near-camera animated characters | DECIDED |
| ENG-04 | Universal Render Pipeline (URP), for broad PC performance and compatibility with crowd animation tools | PROPOSED |
| ENG-05 | Unity Splines package for roads; Unity Terrain for the MVP map | PROPOSED |
| ENG-06 | A polygon clipping library (e.g. Clipper2) for area overlap, offset and clipping | PROPOSED |
| ENG-07 | Crowd animation: a GPU baked-animation solution, bought or built, chosen after prototype P2 (section 13) | OPEN |
| ENG-08 | Third-party packages need lead approval and a licence check before use | DECIDED |

## 2. Layer architecture

The game has four layers, and data flows one way: player input becomes commands, commands change the simulation, and presentation only reads the simulation.

&#91;embedded content: layer architecture · one-way data flow\]

Presentation never writes back: anything the player changes goes through a command.

| ID | Rule | Tag |
| --- | --- | --- |
| ARC-01 | **Definitions** layer: read-only data for buildings, items, recipes, stages, needs (section 4) | DECIDED |
| ARC-02 | **Simulation** layer: all game state in ECS (citizens, families, oxen, trees, inventories, tasks, buildings, areas, roads). The only source of truth (GDD PRIN-04) | DECIDED |
| ARC-03 | **Commands** layer: every player action is a command object applied by the simulation on its next tick (section 5). Tools and UI never write simulation state directly | DECIDED |
| ARC-04 | **Presentation** layer: models, animation, stockpile visuals, overlays and UI read simulation state and never change it | DECIDED |
| ARC-05 | Every simulation object has a stable ID that survives save and load; cross-references use these IDs, not engine object references | DECIDED |
| ARC-06 | Each village is a scope within the simulation (its stores, task board, treasury), so regional and multi-village features add scopes rather than new systems. ARC-07: physical goods and labour are simulated step by step (felling, hauling, building, repairs); finances, seed, daily food consumption, crop yield and condition decay are batch calculations on their update rate (CLK-03), while workers still visibly do the related work in the world (PROPOSED) | DECIDED |

## 3. Simulation clock and time-slicing

The simulation runs on a fixed game-time tick, independent of frame rate; game speed only changes how many ticks run per real second.

| ID | Rule | Tag |
| --- | --- | --- |
| CLK-01 | Fixed tick of 1 game-minute. At 1× (45 real minutes per game day, GDD TIME-02) that is about 0.5 ticks per real second; at 16× about 8.5 | PROPOSED (tick length PLACEHOLDER) |
| CLK-02 | Movement is interpolated between ticks for smooth visuals | DECIDED |
| CLK-03 | Systems declare an update rate in game time: per tick (movement, work progress); hourly (dispatch, logistics passes); daily (food consumption, critical needs, family formation, ageing, births); weekly (food spoilage, building condition, routine stock checks); seasonal (planting, harvest, crop yield); yearly (production, reserve and population summaries) | DECIDED |
| CLK-04 | Heavy periodic work is spread across ticks by ID (time-slicing), so no single tick does all of it | DECIDED |
| CLK-05 | If a frame cannot run all due ticks at high speed, the game slows itself rather than skipping simulation steps, and shows that it has done so | PROPOSED |
| CLK-06 | Pause stops ticks entirely; player commands can still be issued and apply on the next tick | DECIDED |
| CLK-07 | Same-machine reproducibility: the same save plus the same commands gives the same result on the same machine. Seeded randomness only, a fixed tick, a fixed system order, no frame-time-dependent logic; normal floating-point maths is allowed. Strict cross-machine determinism (fixed-point maths) is not required, because any future multiplayer is host-authoritative: player 1's PC runs the only simulation, checks other players' commands and sends them the resulting state. Lockstep multiplayer (every PC simulating) is ruled out | DECIDED |

## 4. Data definitions

Designers edit definitions as ScriptableObjects in the Unity editor; at load they become read-only runtime data (blob assets) the simulation reads.

| ID | Rule | Tag |
| --- | --- | --- |
| DEF-01 | Definition types: Item, Building, Module, Area type, Process (stage list), Need, Household level and its house tier (with renovation recipe), Building component (material, quality, decay rate), Food storage profile (spoilage by store type), Crop, Expansion, Visual | DECIDED |
| DEF-02 | Every definition has a stable string ID (e.g. `item.log`, `building.woodcutting_hut`) used in saves and references | DECIDED |
| DEF-03 | Every PLACEHOLDER number in the GDD lives in a definition or a tuning file, never in code | DECIDED |
| DEF-04 | Processes (felling, construction, production) are ordered stage lists with enable flags (GDD PRIN-05, TMB-01) | DECIDED |
| DEF-05 | An editor validator checks definitions on save: missing references, empty recipes, stages with no duration, visuals missing | DECIDED |
| DEF-06 | Each definition references its Visual by ID, and the Visual records placeholder or final status (GDD ART-01, ART-03) | DECIDED |

## 5. Commands and player tools

Tools (road drawing, area drawing, module placement) run on GameObjects and only produce commands; the simulation validates and applies them.

| ID | Rule | Tag |
| --- | --- | --- |
| CMD-01 | Every player action is a serialisable command: e.g. DesignateArea, EditArea, PlaceModule, DrawRoad, AssignFamily, SetStorageRule, CreateSupplyRoute, Rebuild | DECIDED |
| CMD-02 | Tools preview and pre-validate locally for instant feedback; the simulation re-validates on apply and rejects with a reason | DECIDED |
| CMD-03 | Commands are logged with their tick, so a session can be replayed for debugging | PROPOSED |
| CMD-04 | Undo for drawing tools before confirmation is handled in the tool; after confirmation, changes are new commands (e.g. EditArea, Demolish) | PROPOSED |

### Events and read models

The simulation tells the rest of the game what happened through events, and the UI reads state through read-only views. Nothing outside the simulation holds a writable reference into it.

| ID | Rule | Tag |
| --- | --- | --- |
| CMD-05 | Each tick the simulation writes typed events to an event stream: e.g. BuildingCompleted, StorageFull, WaitingForOx, TaskFailed, FamilyFormed, RaidStarted | DECIDED |
| CMD-06 | UI, audio, notifications and the debug log consume events after the tick. Tests assert on them too (DEV-05) | DECIDED |
| CMD-07 | UI panels read state through read-only views built for the selected object each frame (e.g. a woodcutting operation's modules, families, stock) | DECIDED |
| CMD-08 | Near-camera character proxies (PRS-02) are updated one way by a sync step: position, facing and animation state copied from the simulation | DECIDED |

### Selection (picking)

| ID | Rule | Tag |
| --- | --- | --- |
| CMD-09 | A click first tests the ray against simple invisible boxes around buildings, modules and citizens near the ray, so tall objects are hit from low camera angles. If none is hit, the ray's terrain point queries the spatial index (GEO-16) for the nearest selectable object within a small radius; buildings and areas are matched by point-in-polygon | DECIDED |
| CMD-10 | When several objects are near the click, priority is citizen or ox, then building or module, then area. Trees are not selectable by players (debug overlay only) | DECIDED |
| CMD-11 | No physics colliders on trees or citizens for picking; Unity Physics is added only if a later feature needs it | DECIDED |

## 6. Tasks, dispatch and steps

Work is split into three parts: **tasks** say what needs doing, a **dispatcher** decides who does it, and a small **step runner** on each citizen carries it out. A **schedule** layer above them handles meals and sleep. No behaviour trees.

### Tasks

| ID | Rule | Tag |
| --- | --- | --- |
| TSK-01 | A task is a simulation record: type, target, priority, eligibility (which workplace's families, or unassigned families), required resources (e.g. an ox), reservations held, state | DECIDED |
| TSK-02 | Task types for MVP-1: Fell, Delimb, Buck, Haul, Deliver (construction materials), Build. Later types add without changing the system (e.g. Gather branches, Weave wattle, Mix daub, Repair and Renovate for the Village Works Yard, Plant, Harvest) | DECIDED |
| TSK-03 | Tasks are created by their owners: a woodcutting operation creates Fell tasks for its work area; a construction site creates Deliver and Build tasks; the logistics system creates Haul tasks (section 8) | DECIDED |
| TSK-04 | Each village has a task board holding its open tasks | DECIDED |
| TSK-05 | Task states: Open → Assigned → Active → Done, or Cancelled. Cancelling releases all reservations and resources (section 7) | DECIDED |
| TSK-06 | An owner caps its own open tasks (e.g. a hut opens Fell tasks only for its free workers), so boards stay small | DECIDED |

### Dispatch

| ID | Rule | Tag |
| --- | --- | --- |
| TSK-07 | The dispatcher matches idle workers to open tasks they are eligible for, preferring higher priority, then shorter travel distance | DECIDED |
| TSK-08 | Families assigned to a workplace take that workplace's tasks only. Unassigned families take construction and hauling tasks (GDD POP-04) | DECIDED |
| TSK-09 | A task needing an ox reserves a free ox from the village's pool before it becomes Active; if none is free it waits as *Waiting for ox* (GDD WCO-06) | DECIDED |
| TSK-10 | Dispatch runs on a slower beat (several times per game hour) and is time-sliced across villages | PROPOSED |

### Steps

| ID | Rule | Tag |
| --- | --- | --- |
| TSK-11 | A task expands into a list of steps from its process definition: MoveTo, Work (duration), PickUp, Drop, WaitFor, Attach ox, Detach ox | DECIDED |
| TSK-12 | Each citizen and ox runs a small state machine over its current step: Idle, Moving, Working, Waiting | DECIDED |
| TSK-13 | If a step's target becomes invalid (tree gone, yard demolished, path blocked), the step fails, the task is cancelled, and reservations are released | DECIDED |
| TSK-14 | A disabled process stage adds no step; its duration is added to the next enabled stage (GDD TMB-02) | DECIDED |

### Schedule layer

| ID | Rule | Tag |
| --- | --- | --- |
| TSK-15 | A family schedule (work, meal, sleep) sits above tasks. At mealtime or night it suspends the current task and sends citizens home | DECIDED |
| TSK-16 | A suspended task keeps its reservations and resumes afterwards; if it is not resumed within a timeout, it is cancelled | PROPOSED (timeout PLACEHOLDER) |
| TSK-17 | The walk home to eat is schedule and visuals only; food is deducted once per game day for the whole village (LED-08, GDD ECO-03) | DECIDED |

## 7. Inventory ledger and reservations

One ledger owns every quantity of every item in the world. Everything else holds references into it, so stockpiles, the inspector and the visuals can never disagree.

| ID | Rule | Tag |
| --- | --- | --- |
| LED-01 | Inventory locations: storage pools (operation, granary, storehouse), construction sites, house stores, ground piles at a world position, and carriers (a citizen or an ox in transit) | DECIDED |
| LED-02 | Each location holds, per item: total, reserved, and incoming (reserved capacity for deliveries on the way). Available = total − reserved | DECIDED |
| LED-03 | Five operations only: \*\*Produce\*\* (felling, harvesting, crafting create items at a location), **Reserve** (stock or capacity), **Release**, **Transfer** (from one location to another, including into a carrier), **Consume** | DECIDED |
| LED-04 | Every reservation has an ID and an owner (a task, a construction site, a route). Cancelling the owner releases all its reservations automatically | DECIDED |
| LED-05 | A haul reserves both the stock at the source and the capacity at the destination before it starts, so deliveries never arrive at a full store | DECIDED |
| LED-06 | Transfers are atomic: an item is always in exactly one location, including while being carried | DECIDED |
| LED-07 | Raider theft is an ordinary Transfer into a raider carrier (GDD MIL-09); a killed raider's carrier becomes a ground pile (MIL-11) | DECIDED |
| LED-08 | Daily village food consumption (GDD ECO-03) is one Consume per food type from the village stores, with no transfer. Weekly spoilage is also a Consume (GDD FOOD-09) | DECIDED |
| LED-09 | A debug check in development builds sums all locations each game day and flags any item that appeared or vanished outside the five operations | DECIDED |

## 8. Logistics network

Goods move because of explicit rules the player can see and change, not hidden "nearest free hauler" behaviour. Every storage can be set to pull, push, accept or refuse each item, and to collect only from the producers it is allowed to. This implements GDD LOG rules (GDD section 7) and avoids haulers making long, pointless trips.

### Nodes

| ID | Rule | Tag |
| --- | --- | --- |
| LGS-01 | Every inventory location that takes part in logistics is a node: **producers** (an operation's pool, a workshop's output, backyard produce), **storages** (storage yards, granary, storehouse, starting stockpile) and **consumers** (construction sites, workshop inputs) | DECIDED |
| LGS-02 | Nodes belong to one village. Hauling between villages happens only through supply routes (LGS-14) | DECIDED |

### Storage rules (per storage, per item)

| ID | Rule | Tag |
| --- | --- | --- |
| LGS-03 | **Accept**: on or off. Off means the storage never receives that item | DECIDED |
| LGS-04 | **Minimum (pull)**: below this level the storage requests the item from eligible sources | DECIDED |
| LGS-05 | **Maximum (push)**: above this level the storage offers the excess to eligible destinations. It also stops accepting once reached. Maximum must be above minimum, which stops goods bouncing between two stores | DECIDED |
| LGS-06 | **Priority**: low, normal or high, used when several destinations want the same goods | DECIDED |
| LGS-07 | Defaults come from the storage's definition (e.g. a granary accepts food, a storage yard accepts logs), so storages work sensibly before the player touches them | DECIDED |

### Collection control

| ID | Rule | Tag |
| --- | --- | --- |
| LGS-08 | **Collect from**: each storage chooses where it may collect from: *nearby* (any producer within its haul radius), *linked only* (producers the player has linked to it), or *none* (it only receives deliveries, pushes and routes) | DECIDED |
| LGS-09 | **Service area**: a storage can have a drawn polygon (same tool as work areas) as its collection catchment, instead of a radius | PROPOSED |
| LGS-10 | **Deliver to**: each producer chooses where its output goes: *nearest allowed storage*, *a specific storage*, or *keep* (stays in its own pool until pulled) | DECIDED |
| LGS-11 | **Haul radius**: a maximum haul distance (along roads and paths) per storage, held in data with a player override. Goods are never auto-hauled beyond it; only explicit links and routes go further | DECIDED (default PLACEHOLDER) |

### Matching and hauling

| ID | Rule | Tag |
| --- | --- | --- |
| LGS-12 | A logistics pass runs per village on a slow beat (hourly, time-sliced). It lists offers (producer output, storage excess over maximum) and requests (storage below minimum, construction needs, workshop inputs), then pairs them by priority, then shortest travel distance, within the collect-from, deliver-to and radius rules | DECIDED |
| LGS-13 | Each pairing becomes a Haul task sized to the carrier (e.g. 4 logs per ox trip), with stock at the source and capacity at the destination reserved first (LED-05) | DECIDED |
| LGS-15 | Who hauls: the receiving storage's assigned families first; then the producer's families if it is set to deliver its own output; then unassigned families (GDD POP-04). Logs always need an ox (GDD INV-12) | PROPOSED |

### Supply routes between villages

| ID | Rule | Tag |
| --- | --- | --- |
| LGS-14 | A supply route moves goods from a storage in one village to a storage in another. Modes: *keep stocked* (fill the destination to a target), *fixed amount per period*, or *send surplus* (anything over the source's maximum) | DECIDED |
| LGS-16 | Route trips travel by road and can take days (GDD TIME-07). Goods in transit sit in the carrier's inventory, so they can be counted, delayed or stolen | DECIDED |
| LGS-17 | Who runs routes (dedicated carters, ox carts, pack animals) and their capacities | OPEN |

### Visibility

| ID | Rule | Tag |
| --- | --- | --- |
| LGS-18 | A logistics overlay shows links, service areas, haul radii and live flows (arrows sized by volume), and why a storage is not receiving goods (refused, full, out of radius, no hauler) | DECIDED |

### Rollout by MVP

| MVP | Logistics scope | Tag |
| --- | --- | --- |
| MVP-1 | Producers deliver to their own operation's yards; construction sites pull from the nearest stock. No player settings yet | PROPOSED |
| MVP-2 | Granary accepts food; foragers deliver to the nearest granary | PROPOSED |
| MVP-4 | Full storage rules, collect-from, deliver-to, links, haul radius and overlay | PROPOSED |
| Post-final | Supply routes between villages | PROPOSED |

## 9. Area geometry

Areas are flat 2D polygons in world coordinates that are draped over the terrain for display and checks. All geometry tests run on integer coordinates, so results are exact, repeatable and safe from rounding errors.

### Representation

| ID | Rule | Tag |
| --- | --- | --- |
| GEO-01 | An area is an ordered list of vertices on the ground plane (x, z), stored in the simulation. Height is always sampled from the terrain, never stored | DECIDED |
| GEO-02 | Coordinates are stored as integers in millimetres for geometry operations (clipping, intersection, containment) | PROPOSED |
| GEO-03 | Vertices are kept in one winding order (counter-clockwise); edges are straight segments | DECIDED |
| GEO-04 | Curved edges are dense points: when an edge snaps to a road, points are inserted along the road's edge every 1–2 m (resolves GDD open question on curves) | PROPOSED (spacing PLACEHOLDER) |
| GEO-05 | Area kinds: building area, plot, work area, service area, module footprint. Each kind has its own validation rules from its definition | DECIDED |

### Validation

Every check runs twice: live in the drawing tool (preview colour and reason) and again in the simulation when the command applies (CMD-02).

| ID | Check | Applies to | Tag |
| --- | --- | --- | --- |
| GEO-06 | Simple polygon: no self-intersecting edges, no zero-length edges | All | DECIDED |
| GEO-07 | Minimum and maximum area (m²), and minimum width (so no thin slivers) | All except work areas | DECIDED |
| GEO-08 | Slope: terrain sampled on a grid inside the polygon; maximum slope from the definition | Building areas, plots, footprints | DECIDED |
| GEO-09 | No overlap with other building areas or plots. Touching (a shared edge) is allowed, so terraced plots work | Building areas, plots | PROPOSED (touching) |
| GEO-10 | No crossing a road: each road has a corridor polygon (centre line ± half width) used for this test | Building areas, plots, footprints | DECIDED |
| GEO-11 | Module footprints lie fully inside their parent area and do not overlap each other | Footprints | DECIDED |
| GEO-12 | Editing an area re-runs every check, and is refused if a module would end up outside it (GDD AREA-11) | All | DECIDED |

### Snapping

| ID | Rule | Tag |
| --- | --- | --- |
| GEO-13 | Snap targets: road edges, road ends, area vertices and edges, building entrances. Snap distance is measured in screen pixels, so it feels the same at every zoom | DECIDED |
| GEO-14 | When two consecutive vertices snap to the same road edge, the edge between them is replaced by the road edge's curve (GEO-04) | DECIDED |
| GEO-15 | One toggle turns all snapping on or off, for roads and areas (GDD AREA-06) | DECIDED |

### Spatial index and queries

| ID | Rule | Tag |
| --- | --- | --- |
| GEO-16 | All areas and road corridors are kept in a spatial grid index for fast queries: what is at this point, what overlaps this shape, which trees are inside this work area | DECIDED |
| GEO-17 | Trees inside a work area are found through the index and cached; the cache updates when the area or the trees change | DECIDED |

### Plot analysis (MVP-2 onward)

| ID | Rule | Tag |
| --- | --- | --- |
| GEO-18 | For a plot: measure road frontage (edge length snapped to roads), place the house spot along the frontage, then subtract it to get the backyard polygon and its area | DECIDED |
| GEO-19 | The plot's type (house only, + extra house, + expansion, + both) is computed from the backyard area against the definition's thresholds, live while drawing (GDD HSE-15, HSE-18) | DECIDED |

### Display

| ID | Rule | Tag |
| --- | --- | --- |
| GEO-20 | Area outlines are drawn projected onto the terrain, so they follow hills | DECIDED |
| GEO-21 | Production building areas get an automatic fence: posts placed at a fixed spacing along each edge, set at terrain height (GDD AREA-13) | PROPOSED |
| GEO-22 | Building on slopes: buildings stay level on an automatically generated foundation (stone plinth, sill wall or posts) that fills down to the ground; the terrain is never changed. Each building has a maximum slope (GDD WORLD-07). Terracing for steeper sites, built with retaining walls and paid for in materials such as stone, is a later feature | DECIDED (terracing later MVP) |

## 10. Roads and navigation

Roads are splines that produce both a mesh and a network graph. Movement uses one pathfinder over a terrain cost grid in which roads are cheap; the exact approach is confirmed by prototype P1.

### Roads

| ID | Rule | Tag |
| --- | --- | --- |
| NAV-01 | A road is a spline built from the player's drawn points, smoothed, with a width set by its road type | DECIDED |
| NAV-02 | The spline is sampled every 1–2 m into a centre line; the mesh follows terrain height with a small offset. MVP roads do not reshape the terrain | PROPOSED |
| NAV-03 | The gradient of every sampled segment is checked against the road type's limit (GDD ROAD-09) | DECIDED |
| NAV-04 | Road network graph: nodes at road ends and junctions, edges with length and travel cost. A road ending on or crossing another splits it and adds a junction node (GDD ROAD-05) | DECIDED |
| NAV-05 | The pre-placed trunk road is authored as a spline and loaded into the same system (GDD WORLD-08) | DECIDED |
| NAV-06 | Each road also produces a corridor polygon for area checks (GEO-10) | DECIDED |

### Pathfinding

| ID | Rule | Tag |
| --- | --- | --- |
| NAV-07 | A terrain cost grid covers the map (starting at 2 m cells). Cost rises with slope; building footprints and steep ground block; road cells are cheaper by the road speed bonus (GDD ROAD-10) | PROPOSED (cell size PLACEHOLDER) |
| NAV-08 | Paths are found with A\* in Burst-compiled jobs, batched per tick | PROPOSED |
| NAV-09 | For the large final map, a hierarchical search (coarse chunks first, then detail) or a road-graph-first search for long trips | PROPOSED (decided after P1) |
| NAV-10 | Frequently used paths (hut to yard, home to workplace) are cached and invalidated when buildings or roads change nearby | DECIDED |
| NAV-11 | No collision avoidance between citizens in the MVPs; people may pass through each other | PROPOSED |
| NAV-12 | Unity's built-in NavMesh is evaluated in P1 as an alternative to the custom grid | OPEN |

## 11. Presentation and art conventions

Presentation reads simulation state and draws it; it never decides anything. Large sets are instanced, and character animation detail depends on distance from the camera.

| ID | Rule | Tag |
| --- | --- | --- |
| PRS-01 | Trees and other large static sets are drawn with Entities Graphics instancing, with levels of detail and far impostors (flat stand-ins) | DECIDED |
| PRS-02 | Characters within the near radius (GDD ART-06, 75 m) use pooled GameObject proxies with the Unity Animator: blending and feet fitted to the ground | DECIDED |
| PRS-03 | Characters beyond the radius use baked GPU animation (ENG-07). Switching uses a small distance buffer so characters near the edge do not flicker between the two | DECIDED |
| PRS-04 | Stockpiles are generated from ledger counts: one log model per stored log, laid out by a stacking rule per storage type (GDD INV-06) | DECIDED |
| PRS-05 | Construction progress shows the current stage's visual, or a placeholder scaffold (GDD CON-05) | DECIDED |
| PRS-06 | Overlays (areas, work areas, service areas, logistics flows, nav debug) are drawn as terrain-projected decals or lines | DECIDED |
| PRS-07 | UI built with UI Toolkit | PROPOSED |

### Art conventions (so models drop in without code changes)

| ID | Rule | Tag |
| --- | --- | --- |
| PRS-08 | 1 unit = 1 m; Y up; forward along +Z; pivot at the base centre of the model | DECIDED |
| PRS-09 | Building and module models fit inside the footprint size declared in their definition | DECIDED |
| PRS-10 | Characters use one shared humanoid skeleton (Unity Humanoid avatar); children are scaled versions (GDD ART-07). The ox has its own rig | DECIDED |
| PRS-11 | Each model provides levels of detail: LOD0 (near), LOD1, LOD2, plus an impostor for trees | PROPOSED |
| PRS-12 | Polygon and texture budgets per asset type | OPEN (set after P2 and hardware choice) |

## 12. Save and load

A save stores the simulation only; everything visual is rebuilt on load (GDD SAVE-01, SAVE-02).

| ID | Rule | Tag |
| --- | --- | --- |
| SAV-01 | A save contains: game clock, every simulation record (with stable IDs), the ledger, all reservations, open tasks, area and road geometry, and the random-number state | DECIDED |
| SAV-02 | Saves are taken at a tick boundary, never mid-tick | DECIDED |
| SAV-03 | Custom versioned binary format, written by our own serialiser rather than Unity's world serialisation, so saves survive engine and code updates. Each format version has a migration step | PROPOSED |
| SAV-04 | Saves record the definitions' version; a definition removed in an update loads as a safe fallback with a warning | PROPOSED |
| SAV-05 | Autosave at a set game-time interval, plus manual save and load | PROPOSED (interval PLACEHOLDER) |
| SAV-06 | Automated test: save, load and compare the full simulation state; they must be identical | DECIDED |

## 13. Performance targets and risk prototypes

Final targets wait for the target PC; until then the prototypes below measure the risky parts before Agent NOC builds them for real.

| ID | Rule | Tag |
| --- | --- | --- |
| PRF-01 | Target PC specification: Manor Lords class, see PLT-02 | OPEN |
| PRF-02 | Working target: 60 fps at 1× with 500 citizens and 50,000 trees on the test map; at 16× the game may lower frame rate but must keep up with simulation (CLK-05) | PLACEHOLDER |
| PRF-03 | Simulation cost per tick is measured and shown in the debug overlay from MVP-1 onward | DECIDED |

### Prototypes

Short, throwaway builds, each ending in a written finding that updates this spec.

| ID | Prototype | Question it answers | Before |
| --- | --- | --- | --- |
| P1 | Navigation at scale | Grid A\* vs hierarchical vs NavMesh on hills with roads; cost for 500 agents | MVP-1 |
| P2 | Crowd animation | Near Animator proxies plus far baked animation; switching without flicker; cost at 500–2,000 characters | MVP-1 |
| P3 | Roads on hills | Spline smoothing, terrain-following mesh, gradient check, junctions | MVP-0.5 |
| P4 | Area geometry | Integer polygon clipping, snapping to road curves, touching plots, plot analysis | MVP-0.5 |
| P5 | Simulation at 16× | Tick cost with 1,000 citizens, tasks and logistics passes | MVP-2 |

## 14. Debug tooling

Built alongside the systems, not after them; with agents writing the code, seeing why something happened is essential.

| ID | Tool | From | Tag |
| --- | --- | --- | --- |
| DBG-01 | Time controls: pause, step one tick, step one hour, all speeds | MVP-1 | DECIDED |
| DBG-02 | Citizen inspector: state, current task, current step, reservations, path | MVP-1 | DECIDED |
| DBG-03 | Task board viewer per village: open, assigned and waiting tasks, and why each is waiting | MVP-1 | DECIDED |
| DBG-04 | Ledger audit: daily check that every item is accounted for (LED-09) | MVP-1 | DECIDED |
| DBG-05 | Overlays: nav grid cost, paths, area validation, logistics flows | MVP-0.5 onward | DECIDED |
| DBG-06 | Command log with replay from a save (CMD-03) | MVP-1 | PROPOSED |
| DBG-07 | Definition validator in the editor (DEF-05) | MVP-0.5 | DECIDED |

## 16. Development workflow and testing

Agent NOC's agents build, test and review the game online, so every acceptance criterion must be checkable without a person watching the screen: by automated tests, logged events and captured screenshots.

### Repository and process

| ID | Rule | Tag |
| --- | --- | --- |
| DEV-01 | Git repository with Git LFS for binary assets (models, textures, terrain data) | DECIDED |
| DEV-02 | One branch per task; the reviewer agent approves a merge only when its tests pass and the change cites the GDD and spec IDs it implements | DECIDED |
| DEV-03 | Continuous integration runs Unity in batch mode (no editor window) on every change: compile, definition validator (DEF-05), all tests, and a Windows build on main | DECIDED |
| DEV-04 | Licence: the owner's free Unity Personal account (eligible while revenue and funding stay under $200,000 a year). Activation for batch-mode runs uses GameCI's documented Personal-licence method: activate once in Unity Hub, then store the licence file and account credentials as encrypted CI secrets, never in the repository or chat. Agent NOC verifies this works on its servers before MVP-0 starts | OPEN (verify first) |

### Tests

| ID | Rule | Tag |
| --- | --- | --- |
| DEV-05 | **Unit tests** (Edit Mode) for pure logic: ledger operations, polygon validation and snapping, task and reservation lifecycle, logistics matching, plot analysis | DECIDED |
| DEV-06 | **Scenario tests** (Play Mode) for every MVP acceptance criterion: load the test map, issue commands, run the simulation headless at maximum speed for N game hours, then assert on state and events. Each test is named after the criterion it checks (e.g. `MVP_1_07_TwoYardsGive40Capacity`) | DECIDED |
| DEV-07 | **Save round-trip test**: save, load, compare the full simulation state (SAV-06) | DECIDED |
| DEV-08 | **Screenshot capture** for the reviewer: scenario tests save images from fixed camera positions, with debug overlays on, as build outputs the reviewer can inspect | DECIDED |
| DEV-09 | **Performance tests** with Unity's Performance Testing package: tick cost and frame time on the test map, tracked over time to catch slowdowns | PROPOSED |
| DEV-10 | **Reproducibility test**: the same save and commands, run twice, give identical state (CLK-07) | PROPOSED |

### Project structure and conventions

| ID | Rule | Tag |
| --- | --- | --- |
| DEV-11 | One code assembly per system: Sim.Core, Sim.Ledger, Sim.Geometry, Sim.Tasks, Sim.Logistics, Sim.Navigation, Definitions, Presentation, UI, Tools, Debug, and matching test assemblies. Simulation assemblies may not reference Presentation, UI or Tools | DECIDED |
| DEV-12 | A short conventions file in the repository (naming, folder layout, how to add a definition, how to write a scenario test), maintained by the lead agent | DECIDED |
| DEV-13 | Structured logging with categories (sim, tasks, ledger, logistics, nav); crash logs saved with the last save and command log | DECIDED |
| DEV-14 | Player-facing text is kept in a string table from the start, not hard-coded, so translation is possible later | PROPOSED |

## 17. UI and input (MVP)

The MVP UI is deliberately simple and replaceable: a build bar, a clock bar and movable popup windows. Final layout and style come later from wireframes.

| ID | Rule | Tag |
| --- | --- | --- |
| UI-01 | Build bar along the bottom of the screen with a button per placeable thing (road, test area, woodcutting operation, storage yard, house, granary, and so on), added from definitions | DECIDED |
| UI-02 | Top bar: game date and time, speed buttons (pause, 1×, 4×, 8×, 16× if adopted), and the snapping toggle | DECIDED |
| UI-03 | Clicking a building or area opens its popup window: a plain movable box with a title bar, a close (×) and a pin button at the top right | DECIDED |
| UI-04 | Popups can be dragged anywhere over the game. An unpinned popup is replaced when the player selects something else; a pinned popup stays open until closed | DECIDED |
| UI-05 | Drawing and placement are all mouse-driven: left-click places points, drag moves points, click on an edge inserts a point, double-click or a confirm button finishes, right-click or Escape cancels | DECIDED |
| UI-06 | Camera on keys (pan and rotate) and mouse (pan by edge or drag, rotate, scroll zoom to ground level) | DECIDED |
| UI-07 | The UI reads state through read-only views (CMD-07) and changes things only by issuing commands (ARC-03), so the whole UI can be replaced later without touching the simulation | DECIDED |
| UI-08 | Built with UI Toolkit (PRS-07) | PROPOSED |

## 18. Platform, world and content setup

### Platform and hardware

| ID | Rule | Tag |
| --- | --- | --- |
| PLT-01 | Windows PC only (64-bit), released on Steam. Steam integration comes after the MVPs | DECIDED |
| PLT-02 | Hardware class similar to Manor Lords. Working minimum: quad-core CPU of Intel i5-4670 / AMD FX-4350 class, 8 GB RAM, GTX 1050 (2 GB) / RX 460 class GPU. Working recommended: i5-7600 / Ryzen 3 2200G class, 12 GB RAM, GTX 1060 (6 GB) / RX 580 class | DECIDED (exact specs PLACEHOLDER until profiled) |
| PLT-03 | Like other city builders, this game will be limited by the CPU more than the graphics card, so simulation cost (CLK, PRF) is the main performance budget | DECIDED |
| PLT-04 | No modding support. Definitions stay inside the Unity project as ScriptableObjects; nothing is loaded from external files | DECIDED |

### World scale

| ID | Rule | Tag |
| --- | --- | --- |
| PLT-05 | The map is centred on the world origin. A floating origin shifts the rendered world when the camera moves far from the origin, to avoid visual jitter on the large final map | DECIDED |
| PLT-06 | The 2 × 2 km test map does not need the floating origin; it is required before maps larger than about 8 km across | PROPOSED |

### Terrain authoring

| ID | Rule | Tag |
| --- | --- | --- |
| PLT-07 | MVP test map (2 × 2 km): sculpted and painted in the Unity editor with Unity Terrain and its Terrain Tools package | DECIDED |
| PLT-08 | Maps of 10–20 km² or larger must move to a more robust pipeline: an external terrain generator (realistic erosion, plus soil and wetness maps for overlays) and/or real-world elevation data from an English landscape, imported as heightmaps across several terrain tiles. Which one is chosen before the first large map | DECIDED (which pipeline OPEN) |
| PLT-09 | Trees are scattered in the editor, then converted when the map is built into tree entities with IDs and seeded trunk lengths. Unity's built-in terrain trees are not used at runtime, because they cannot be simulation objects | DECIDED |

### Placeholder content

| ID | Rule | Tag |
| --- | --- | --- |
| PLT-10 | Until models exist, every building and module is a ghost footprint on the ground with a floating name label numbered per type: *Home 1*, *Home 2*, *Woodcutters 1*, *Storage yard 1* | DECIDED |
| PLT-11 | Placeholder trees and terrain textures come from free public-domain (CC0) sources such as Quaternius, Kenney, Poly Haven or ambientCG. Each placeholder is marked as such (GDD ART-03) | DECIDED |
| PLT-12 | Final models are made in Blender and exported as FBX. The lead agent sets up one Blender export preset and checks it with a test import against PRS-08 (scale, axes, pivot) | DECIDED |

## 15. Open technical questions

- [ ] Resolved: target PC is Manor Lords class (PLT-02); confirm exact specs after profiling.
- [ ] Exact Unity 6 LTS version at kickoff (ENG-01).
- [ ] Crowd animation solution: buy or build (ENG-07, after P2).
- [ ] Navigation approach: grid A\*, hierarchical, or NavMesh (NAV-09, NAV-12, after P1).
- [ ] Resolved 5 Nov: building on slopes uses adaptive foundations now, terracing later (GEO-22).
- [ ] Who runs supply routes and with what carriers (LGS-17).
- [ ] Polygon and texture budgets (PRS-12).
- [ ] Resolved 5 Nov: same-machine reproducibility, with host-authoritative multiplayer if it ever comes (CLK-07). Resolved: picking uses boxes then the spatial index (CMD-09); Unity Terrain for the test map, a robust pipeline required for maps of 10–20 km² or more (PLT-07, PLT-08). Verify Unity Personal in batch mode (DEV-04).
