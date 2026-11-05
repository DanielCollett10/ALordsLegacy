# GDD — Medieval Settlement & Regional Economy Simulation

Nov 5, 2026 · @Dan

## 0. Document control

This GDD defines what the game is and how it plays; it is the design source the Agent NOC coordinator breaks into tasks for the lead, dev and Unity agents. Technical architecture (component structure, serialisation, ECS choice) is out of scope here and belongs to the systems/technical spec.

**Status:** v0.4, 5 Nov 2026. Ready for lead and reviewer assessment in Agent NOC, and for breakdown of MVP-0 to MVP-2. MVP-3 onward is PROPOSED and needs owner sign-off before task breakdown.

**Companion documents:** the [Technical Spec](https://claude.ai/artifact/K6eGfjnKhUVFkF67T43fCm) defines how this design is built (Unity 6 with ECS/DOTS, architecture, tasks, logistics, geometry, saving).

**Art:** the owner builds the 3D models. Agents use placeholders, and every visual must be swappable without changing the simulation (PRIN-04).

### How agents should use this document

- Every testable rule has an ID (e.g. `ROAD-04`). Tasks should reference the IDs they implement, and acceptance tests should reference the IDs they verify.
- A rule's tag tells an agent how much freedom it has. Agents must not invent mechanics for anything tagged OPEN; they raise it to the lead instead.
- Numbers marked PLACEHOLDER must live in data (definitions/config), never hardcoded, so design can tune them without code changes.
- Simulation state is the source of truth; visuals only represent it (see `PRIN-04`).

### Tags

| Tag | Meaning | Agent freedom |
| --- | --- | --- |
| DECIDED | Confirmed by the owner | Implement as written |
| PROPOSED | Drafted to fill a gap; awaiting owner confirmation | Implement, but keep it easy to change |
| PLACEHOLDER | A tunable value chosen to unblock work | Expose in data; do not design around the exact number |
| OPEN | Not yet designed | Do not implement; raise a question |

### ID prefixes

`PRIN` principles · ART visuals and placeholders · `WORLD` world and terrain · TIME game time · SAVE saving · `AREA` area/plot drawing · `ROAD` roads · `BLD` building framework · `CON` construction · `INV` inventory and storage · `TMB` timber chain · `WCO` woodcutting operation · `POP` population and labour · `FOOD` food and foraging · `HSE` housing and burgages · ECO village economy · LOG logistics · MIL military and raids · `MVP` acceptance criteria.

## 1. Vision and principles

A grounded medieval settlement sim in Unity. The player grows a 10–50 person camp into a network of villages whose stone, tools and goods eventually fund a medieval town with a keep and castle. Every log, person and road physically exists, distances and time are close to real, and raids by rival lords and bandits are a rare but serious threat.

The feel sits between Banished and Manor Lords: Manor Lords-style freeform roads and plot drawing, extended with more polygon control and Sims-style add-on modules (extra storage, upgraded facilities) attached to buildings.

### Pillars

1. **Physical economy.** Goods are real quantities that are carried, stored and consumed. What you see is what exists.
2. **Grounded time and distance.** Real walking speeds, a real calendar, villages miles apart. The game is played by speeding up and slowing down; 100 logs becoming boards becoming a house that stands for 5–10 years should feel earned.
3. **Freeform, organic layout.** Roads and plots are drawn by the player and follow the land; settlements look grown, not gridded.
4. **Buildings as operations.** Most buildings are a drawn area with a core building and add-on modules, not a single icon.
5. **Logistics is the challenge.** The difficulty is building the network of villages and routes that supports ever larger settlements, up to a town with a keep and castle.
6. **People are precious.** Troops come from the villages' own men, the dead are not replaced instantly, and a successful raid can devastate a village.
7. **Policy over micromanagement.** The player sets priorities; citizens do the routine work.

### Core design principles

| ID | Principle | What it means for implementation | Tag |
| --- | --- | --- | --- |
| PRIN-01 | Organic development | Growth comes from player layout and economic circumstance, not rigid tech-tree unlocks | DECIDED |
| PRIN-02 | Physical economy | Inventory quantities are authoritative; visible stockpiles match them exactly. Deliberate abstractions are listed explicitly: daily food consumption (ECO-03), seed (ECO-14) and finances (ECO-15) | DECIDED |
| PRIN-03 | Persistent people | Each citizen is a simulation record that exists whether or not a model is drawn | DECIDED |
| PRIN-04 | Simulation is truth | Models, meshes and animations represent state; they never own quantities, connectivity or identity | DECIDED |
| PRIN-05 | Expandable processes | Multi-step processes (felling, construction, production) are data-defined stage lists, so stages can be added later without rewriting the system | DECIDED |
| PRIN-06 | Not a puzzle game | Getting stuck early is possible but is not the intended challenge, and the design should not aim for it. The challenge is the logistics network and villages, under the threat of raids. Starting resources set opening difficulty | DECIDED |
| PRIN-07 | Data-driven definitions | Buildings, modules, items, recipes and stages are defined as data, not per-building scripts | DECIDED |
| PRIN-08 | Multiplayer-tolerant | Avoid design assumptions that rule out shared or neighbouring settlements later; no multiplayer in any MVP. Any future multiplayer is host-authoritative: player 1's PC runs the game and checks other players' actions (Tech Spec CLK-07) | DECIDED |
| PRIN-09 | Nothing has to look square | Every designated area and designated module footprint starts as a minimum-size square and can always be reshaped. Squares are allowed, never forced | DECIDED |
| PRIN-10 | Families do the work | Labour is assigned by family (household), as in Manor Lords, not by individual | DECIDED |

### Visual representation and placeholders

Everything visible is a swappable representation of simulation data. Placeholders and the owner's final models can sit side by side for as long as needed, and replacing one never changes the simulation.

| ID | Rule | Tag |
| --- | --- | --- |
| ART-01 | Every visible thing (trees, buildings, modules, citizens, oxen, items, roads) is drawn from a visual definition that its data definition references by ID. Swapping the asset changes no simulation data (PRIN-04) | DECIDED |
| ART-02 | Every visual starts as a placeholder and can be replaced one at a time. Placeholders and final models can be mixed in the same build indefinitely | DECIDED |
| ART-03 | Each visual definition records whether it is a placeholder or final, so outstanding art can be listed at any time | PROPOSED |
| ART-04 | Replacement models follow fixed conventions so they drop in without code changes: 1 unit = 1 m, agreed pivot point and facing, building models fit their footprint, characters use the shared rig (ART-07) | DECIDED (conventions defined in the technical spec) |
| ART-05 | Any action without its own animation plays a generic work loop, so actions can ship before their art (TMB-03) | DECIDED |
| ART-06 | Character animation detail depends on each character's distance from the camera, not on zoom level. Inside the near radius: live animation with blending and feet fitted to the ground. Outside it: cheap pre-recorded (baked) playback. Both stay on the terrain surface | DECIDED (radius PLACEHOLDER: 75 m) |
| ART-07 | All citizens share one humanoid skeleton, with children scaled down. Variety comes from swappable heads, hair, clothing and colours | PROPOSED |
| ART-08 | The ox uses its own four-legged rig | DECIDED |
| ART-09 | Switching between live and baked animation should not visibly pop | PROPOSED |

**Animations needed by MVP** (PROPOSED). MVP-1 people: idle, walk, axe chop, sawing, leading an ox, hammering. MVP-1 ox: idle, walk, dragging logs. MVP-2 adds eating and sitting.

## 2. World, time and saving

The world is one continuous, hand-authored landscape; only resource placement varies between playthroughs.

| ID | Rule | Tag |
| --- | --- | --- |
| WORLD-01 | Terrain is authored, not procedurally generated. The MVP map is rolling hills, valleys, open land and forest | DECIDED |
| WORLD-02 | Terrain exposes height and slope at any point, for use by area drawing, roads, placement and navigation | DECIDED |
| WORLD-03 | Trees are individual harvestable resources (see section 8), not decoration. Forest density is authored per map area | DECIDED |
| WORLD-04 | Resource deposits (clay, stone, iron) are placed randomly per world instance, spread out so that some lie far from the start | DECIDED |
| WORLD-05 | Placement rules for deposits (seed, minimum distances, guaranteed nearby resources) | OPEN |
| WORLD-06 | The start area always provides timber and foraging food, so the opening is never starved of basics | PROPOSED |
| WORLD-07 | Slope limits: each area type and road has a maximum buildable slope, held in data. Buildings stay level on an automatically generated foundation; terracing steeper sites (paid for in stone) comes later (Tech Spec GEO-22) | PLACEHOLDER |
| WORLD-08 | The map has a pre-placed trunk road. It is part of the road network from the start, and player roads can join it | DECIDED |
| WORLD-09 | The game starts at a pre-placed camp-site near the trunk road. It holds the starting stockpile and an oxen stand with the starting ox | DECIDED |
| WORLD-10 | Whether the trunk road leads off-map (future trade and arrivals) and whether the start location is fixed or chosen | OPEN |

Rivers, marsh, fertile soil quality and regional boundaries are later features (section 12). The MVP map should still leave space for them so they can be added without re-authoring the terrain.

### Game time and speed

Time follows a real calendar, and people move at real walking speed at 1×. A 45-minute day/night cycle makes one game day run 32× faster than real time.

| ID | Rule | Tag |
| --- | --- | --- |
| TIME-01 | The calendar is a real 365-day year | DECIDED |
| TIME-02 | A game day at 1× is 30 real minutes of daylight plus 15 of night (45 minutes; game time runs 32× real time) | DECIDED (lengths PLACEHOLDER) |
| TIME-03 | Speed controls: pause, 1×, 4×, 8×; a 16× speed is PROPOSED to help long stretches pass | DECIDED |
| TIME-04 | Movement is at real-world speed on screen at 1×: walking about 5 km/h; oxen slower (about 3 km/h) | DECIDED (speeds PLACEHOLDER) |
| TIME-05 | Families keep a daily schedule: work, a meal at home at a set time, sleep at night | DECIDED |
| TIME-06 | All durations (build times, felling stages, crop growth) are defined in game time, held in data, and scale with game speed | DECIDED |
| TIME-07 | The map is large, with villages several miles apart; travel between them takes one or more game days | DECIDED |

**What these numbers imply.** At 5 km/h on screen, a game day of non-stop walking covers about 3.75 km (2.3 miles), so a village 5–8 km away is 1.5–2 game days' walk, which matches TIME-07. The cost is that local trips take a lot of game time: a 300 m walk to a tree takes about 3.5 real minutes, roughly 2 game hours. Workers therefore make few trips per game day, and production, food use and build times must be tuned per game day with that in mind. A game year lasts about 274 real hours at 1×, or 34 hours at 8×, so a yearly crop cycle is slow.

| ID | Rule | Tag |
| --- | --- | --- |
| TIME-08 | Whether a full year is too long in play, and if so whether to shorten the calendar, speed up the day, or offer a faster speed | OPEN (decide after playtesting MVP-2) |

### Saving and loading

| ID | Rule | Tag |
| --- | --- | --- |
| SAVE-01 | Standard save and load: a save stores the whole game state, and loading restores it exactly | DECIDED |
| SAVE-02 | Everything shown on screen after a load must match the saved simulation state (PRIN-04) | DECIDED |

## 3. Area and plot drawing

The player defines operations and plots by drawing a freeform polygon on the terrain. It starts as 4 points, can be given many more, can bend to follow roads, and may never overlap another area.

### Drawing flow

1. Choose a building or plot type from the build menu. This sets which validation rules apply.
2. Drag on the terrain to lay down a 4-point shape (the base polygon).
3. Reshape it: drag points, click an edge to insert a point, or remove a point. The outline previews valid/invalid live.
4. Confirm to create the area, or cancel to discard it. Nothing is created or costed until the player confirms.

### Rules

| ID | Rule | Tag |
| --- | --- | --- |
| AREA-01 | Areas are closed polygons that start as a minimum-size square (4 points) and can always be reshaped (PRIN-09) | DECIDED |
| AREA-02 | The player can add points to make freeform shapes (irregular burgages, curved yards) | DECIDED |
| AREA-03 | Maximum points per area, held in data per type (starting value 16) | PLACEHOLDER |
| AREA-04 | Building areas and plots can never overlap each other. Work areas are a separate layer and may overlap (WORK rules below) | DECIDED |
| AREA-05 | An area edge near a road snaps to the road's edge and follows its curve, so plots sit flush along road frontage | DECIDED |
| AREA-06 | Snapping (to roads, area edges and points) can be toggled on and off; it shares one toggle with road snapping | DECIDED |
| AREA-07 | Areas drape over the terrain: the boundary follows ground height, and the area is valid on uneven ground within its slope limit | DECIDED |
| AREA-08 | Areas may not self-intersect, cross a road, or exceed their type's slope limit | PROPOSED |
| AREA-09 | Each type has a minimum and maximum area in m², held in data | PLACEHOLDER |
| AREA-10 | Invalid areas show the reason (overlap, too steep, too small, crosses road) before confirmation | PROPOSED |
| AREA-11 | A confirmed area can be reshaped later. A change that would leave a placed module outside the area, or create an overlap, is refused | PROPOSED |
| AREA-12 | Plot size fixed at drawing time decides what a plot can later become (e.g. room for an artisan workshop, section 5) | DECIDED |

### Open points

- Whether edges can be true curves, or only point-to-point segments that look curved when there are enough points (snapping to a road would insert the points automatically).
- Whether an area can share an edge exactly with a neighbouring area (terraced burgages) and snap to it.

### Work areas

Production buildings that work the land have two polygons: a fenced **building area** holding their modules, and a separate **work area** that only tells workers where to work. Work areas are drawn with the same tool but follow looser rules.

| ID | Rule | Tag |
| --- | --- | --- |
| WORK-01 | Every production building that gathers from the land (woodcutter, firewood cutter, clay pit, forager and so on) has one work area, separate from its building area | DECIDED |
| WORK-02 | Work areas are drawn and edited with the same polygon tool: 4 base points, more can be added, snapping toggle | DECIDED |
| WORK-03 | A work area only assigns work. It does not block construction: other buildings, plots and roads can be built inside it | DECIDED |
| WORK-04 | Work areas are hidden by default and shown only when the player opens the work-area option on the building (e.g. the woodcutting lodge panel) | DECIDED |
| WORK-05 | Work areas may overlap each other; resource reservation (TMB-08) stops two workers taking the same tree | PROPOSED |
| WORK-06 | Building a structure or road over resources inside a work area clears them (BLD-10) | PROPOSED |
| WORK-07 | A newly placed building gets a default work area around it, which the player can reshape | PROPOSED |
| WORK-08 | Maximum distance from the building to the furthest point of its work area, held in data | PLACEHOLDER |
| AREA-13 | A production building area is marked by a boundary (e.g. a fence) generated automatically along its edge | PROPOSED |

## 4. Roads

Roads are a core experience and the first system built (MVP-0.5). The player draws a freeform route, the road follows the hills, and it becomes a persistent part of the movement network.

### Drawing flow

1. Choose the road tool.
2. Click to start, then move the mouse. The road previews as a smooth curve laid over the terrain.
3. Click to set intermediate points; each one bends the curve. Double-click or press confirm to finish. Escape cancels.
4. On confirmation the road is created as a world object and joins the road network.

### Rules

| ID | Rule | Tag |
| --- | --- | --- |
| ROAD-01 | Roads are freeform curves, not tile segments; the system generates geometry from the drawn route | DECIDED |
| ROAD-02 | Roads follow the terrain's contours and rise and fall with the hills | DECIDED |
| ROAD-03 | The mouse snaps to useful points: road ends, existing road lines, area edges and building entrances | DECIDED |
| ROAD-04 | Snapping can be toggled on and off (shared toggle with area drawing, AREA-06) | DECIDED |
| ROAD-05 | A road that starts or ends on another road, or crosses one, creates a junction automatically | PROPOSED |
| ROAD-06 | Roads are persistent: saved, reloadable and present in the network, not temporary lines | DECIDED |
| ROAD-07 | The network is a simulation record (nodes and connections), separate from the road mesh (PRIN-04) | DECIDED |
| ROAD-08 | Roads cannot pass through building areas or plots (work areas are unaffected); those areas snap to roads instead (AREA-05, AREA-08) | PROPOSED |
| ROAD-09 | Steepness limit: a section steeper than the limit shows as invalid in the preview | PLACEHOLDER |
| ROAD-10 | Citizens and oxen move faster on roads than off-road; the multiplier is held in data (starting value 1.5×) | PROPOSED |
| ROAD-11 | Drawn roads can be deleted. Editing an existing road's curve is a later feature | PROPOSED |
| ROAD-12 | MVP roads are free dirt paths with no material cost | PROPOSED |

Later features include road upgrades (dirt, gravel, cobble), material costs, bridges, transport capacity and regional routes. Road data should allow a road type field from the start.

## 5. Building framework

Every building uses one of three patterns: an area operation, a road-fronted plot, or a fixed-footprint structure. Each can grow through one or more of three kinds of upgrade.

### Building patterns

| Pattern | How it is placed | Examples | Growth |
| --- | --- | --- | --- |
| Area operation | Draw a fenced building area, place a mandatory core building and add-on modules inside it, then draw a separate work area for its workers (section 3) | Woodcutting operation (MVP-1), foraging camp (MVP-2), later quarry, clay pit | Add-on modules |
| Plot (burgage) | Draw a plot along a road; a house is built on its frontage and the rest is the backyard | Houses (MVP-2), artisan burgages (later) | Plot-dependent upgrades |
| Fixed footprint | Place a single structure of defined size | Granary or pantry (MVP-2), animal tie point | In-place upgrades |

### Upgrade types

| ID | Type | Rule | Example | Tag |
| --- | --- | --- | --- | --- |
| BLD-01 | Add-on module | Extra modules placed in free space inside an operation's area; each adds capacity or a function | A second storage yard adds 20 log capacity | DECIDED |
| BLD-02 | Plot-dependent upgrade | Only available if the plot was drawn with enough spare space (AREA-12) and, for artisans, the household is level 2 or above; it uses backyard space | Burgage upgraded to an artisan workshop | DECIDED |
| BLD-03 | In-place upgrade | Raises a building's tier without changing its footprint | Animal tie point upgraded to a stable | DECIDED |
| BLD-04 | Some buildings have no upgrades at all; this is set per definition |  |  | DECIDED |

### Module rules

| ID | Rule | Tag |
| --- | --- | --- |
| BLD-05 | A module belongs to exactly one parent area and must sit fully inside it | DECIDED |
| BLD-06 | Modules have their own footprint and position, and may not overlap other modules | DECIDED |
| BLD-07 | Each operation type defines its mandatory core modules and its optional modules | DECIDED |
| BLD-08 | An operation without all its mandatory modules complete is not operational; the inspector lists what is missing | DECIDED |
| BLD-09 | Each module has its own construction material cost (section 6) | PROPOSED |
| BLD-10 | Placing a module on trees marks them for clearing, and their timber is added to the economy | PROPOSED |
| BLD-11 | Demolishing a module returns a share of its materials (starting value 50%) to the nearest storage | PLACEHOLDER |
| BLD-12 | Modules cannot be moved after placement in the MVPs; demolish and rebuild instead | PROPOSED |

### Operational states

`Planned` (placed, not built) → `Under construction` → `Operational`. An operational building becomes `Inactive` when a cause applies (missing core module, no workers, storage full, or paused by the player), and the inspector shows that cause.

### Burgage plots (MVP-2 onward)

Burgages work as in Manor Lords: a plot drawn along a road holds one house for one family. A bigger plot leaves backyard space for an expansion or an extra house, and households rise through 5 levels as their needs are met.

| ID | Rule | Tag |
| --- | --- | --- |
| HSE-01 | A plot needs road frontage (at least one edge snapped to a road, AREA-05). The house stands on the frontage edge | DECIDED |
| HSE-02 | A house holds one family (two at levels 4–5) | DECIDED |
| HSE-03 | A plot drawn larger than a standard house footprint has a backyard | DECIDED |
| HSE-04 | A backyard can take an expansion: an artisan workshop (e.g. forge, clothier), livestock, or an allotment-style garden | DECIDED |
| HSE-05 | If the plot has room, an extra house can be added, holding another family. It uses backyard space, so plot space is a trade-off between expansions and extra houses | DECIDED (trade-off PROPOSED) |
| HSE-06 | Garden and livestock produce goes to the village stores, not the household (ECO-02) | DECIDED |
| HSE-07 | Plot sizes (m²) needed for a backyard, each expansion and an extra house | PLACEHOLDER |
| HSE-08 | Which artisans exist, and whether plots can be subdivided or merged | OPEN |

### Household levels

A household's level rises when its needs are supplied from the village stores; each level opens new options for the plot.

| Level | Needs to reach it | Opens | Tag |
| --- | --- | --- | --- |
| 1 | Food and water | Basic household | DECIDED |
| 2 | Diverse food, water, church access | Artisan backyard expansions | DECIDED |
| 3 | Level 2 needs + alcohol (cider from orchards, ale from barley, or mead from bees) | More artisan types | DECIDED |
| 4 | Level 3 needs + advanced food: salt and prepared food (sausages, biscuits, bread and so on) | Multi-floor house holding 2 families | DECIDED |
| 5 | Level 4 needs + silk garments (baseline). May change, e.g. a security requirement such as a retinue guarding the area or a village wall | Multi-floor, highest tier, for denser towns | PROPOSED |

| ID | Rule | Tag |
| --- | --- | --- |
| HSE-09 | Levels and their needs are data. A need can be a supplied good (food, ale), an access requirement (within range of a church or market) or a settlement condition (security). Levels, needs and need types can be changed or added without system changes | DECIDED |
| HSE-10 | Once a household reaches a level it never drops, even if supply stops | DECIDED |
| HSE-11 | Unmet needs or poor building condition never lower a house's level. Instead they block further upgrades and reduce the household's work output until resolved | DECIDED (for now) |
| HSE-12 | "Diverse food" means at least 2 different food types available in the village stores | PLACEHOLDER |
| HSE-13 | Water comes from wells, added later. Wells depend on groundwater in the terrain, shown with a map overlay; the same overlay approach will show which land suits which crops | DECIDED (later MVP) |
| HSE-14 | What level 5 adds over level 4 beyond being the top tier | OPEN |
| HSE-19 | A need whose source is not in the game yet (water before wells, church before churches) counts as met | DECIDED |

### House tiers and renovation

A household's level and its house's tier are the same number. Meeting the needs makes a household eligible for the next level; a renovation using real materials and labour completes it. Tiers are upgrades of the same dwelling, not separate buildings.

| Tier | House | Typical construction |
| --- | --- | --- |
| 1 | Primitive hut | Rough timber posts, wattle panels, earth daub, reed or straw thatch, packed-earth floor |
| 2 | Improved cottage | Stronger timber supports, better wattle and daub, better roof, doors, possibly stone footings |
| 3 | Timber-framed cottage | Dressed structural timber, better carpentry, flooring and foundations; wattle-and-daub infill |
| 4 | Two-storey house (2 families) | Complex timber framing, upper floor, stairs, more rooms, possibly dressed stone foundations |
| 5 | Prosperous house | High-quality timber, advanced carpentry, dressed stone, better finishes, larger layout |

| ID | Rule | Tag |
| --- | --- | --- |
| HSE-20 | Reaching the needs for the next level makes a household eligible; the player then orders a renovation, which consumes that tier's materials and labour before the level applies | DECIDED |
| HSE-21 | Renovation recipes per tier (materials and quantities) | OPEN |
| HSE-22 | Maintenance, renovation and expansion are separate activities, each with its own tasks | DECIDED |
| HSE-23 | MVP-2 houses are placeholder tier 1 houses built from logs only. Wattle, daub and thatch replace the log recipe once the core systems are proven (MVP-3); this is a data and UI addition, not a new system | DECIDED |

### Plot sizes

As in Manor Lords, a plot's size decides what it can hold. The front holds the house, and space behind it can hold a second house, an expansion, or both.

| Plot type | Holds | Minimum size | Tag |
| --- | --- | --- | --- |
| House only | 1 house | House spot: 60–100 m² | PLACEHOLDER |
| House + extra house | 2 houses, 2 families | House spot + 60 m² | PLACEHOLDER |
| House + expansion | 1 house + garden, livestock, bees or artisan | House spot + 80 m² | PLACEHOLDER |
| House + extra house + expansion | 2 houses + 1 expansion | House spot + 140 m² | PLACEHOLDER |

| ID | Rule | Tag |
| --- | --- | --- |
| HSE-15 | Plot space is allocated in order: the house spot first, then spare space. Spare space at least the extra-house buffer can take a second house; whatever remains is expansion space | DECIDED |
| HSE-16 | The player chooses whether spare space becomes an extra house or expansion space, as long as it is large enough for that use | PROPOSED |
| HSE-17 | Expansion types: allotment garden, livestock, bees, artisan workshop. The first artisans are the clothier and the cooper (barrels; the player has no barrels until a cooper exists) | DECIDED |
| HSE-18 | The plot editor shows, while drawing, which plot type the current shape qualifies for | PROPOSED |

### Artisans vs production buildings (needs definition)

The game has two kinds of industry, and both must stay worth using; neither may simply replace the other. **This needs more design work before either is built.**

|  | Artisan (house upgrade) | Production building (placed) |
| --- | --- | --- |
| Where | Backyard of a burgage, level 2+ household | Its own building area, like the woodcutting operation |
| Examples so far | Clothier, cooper | Primitive carpenter (woven baskets), later a proper carpenter (boxes, barrels, carts) |
| Starting idea for the split | Specialist, higher-tier goods, low volume, tied to household level | Basic and bulk goods, higher volume, staffed from the family pool |

Overlap already exists: the cooper and the proper carpenter both make barrels. Options to resolve it include giving each a different product range, different quality tiers, or making one supply inputs to the other. Status: OPEN.

## 6. Construction and materials

Buildings cost exactly the materials they visibly use. A standard start provides about 20 logs, enough to build the first few buildings in any reasonable order.

### Rules

| ID | Rule | Tag |
| --- | --- | --- |
| CON-01 | Every building and module has an exact material recipe in its definition (e.g. 8 logs). No abstract build points | DECIDED |
| CON-02 | A construction site reserves its materials from storage. They are hauled to the site (logs only by ox, INV-12) and consumed as work happens | DECIDED |
| CON-03 | Reserved materials cannot be used by another job; the inspector shows total, reserved and available stock | DECIDED |
| CON-04 | Construction is a data-defined list of stages (PRIN-05). The MVPs use 2 stages, *Deliver materials* and *Build*; foundation, frame, walls and roof can be added later without system changes | DECIDED |
| CON-05 | The building's visual shows its current stage. Until staged models exist, a placeholder scaffold shows progress | PROPOSED |
| CON-06 | Construction needs at least one available citizen. The player can raise a site's priority | PROPOSED |
| CON-07 | Cancelling a site returns undelivered reservations to stock and drops delivered materials on site as a pickup pile | PROPOSED |

### Starting conditions

| ID | Rule | Tag |
| --- | --- | --- |
| CON-08 | A standard start provides 20 logs, held in the starting stockpile at the camp-site (WORLD-09) | DECIDED (amount PLACEHOLDER) |
| CON-09 | Harder starts lower the starting materials. This is the only lever for opening difficulty | DECIDED |
| CON-10 | A standard start includes 1 ox, tied at the camp-site's pre-placed oxen stand | DECIDED |
| CON-11 | Oxen form a settlement-wide pool. Any hauling job, construction or production, requests a free ox; an oxen stand is not required for an ox to work | DECIDED |
| CON-12 | No never-stuck guarantee: a player can spend themselves into a corner. Avoiding it is the player's job, and it is not the intended challenge (PRIN-06) | DECIDED |
| CON-13 | How the player gets more oxen | OPEN (likely MVP-3) |

### First-build costs

These are starting values to make the opening work. Real values should follow the models' actual log counts once they are built.

| Building | Cost (logs) | Build time (worker-seconds) | Tag |
| --- | --- | --- | --- |
| Woodcutting hut | 8 | 60 | PLACEHOLDER |
| Storage yard (designated ground) | 0 | 0 | PROPOSED |
| Oxen stand (tie pole and rope) | 1 | 10 | PLACEHOLDER |
| House (MVP-2) | 6 | 60 | PLACEHOLDER |
| Granary / pantry (MVP-2) | 6 | 45 | PLACEHOLDER |

A working woodcutting operation costs 8 logs (the hut), leaving 12 of a standard start for houses and other buildings.

### Early construction industries (MVP-3)

Two simple production buildings supply the materials for tier 1–2 houses. They use the same area-operation pattern as woodcutting, so they are data and UI additions once that pattern is proven.

| ID | Rule | Tag |
| --- | --- | --- |
| CON-14 | Wattle-weaving station: workers gather branches from hedgerows and coppiced woodland in its work area and weave them into wattle panels. Wattle stays useful as wall infill at every tier | DECIDED |
| CON-15 | Branches removed by the felling chain's delimb stage can also supply the wattle station (GDD TMB-10) | PROPOSED |
| CON-16 | Daub mixing pit: makes basic daub from local earth, straw or fibres and water; no clay pit needed. Prepared clay later makes better daub that lasts longer and needs less upkeep | DECIDED |
| CON-17 | Thatch from reed (gathered) or grain straw (from farming) | DECIDED (sources by MVP OPEN) |

### Materials, condition and maintenance

| ID | Rule | Tag |
| --- | --- | --- |
| CON-18 | Buildings are made of components (roof, walls, frame, foundation), each with a material, a quality, a condition and an upkeep need | DECIDED |
| CON-19 | Material choices differ in cost, durability and upkeep: earth daub vs prepared clay daub; reed vs straw thatch; rough vs dressed timber; basic vs dressed stone foundations | DECIDED |
| CON-20 | Workmanship (the builder's skill) affects how long a component lasts | DECIDED (skill system MVP-3) |
| CON-21 | Condition falls over time, calculated weekly rather than continuously | DECIDED |
| CON-22 | A Village Works Yard handles all maintenance: it finds buildings needing repair, stores common repair materials, assigns its workers, and repairs roofs, daub, timber and other components, important or badly worn buildings first | DECIDED |
| CON-23 | Repairs are visible work that consume real materials from storage | DECIDED |
| CON-24 | Poor condition never lowers a house's tier; it blocks upgrades and reduces output (HSE-11) | DECIDED (for now) |
| CON-25 | Which MVP adds condition and the Works Yard | OPEN (proposed MVP-4) |

## 7. Inventory, storage and logistics

Storage modules in an operation add to one shared pool, and every quantity is exact and visible. A storage yard holds 20 logs.

| ID | Rule | Tag |
| --- | --- | --- |
| INV-01 | Items are defined as data (ID, name, unit, transport class, stack visual). MVP-1 item: Log. MVP-2 adds food items (section 11) | DECIDED |
| INV-02 | All storage modules in an operation add their capacity to one inventory pool for that operation | DECIDED |
| INV-03 | A storage yard is designated ground that holds 20 logs at its minimum size | DECIDED (PLACEHOLDER value) |
| INV-04 | Reshaping a storage yard larger raises its capacity in proportion to its area | PROPOSED |
| INV-05 | Each quantity is tracked as total, reserved, available (total − reserved) and in transit | DECIDED |
| INV-06 | The visual stockpile matches the actual quantity. For logs, one visible log per log stored | DECIDED |
| INV-07 | Although the pool is shared, each log is shown in a specific yard. Yards fill one at a time, nearest to the hut first | PROPOSED |
| INV-08 | When the pool is full, the operation stops harvesting and shows *Storage full*. Logs already cut wait at the tree site | PROPOSED |
| INV-09 | Construction and other consumers draw from the nearest pool that has available stock, and may combine pools | PROPOSED |
| INV-10 | The starting stockpile (CON-08) is a settlement-level storage point with no capacity limit for its starting contents | PROPOSED |
| INV-11 | Loose items in the world (logs at a felled tree, dropped materials) are tracked inventory at that location, not decoration | DECIDED |
| INV-12 | Each item has a transport class. Heavy items (logs) move only by ox and are never carried by hand; light items (food) can be carried by citizens | DECIDED (food carrying PROPOSED) |

### Logistics (later MVPs)

The player controls where goods go, so haulers never make long, pointless trips. This goes deliberately further than Manor Lords; the implementation is in Technical Spec section 8.

| ID | Rule | Tag |
| --- | --- | --- |
| LOG-01 | Each storage has per-item settings: accept or refuse, a minimum it pulls up to, a maximum it pushes out above, and a priority | DECIDED |
| LOG-02 | Each storage chooses where it collects from: nearby producers, only producers the player has linked to it, or none | DECIDED |
| LOG-03 | Each producer chooses where its output goes: the nearest allowed storage, a specific storage, or kept in its own pool until pulled | DECIDED |
| LOG-04 | Goods are never hauled automatically beyond a storage's haul radius; only explicit links and supply routes go further | DECIDED |
| LOG-05 | Supply routes move goods between villages: keep a store stocked, send a fixed amount per period, or send surplus | DECIDED |
| LOG-06 | A logistics overlay shows links, collection areas, live flows, and why a store is not receiving goods | DECIDED |
| LOG-07 | Rollout: simple automatic delivery in MVP-1 and MVP-2; full settings in MVP-4; supply routes after the final MVP | PROPOSED |

### Village economy and treasury

The economy is communal, as in Manor Lords. What the village produces goes into the village stores, and the village's gold is kept and spent there, separate from the lord's purse.

| ID | Rule | Tag |
| --- | --- | --- |
| ECO-01 | Goods produced in a village belong to that village's stores, not to the household or workplace that made them | DECIDED |
| ECO-02 | Home produce (allotment vegetables, eggs, milk, wool, honey and other animal products) goes to the village stores | DECIDED |
| ECO-03 | Food is consumed by a single daily deduction from the village stores; nobody fetches or delivers it. This is a deliberate exception to the physical economy (PRIN-02), to avoid pathing and scheduling overhead | DECIDED |
| ECO-04 | Each village has its own treasury (called the town treasury once it becomes a town), shared by the whole village and spent only on it | DECIDED |
| ECO-05 | The village treasury pays for communal infrastructure, maintenance, seed, livestock and bees for backyards, artisan upgrades, imports and improvements, and can save towards larger projects | DECIDED |
| ECO-06 | The lord's treasury is the player's personal wealth. It receives tithes, dues and other lordly income, and pays for the lord's estate, the military (including mercenaries) and regional ambitions | DECIDED |
| ECO-07 | Each household's contribution scales with its house tier and prosperity. The player sets the split between the village treasury and the lord's treasury | DECIDED (rates OPEN) |
| ECO-08 | Getting rich is not the goal. Coin is a means of running and improving villages, not a score | DECIDED |
| ECO-09 | The village treasury arrives in MVP-3 with a starting sum, so backyard seed, livestock and bees can be bought. Trade with other regions comes later | PROPOSED |
| ECO-10 | Contribution rates, how the lord collects (eventually through his keep), income beyond trade, and prices | OPEN |
| ECO-11 | Access to higher-tier goods (alcohol, clothes and so on) comes from being within range of a market. When supply is short, houses closer to the market always get priority over houses further away | DECIDED |
| ECO-12 | Basic food (level 1) needs no market; it comes straight from the village stores | PROPOSED |
| ECO-13 | The food economy uses realistic numbers for consumption, crop yields and growth (FOOD-08 to FOOD-15) | DECIDED (numbers OPEN) |
| ECO-14 | Seed is a soft seasonal resource, not physical goods. A new field has a one-off seed and establishment cost; part of each harvest is kept for next season automatically; the village buys extra seed for shortfalls, expansion or new crops | DECIDED |
| ECO-15 | Finances are abstract: income, spending, reserves and purchasing policies, with no simulated household-by-household economy | DECIDED |
| ECO-16 | Seed buying is set on a village policy screen. Default: keep seed from the last harvest and buy any shortfall when the treasury can afford it | DECIDED |

### Food economy

Food is the central survival system, balanced around the full 365-day year: people eat every day, but food is produced in seasons, so reserves must carry the village through winter and early spring.

| ID | Rule | Tag |
| --- | --- | --- |
| FOOD-08 | Consumption is daily and set by population; production is seasonal; harvests refill the communal reserves | DECIDED |
| FOOD-09 | Each food type has a storage and preservation profile: how fast it spoils and where it keeps best. Spoilage is calculated weekly | DECIDED (rates PLACEHOLDER) |
| FOOD-10 | Granaries, cellars, smokehouses and other stores extend food life and are major investments | DECIDED |
| FOOD-11 | Production depends on weather, soil fertility, crop choice, farming skill, labour and occasional crop disease | DECIDED |
| FOOD-12 | Food sources complement each other (grain, vegetables, fruit, livestock, fishing, hunting, preserved food); diversifying beats relying on one crop | DECIDED |
| FOOD-13 | The village is shown in one of three states: shortage (reserves cannot meet consumption), self-sufficient (needs met plus a modest reserve), or surplus (genuine surplus to grow reserves or export) | DECIDED |
| FOOD-14 | Hunger reduces work output, and prolonged hunger causes starvation deaths. Families leave only in extreme, prolonged famine | DECIDED (thresholds OPEN) |
| FOOD-15 | A bad harvest is survivable with adequate reserves, and feeding an unlimited population must never be easy | DECIDED |

## 8. Timber chain

A tree becomes logs in staged steps: fell, delimb, cut to length, and haul by ox. The stages are data, so MVP-1 can ship with only some of them animated, and more can be added later without reworking the system.

### Stages

| Order | Stage | Tree or item state after | Who | MVP-1 | Tag |
| --- | --- | --- | --- | --- | --- |
| 1 | Fell | Felled trunk (with branches) on the ground; stump left | Woodcutter | Enabled, animated | DECIDED |
| 2 | Delimb | Clean trunk | Woodcutter | Defined, disabled | DECIDED |
| 3 | Cut to length (buck) | N logs lying at the tree site (INV-11) | Woodcutter | Enabled, animated | DECIDED |
| 4 | Haul | Logs in a storage yard | Ox + handler | Enabled | DECIDED |

### Rules

| ID | Rule | Tag |
| --- | --- | --- |
| TMB-01 | The chain is an ordered list of stage definitions. Each holds: input state, output state, worker role, duration, required animal or tool, animation key and an enabled flag | DECIDED |
| TMB-02 | A disabled stage is skipped. Its duration is added to the next enabled stage, so the total time per tree stays the same whichever stages are on | PROPOSED |
| TMB-03 | A stage with no animation yet plays a generic work loop at the tree, so a stage can be enabled before its art exists | PROPOSED |
| TMB-04 | All logs are the same standard length (starting value 4 m) | DECIDED (length PLACEHOLDER) |
| TMB-05 | Each tree has a usable trunk length. Logs produced = floor(usable length ÷ standard log length) | DECIDED |
| TMB-06 | Starting usable trunk lengths are 12–20 m, giving 3–5 logs per tree | PLACEHOLDER |
| TMB-07 | Woodcutters pick the nearest unreserved standing tree inside the operation's work area. One woodcutter works one tree at a time | PROPOSED |
| TMB-08 | A tree is reserved for a worker from the start of Fell until its logs are cut, so two workers never take the same tree | DECIDED |
| TMB-09 | Stumps stay as visible terrain objects in the MVPs. Regrowth and replanting are OPEN | PROPOSED |
| TMB-10 | Tree species, timber quality and by-products (branches as firewood) are OPEN; the item data should allow them later | OPEN |

## 9. Woodcutting operation (MVP-1)

The woodcutting operation is the first production building. The player designates a building area with a hut footprint and a storage yard, plus a work area. The operation starts once the hut is built and the player assigns a family to it. Logs move only by ox, drawn from the settlement's oxen.

### Modules

| Module | Role | Requirement | Capacity | Tag |
| --- | --- | --- | --- | --- |
| Woodcutting hut | Core workplace. It provides work slots that the player fills from the pool of unassigned families; the work area is set from its panel | Mandatory, exactly 1; must be built before the operation starts | 2 family work slots | DECIDED (slots PLACEHOLDER) |
| Storage yard | Designated ground where logs are stacked; adds to the operation's pool | Mandatory, at least 1; more can be added | 20 logs at minimum size | DECIDED |
| Oxen stand | Tie point where an ox can wait at the operation, shortening its trips | Optional | 1 ox | PROPOSED |

### Rules

| ID | Rule | Tag |
| --- | --- | --- |
| WCO-01 | The operation has two polygons: a fenced building area for its modules (AREA rules) and a work area where its workers harvest (WORK rules) | DECIDED |
| WCO-02 | The operation starts working only once the hut is built, at least one storage yard is designated, and the player has assigned at least one family | DECIDED |
| WCO-03 | Module footprints (hut, storage yard) start as a minimum-size square and can be reshaped (PRIN-09) | DECIDED |
| WCO-04 | The hut costs 8 logs, hauled by ox from the starting stockpile and built by unassigned families (section 6) | DECIDED (cost PLACEHOLDER) |
| WCO-05 | The hut never fills its own slots; the player assigns families to them from the unassigned pool (POP-03) | DECIDED |
| WCO-06 | Logs are never carried by hand. A member of the assigned family leads a free ox from the settlement's oxen. If none is free, logs wait at the tree site and the operation shows *Waiting for ox* | DECIDED |
| WCO-07 | An ox hauls up to 4 logs per trip | PLACEHOLDER |
| WCO-08 | Hauling goes to the yard with free capacity that is nearest the tree site; logs are unloaded onto that yard's visible stack | PROPOSED |
| WCO-09 | When no standing trees remain in the work area, the operation goes *Inactive: no trees*; the player redraws the work area | PROPOSED |
| WCO-10 | The player can assign or remove families (0 to slot count), pause or resume, show and edit the work area, and remove modules | DECIDED |

### Work loop

1. A member of the assigned family reserves the nearest standing tree in the work area (TMB-07).
2. They walk to it, then fell it, delimb it if that stage is enabled, and cut it to length. Logs now lie at the tree site.
3. A family member fetches a free ox (from the camp-site stand, or the operation's own stand if built), loads up to 4 logs, and skids them to the nearest yard with space.
4. The logs are unloaded; the pool and the yard's visible stack go up by the same amount. The ox returns to its stand or takes its next hauling job.
5. Repeat until the pool is full (INV-08), there are no trees left (WCO-09), or the player pauses the operation.

### Inspector

Selecting the operation shows: its modules and their states (designated, under construction, built); assigned families and capacity; current task per worker; ox availability; pool stock as total / reserved / available against capacity (e.g. 28 / 0 / 28 of 40); logs waiting at tree sites; standing trees left in the work area, with a toggle to show and edit it; and any inactive cause.

## 10. Population and labour

Labour is assigned by family, as in Manor Lords: the player assigns families to workplaces, and the family's members do that work. Citizens are persistent individuals within families from MVP-1, and gain depth until the final MVP.

### Citizen depth by MVP

| MVP | Family and citizen records hold | Tag |
| --- | --- | --- |
| MVP-1 | Family: ID, members, workplace. Citizen: ID, name, birth date, family, current task, current destination, carried item. Start: 3 families, 10 citizens | PROPOSED (counts PLACEHOLDER) |
| MVP-2 | + residence (one family per house), hunger, food drawn from village stores | PROPOSED |
| MVP-3 | + household level 1–5 driven by needs (section 5), professions, skills, ageing, families arriving or leaving with housing and food | PROPOSED |
| Final MVP | Full inspection: select any citizen or family, see who they are, where they live and work, their household level, and what they are doing now | DECIDED |

### Rules

| ID | Rule | Tag |
| --- | --- | --- |
| POP-01 | A citizen exists and works in the simulation whether or not their model is shown (PRIN-03) | DECIDED |
| POP-02 | Workplaces provide work slots counted in families | DECIDED |
| POP-03 | The player assigns families from the unassigned pool to work slots. Workplaces never fill themselves | DECIDED |
| POP-04 | Unassigned families build construction sites and haul materials, as in Manor Lords | PROPOSED |
| POP-05 | A family works at one workplace at a time | DECIDED |
| POP-06 | Each house holds one family (two at multi-floor levels, HSE rules) | DECIDED |
| POP-07 | Starting population for the full game is 10–50, set by the chosen start | DECIDED (range) |
| POP-08 | Oxen are a settlement-wide pool, separate from families. Any hauling job requests a free ox, and a stand is not required for an ox to work | DECIDED |
| POP-09 | A family is 2 parents (husband and wife) and up to 3–4 children | DECIDED |
| POP-10 | Both parents work. Children work at the family's workplace from age 7–8 | DECIDED (age PLACEHOLDER) |
| POP-11 | Younger children are not simulated as cared for; they idle around their house, their parents or the village | DECIDED |
| POP-12 | Men and boys of serving age (14–15+) can be drawn from families as troops (MIL rules, section 12) | DECIDED (age PLACEHOLDER) |
| POP-13 | People who die are not replaced instantly | DECIDED |
| POP-14 | Every citizen has a birth date and ages day by day on the game calendar | DECIDED |
| POP-15 | A dead husband is replaced automatically by a man from one of the player's villages (or a generated man if none is free), after a randomly weighted delay of 1–6 months. No matchmaking is simulated | DECIDED (delay PLACEHOLDER) |
| POP-16 | Families are restored after a loss by options A, B and D below; D is the main way new families form | DECIDED |

### Restoring families after a loss (options)

The difficulty: work is assigned by family, but a death leaves a family incomplete. These options can be combined.

| Option | How it works | Trade-off |
| --- | --- | --- |
| A. Incomplete families keep working | A family is 1–2 adults plus children. A widowed family stays in its house and keeps its work slot, with output scaled to its working members | Simplest, and needs no single-person families as a separate type. A slot's output varies |
| B. Newcomer remarriage | A newcomer (POP-15) arrives and joins an incomplete family, restoring it to 2 adults | Matches POP-15. The village depends on newcomers arriving |
| C. Merging | Two incomplete families in the village (a widow and a widower) combine into one, freeing a house | Realistic, and uses existing people. Can shuffle families between houses and workplaces |
| D. Coming of age | A child who reaches adulthood forms a new family with a newcomer or another family's child, once a free house exists | Long-term, organic growth. Slow: it takes years of game time |
| E. Absorption | A widowed adult with young children moves in with relatives in another family, leaving their house empty | Realistic for the period. Needs relatives to be tracked |

**Decision:** A + B + D, kept simple. Incomplete families keep working at reduced output (A); a replacement husband joins automatically (B, POP-15); and coming of age and marriage (D) is the main way new families form. No servants, merging or relatives (C and E dropped). Family simulation exists to feed logistics, production and defence, not to be a focus of its own.

### How new families form (medieval model)

In medieval English villages, a couple usually married once they could get a holding of their own, so a new marriage meant a new household. The game follows that rule: no free house, no new family. Housing therefore drives population growth.

| ID | Rule | Tag |
| --- | --- | --- |
| POP-17 | Life stages: 0–6 child (idle); 7–8 to 13 works with the family; 14+ serving age; adult and able to marry from 18 | PROPOSED (ages PLACEHOLDER) |
| POP-18 | A new family forms when an unmarried adult marries, typically between ages 20 and 26 | PROPOSED |
| POP-19 | A marriage happens only when a free house is available for the couple. Until then, unmarried adults stay and work in their parents' family | DECIDED |
| POP-20 | When the parents die, the eldest child inherits the house; brothers and sisters need a house of their own | PROPOSED |
| POP-21 | Partners come from the same village or another of the player's villages | DECIDED |
| POP-22 | If no partner is free in the player's villages, one is generated, so a shortage of men or women never blocks a new family | PROPOSED |
| POP-23 | A family's work output is proportional to its working members. Example: a widow with 3 children under 7 has 1 of 4 members working (25%); it rises as children reach working age, and jumps when a replacement husband joins, which can happen at any point after the 1–6 month delay (POP-15) | DECIDED (formula PLACEHOLDER) |
| POP-24 | No random deaths, including child deaths. People die only from war and raids, from prolonged starvation (FOOD-14), or from a health system added later | DECIDED |
| POP-25 | Married couples have a child about every 2–3 years while the wife is of childbearing age | PLACEHOLDER |
| POP-26 | The lord's fee for permission to marry (merchet) and the entry fee for taking up a holding, as possible uses for coin | OPEN |
| POP-28 | Family simulation stays simple: no matchmaking, servants or relationship tracking beyond family membership | DECIDED |

**Timing.** A game year is about 34 real hours at 8× (TIME-02), so at calendar speed a newborn would need over 600 real hours to reach marrying age. Ageing therefore runs faster than the calendar, a possible 16× speed helps long stretches pass, and starting families include older children. All three will be tuned in playtesting.

| ID | Rule | Tag |
| --- | --- | --- |
| POP-27 | Citizens age at 2× the calendar rate, held in data | DECIDED (rate PLACEHOLDER) |
| POP-29 | Starting families include some teenage and young adult children, so the first marriages happen within the first few game years | PROPOSED |

Confirmed by the owner on 5 Nov 2026: simple persistent workers from MVP-1, full citizens by the final MVP.

## 11. MVP roadmap

Seven MVPs run from an interactive landscape to a self-sustaining small village. Each one must pass its acceptance criteria before the next starts. MVP-0 to MVP-2 are DECIDED in scope; MVP-3 onward are PROPOSED.

&#91;embedded content: MVP roadmap · 7 gated MVPs\]

Each MVP builds on the one before; the proposed second row is the part most open to change.

### MVP-0 — Landscape and interaction

**Goal:** the player can look around a hand-authored world and point at the ground.

MVP-0 is the terrain map and nothing else: a small hand-authored test map of rolling hills, open plains and a forest, which the player can look around and point at. Roads, the trunk road and the camp-site come in MVP-0.5 and MVP-1.

| Element | Requirement | Tag |
| --- | --- | --- |
| Map | One hand-authored test map of gentle rolling hills and open plains, about 2 × 2 km. The full game map is much larger (TIME-07); this is a test map | DECIDED (size PLACEHOLDER) |
| Forest | At least one forest of individual trees. Every tree is a resource object with an ID and a usable trunk length (WORLD-03, TMB-05) | DECIDED |
| Ground | Basic terrain materials: grass, dirt, forest floor | DECIDED |
| Models | Placeholder tree models; the owner's models are swapped in later without changing tree data (PRIN-04) | DECIDED |
| Camera | Manor Lords-style RTS camera: pan (keys and screen edge), rotate, and zoom from a high overview down to ground level; stays above the ground and inside the map. Walking around in first person is a possible later feature, not in any MVP | DECIDED |
| Picking | The cursor reports terrain point, height and slope. Trees are not clickable for players; the debug overlay can show a tree's ID and trunk length for testing | DECIDED |
| Debug overlay | Frame rate and tree count, for early performance checks | PROPOSED |

- MVP-0-01: The test map loads with rolling hills, open plains and a forest.
- MVP-0-02: The camera moves freely over the whole map without passing under the ground or leaving the map.
- MVP-0-03: The cursor reports the terrain point, height and slope under it.
- MVP-0-04: The debug overlay can show each tree's ID, usable trunk length and the logs it would yield; players cannot select trees (floor(length ÷ 4 m)).
- MVP-0-05: Replacing the tree model does not change any tree's data.
- MVP-0-06: Frame rate with the full forest visible is recorded; the target is set in the technical spec.

### MVP-0.5 — Roads and areas

**Goal:** prove the two core drawing tools before any building exists.

**Scope:** road drawing (section 4), including the pre-placed trunk road (WORLD-08); area drawing with a generic *test area* type and a generic work area (section 3); snapping and its toggle; overlap and slope validation; save and load of roads and areas.

- MVP-0.5-01: A freeform road can be drawn over hills; it follows the contours and stays after confirmation.
- MVP-0.5-02: A road drawn to touch another creates a junction, and the network shows them as connected.
- MVP-0.5-03: With snapping on, the cursor snaps to road ends, road lines and area edges; with it off, it does not.
- MVP-0.5-04: A 4-point area can be drawn, given extra points, reshaped, confirmed or cancelled.
- MVP-0.5-05: An area edge drawn beside a road snaps flush to the road's curve.
- MVP-0.5-06: An overlapping, too-steep or road-crossing area is refused, with the reason shown.
- MVP-0.5-07: Roads and areas survive a save and reload unchanged.

### MVP-1 — Woodcutting operation

**Goal:** the first working economy loop, from tree to visible stockpile.

**Scope:** woodcutting building area with hut and storage yard (oxen stand optional), plus its work area (section 9); construction from the 20-log starting stockpile (section 6); 3 starting families (section 10); timber chain with Fell, Cut to length and Haul enabled (section 8); pooled inventory (section 7); inspector; the starting camp-site (WORLD-09); game clock, day/night cycle and speed controls (TIME rules).

- MVP-1-01: The game starts at the camp-site by the trunk road, with 20 logs, 1 ox at its stand, and 3 unassigned families.
- MVP-1-02: The player designates a building area on uneven ground, with a hut footprint and a storage yard. Both start as minimum squares and can be reshaped.
- MVP-1-03: The player draws a work area from the hut panel. It hides when the panel closes, and other buildings can be placed inside it.
- MVP-1-04: Unassigned families build the hut, with its 8 logs hauled by ox from the stockpile; visible stock goes down by exactly 8.
- MVP-1-05: Before the hut is complete, the operation does not work and shows the hut as missing.
- MVP-1-06: Once it is built, the player assigns a family to a hut slot; no family is ever assigned automatically.
- MVP-1-07: A worker reserves a tree in the work area, walks to it, fells it and cuts it into floor(length ÷ 4 m) logs.
- MVP-1-08: A family member leads the ox to haul the logs to a yard. No log is ever moved by hand; with the ox busy, logs wait and the operation shows *Waiting for ox*.
- MVP-1-09: The pool count, the inspector and the visible stacks agree after every delivery.
- MVP-1-10: A second storage yard raises pool capacity from 20 to 40.
- MVP-1-11: A full pool stops harvesting and shows *Storage full*; using logs elsewhere restarts it.
- MVP-1-12: Enabling the Delimb stage in data adds it to the loop with no code change.

### MVP-2 — Houses and food

**Goal:** citizens live somewhere and eat, so the settlement becomes a place rather than a work camp.

**Scope:** burgage plots with a base house (section 5); households; a foraging camp (an area operation with a forager hut as core; forage bushes inside the area yield food); a granary or pantry (fixed footprint) as food storage; scheduled meals at home, with food deducted from the village stores (no fetching or delivery); hunger; starting food stock. Water counts as met until wells exist.

| ID | Rule | Tag |
| --- | --- | --- |
| FOOD-01 | Food items: berries and mushrooms, counted in units | PROPOSED |
| FOOD-02 | Families walk home for a meal at a set time each day (TIME-05). Food is deducted once per game day for the whole village; the walk home to eat is schedule and visuals only. Nobody fetches or delivers food (ECO-03) | DECIDED |
| FOOD-03 | Each citizen eats 1 food unit per game day | PLACEHOLDER (to be replaced by realistic figures, ECO-13) |
| FOOD-04 | Granary / pantry capacity: 100 food units | PLACEHOLDER |
| FOOD-05 | A standard start includes 50 food units, so food never hard-locks the opening (PRIN-06) | PLACEHOLDER |
| FOOD-06 | Forage bushes regrow over time; seasonal yield is OPEN | PROPOSED |
| FOOD-07 | Water counts as met until wells exist (HSE-19) | DECIDED |

Houses in MVP-2 are level 1, one family each, built from logs only as placeholders (HSE-23), on plots with no backyard expansions yet.

- MVP-2-01: A plot drawn along a road gets a house; a household moves in and appears in the inspector.
- MVP-2-02: Foragers gather from bushes in their area and deliver food to the granary.
- MVP-2-03: Daily food use lowers stock by exactly citizens × 1; hungry citizens show as hungry.
- MVP-2-04: Food and logs are tracked in separate pools without interfering with each other.

### MVP-3 — Growth (PROPOSED)

**Goal:** the settlement grows on its own when housing and food allow it.

**Scope:** families arrive when there is spare housing and a food surplus, and leave only in extreme famine (FOOD-14); household levels 1–2 driven by needs; backyard gardens and livestock feeding the village stores; extra houses on large plots; professions and skills; the first in-place upgrade (animal tie point to stable, BLD-03); a village treasury with a starting sum, used to buy seed, livestock and bees for backyards (ECO-09); wattle-weaving station, daub pit and thatch, so tier 1 huts replace the placeholder log houses and renovation to tier 2 works (HSE-20, HSE-23).

### MVP-4 — Processing and logistics (PROPOSED)

**Goal:** a second production chain, and player control over where goods go.

**Scope:** a sawpit that turns logs into planks; a building that costs planks; logistics settings (accept lists, minimum and maximum stock, priorities); road speed bonus tuning (ROAD-10).

### Final MVP — Small village (PROPOSED)

**Goal:** a small village that sustains itself, proving every core system together.

**Scope:** about 40 citizens in families; timber, planks and food chains; at least one level-2 household running an artisan backyard (clothier or cooper) (BLD-02, HSE-04); full citizen inspection.

- FINAL-01: A village of 40 or more citizens runs for 2 in-game years with no player input beyond priorities, and does not starve.
- FINAL-02: Every stockpile, construction site and citizen shown matches the simulation state after a save and reload.
- FINAL-03: At least one plot drawn with spare backyard space is upgraded to an artisan workshop.
- FINAL-04: Performance targets are set and met on the agreed target PC (OPEN until the hardware is chosen).

## 12. Long-term systems (beyond the final MVP)

These stay in the vision. No MVP builds them, but no MVP may make them impossible either (PRIN-08).

| System | Intent | Key open questions |
| --- | --- | --- |
| Endgame | A medieval town with a keep and castle, built with stone, tools and goods supplied by the network of villages | Which buildings mark the endgame |
| Regions | The world is split into regions holding settlements, resources and routes. The player starts with 1 and may reach about 6–8 | Are regions physical map areas, administrative boundaries, or both? |
| Expansion permission | A lord or king grants new regions | Is the cost wealth, reputation, military service or something else? Can regions be bought, inherited, conquered or lost? |
| Regional settlements | Full settlements founded near scarce clay, stone or iron, with their own housing, food and defence | How much is managed locally and how much centrally? |
| Trade and markets | Goods exchanged with regions outside the map, paid in village coin (ECO-05) | Prices, routes |
| Military, raids and fortification | See below | See below |
| Settlement stages | Camp, small village, village, large village, town, fortified settlement, as descriptive labels rather than gates (PRIN-01) | Thresholds |
| Multiplayer | Shared or neighbouring settlements, trade, diplomacy, conflict | Player count and rules |
| Rich terrain | Rivers, marsh, groundwater, soil fertility overlays, seasons | Which of these affect the economy |

### Military, raids and losses

Raids by rival lords and bandit camps are rare but can devastate a village; raiding back is an opportunity. Because distances are real, a large retinue 5–10 days' walk away cannot save a village in time.

| ID | Rule | Tag |
| --- | --- | --- |
| MIL-01 | Troops are drawn directly from a settlement's men and boys of serving age (14–15+). Taking them removes workers from their families | DECIDED |
| MIL-02 | Dead troops and citizens are not replaced instantly (POP-13), so every loss hurts the village's economy | DECIDED |
| MIL-03 | Rival lords and bandit camps raid player villages. Raids are rare, but a successful raid can hit a village very hard | DECIDED |
| MIL-04 | Defence depends on distance: a village's own retinue matters, because help from a larger retinue several days away arrives late | DECIDED |
| MIL-05 | The player can attack raiders and bandit camps. Spoils include weapons to sell, gold to keep and food rations | DECIDED |
| MIL-06 | Raiders can burn and raze buildings. The building's designated area or plot is never deleted: it stays as a ruin | DECIDED |
| MIL-07 | Selecting a ruined plot or building shows a *Rebuild* button where the upgrade buttons normally are. Clicking it queues a rebuild task that citizens carry out | DECIDED |
| MIL-08 | A rebuild costs half the original materials | DECIDED (PLACEHOLDER value) |
| MIL-09 | Raiders steal physically: they must walk to the storage that holds an item (a granary, a house) and carry it away. Stolen goods leave that exact storage, and nothing is taken without that interaction (PRIN-02) | DECIDED |
| MIL-10 | Raiders can take anything stored in the village, from food in granaries to weapons kept in houses, limited by what they can carry | DECIDED (carry limits PLACEHOLDER) |
| MIL-11 | Loot carried by a raider who is killed or caught is dropped where they fall and can be recovered | PROPOSED |
| MIL-12 | Houses can store personal equipment such as weapons, as a small house inventory | PROPOSED |
| MIL-13 | Retinue, equipment, the lord's keep and tithe collection (ECO-07) | OPEN |

## 13. Open questions and decision log

### Open questions for the owner

- [x] Worker depth, harvest zone, oxen, start camp-site, hut-before-work, household needs, coinage. Resolved (see decision log).
- [x] Game time, speed controls, saving, family size, meals, water and church timing, first artisans. Resolved 5 Nov.
- [x] Never-stuck: no guarantee; getting stuck is allowed but not the intended challenge. Resolved.
- [x] Who works: both parents, plus children from age 8. Resolved.
- [x] Raids: burning and razing, plots kept for one-click rebuild at half cost, physical theft. Resolved.
- [ ] Tune family timing in playtesting: ageing rate (POP-27), 16× speed (TIME-03), replacement delay (POP-15), birth rate (POP-25).
- [ ] Artisans vs production buildings: what each is responsible for (section 5).
- [ ] Consequence of unmet household needs (HSE-11), and level 5's final requirements.
- [ ] Realistic food numbers: consumption, yields, spoilage, and hunger and famine thresholds (ECO-13, FOOD-09, FOOD-14). Renovation recipes per house tier (HSE-21). Contribution rates (ECO-07). When condition and the Works Yard arrive (CON-25).
- [ ] Market range, and when the market building arrives in the roadmap.
- [ ] Real plot sizes (plot size table).
- [ ] Tithe rate, coin income and prices (ECO-10).
- [ ] How the player gets more oxen (CON-13).
- [ ] Does the trunk road lead off-map, and is the start location fixed or chosen (WORLD-10)?
- [ ] Area edges use dense points that follow road curves (Tech Spec GEO-04, PROPOSED). Confirm.
- [ ] Neighbouring plots may share an edge, allowing terraced burgages (Tech Spec GEO-09, PROPOSED). Confirm.
- [ ] Is a full 365-day year too long in play (TIME-08)? Owner leans towards keeping it; revisit after playtesting.
- [ ] Confirm the proposed scope of MVP-3, MVP-4 and the final MVP.
- [ ] Target PC hardware for performance criteria (Tech Spec PRF-01).

### Decision log

Newest first.

| Date | Decision | Section |
| --- | --- | --- |
| 2026-11-05 | House tiers merged with household levels: needs make a household eligible, renovation with real materials completes it | 5 |
| 2026-11-05 | Poor condition and unmet needs never lower a level; they block upgrades and reduce output | 5, 6 |
| 2026-11-05 | Hunger reduces output and prolonged hunger kills; families leave only in extreme famine | 7, 10 |
| 2026-11-05 | MVP-2 houses are log-only placeholders; wattle, daub and thatch arrive in MVP-3 as data and UI additions | 5, 11 |
| 2026-11-05 | Wattle-weaving station, daub pit, building components with condition, and a Village Works Yard for maintenance | 6 |
| 2026-11-05 | Village and lord treasuries: contributions scale with house tier and prosperity, player sets the split; finances and seed are abstract | 7 |
| 2026-11-05 | Seasonal food economy over a 365-day year with spoilage, storage facilities and shortage / self-sufficient / surplus states | 7 |
| 2026-11-05 | Family model kept simple: output scales with working members; replacement husband joins automatically; no servants; partners only from the player's villages; ageing at 2× calendar; 16× speed proposed | 2, 10 |
| 2026-11-05 | Raids burn and raze buildings but never delete their plot; a *Rebuild* button queues a rebuild at half the original materials; raiders steal only by physically reaching the storage | 12 |
| 2026-11-05 | Citizens have birth dates and age; a replacement husband can join after 1–6 months | 10 |
| 2026-11-05 | Raids are rare but devastating; troops come from village men 14–15+; dead are not replaced instantly | 10, 12 |
| 2026-11-05 | Both parents work; children work from age 7–8; younger children idle, no childcare simulated | 10 |
| 2026-11-05 | No never-stuck guarantee; the challenge is logistics and village growth toward a town with keep and castle | 1, 6 |
| 2026-11-05 | Grounded time and distance is a pillar; a long game of speeding up and slowing down is intended | 1, 2 |
| 2026-11-05 | Real 365-day calendar; 45-minute day/night at 1×; speeds 1×/4×/8×; real walking speed; villages miles apart | 2 |
| 2026-11-05 | Standard whole-game save and load | 2 |
| 2026-11-05 | Family = 2 parents + up to 3–4 children | 10 |
| 2026-11-05 | Wells (with groundwater overlay) and church come later; needs whose source does not exist yet count as met | 5 |
| 2026-11-05 | First artisans: clothier and cooper; artisan vs production building split needs definition | 5 |

| Date | Decision | Section |
| --- | --- | --- |
| 2026-11-05 | Village coin serves the whole village (backyard seeds, livestock, bees, artisan upgrades, trade with other regions); lord's coin is separate (mercenaries, regional matters); villages pay tithes to the lord | 7 |
| 2026-11-05 | Plot types by size, Manor Lords style: house only; + extra house; + expansion; + both | 5 |
| 2026-11-05 | Household levels never drop; unmet needs have no effect for now (known gap) | 5 |
| 2026-11-05 | Level needs: 1 food and water; 2 diverse food, water, church; 3 alcohol; 4 salt and prepared food; 5 silk (provisional); all data-driven | 5 |
| 2026-11-05 | Communal economy: village production goes to village stores; each village keeps its own treasury, separate from the lord's purse | 7 |
| 2026-11-05 | Burgages: one family per house; large plots allow a backyard expansion (artisan, livestock, garden) or an extra house | 5 |
| 2026-11-05 | Households have levels 1–5: level 2 artisans, level 3 more artisans, levels 4–5 multi-floor with 2 families | 5 |
| 2026-11-05 | Workplaces provide family work slots that the player fills from the unassigned pool; no auto-assignment | 9, 10 |
| 2026-11-05 | The woodcutting hut must be built before the operation starts (supersedes "starts on designation") | 6, 9 |
| 2026-11-05 | Labour is assigned by family, as in Manor Lords | 1, 10 |
| 2026-11-05 | Oxen are a settlement-wide pool; no stand is needed for an ox to work; the operation's oxen stand is optional | 6, 9, 10 |
| 2026-11-05 | Game starts at a pre-placed camp-site (stockpile, oxen stand, 1 ox) near a pre-placed trunk road | 2, 6 |
| 2026-11-05 | Nothing has to look square: designated areas and footprints start as minimum squares and can be reshaped | 1, 3 |
| 2026-11-05 | A technical spec (Unity DOTS) follows the GDD; the owner builds the 3D models | 0 |
| 2026-11-05 | Production buildings have a separate work area, hidden by default, that does not block other construction | 3, 9 |
| 2026-11-05 | Logs move only by ox; nothing heavy is carried by hand | 7, 9 |
| 2026-11-05 | Worker depth: simple persistent workers from MVP-1, full citizens by the final MVP | 10 |
| 2026-11-05 | Felling is staged (fell, delimb, cut to length, haul by ox) and stages are expandable data | 8 |
| 2026-11-05 | Logs are a fixed standard length; tree length sets log count | 8 |
| 2026-11-05 | MVP-2 = houses and foraging with food storage | 11 |
| 2026-11-05 | Storage modules pool per operation, 20 logs each | 7 |
| 2026-11-05 | Roads and area drawing come first, as MVP-0.5 | 4, 11 |
| 2026-11-05 | Standard start gives about 20 logs; starting materials set opening difficulty (getting stuck is allowed, see newer entry) | 6 |
| 2026-11-05 | Building areas never overlap; freeform polygons, base 4 points, snap to roads | 3 |
| 2026-11-05 | Roads are freeform, follow contours, and have toggleable snapping | 4 |
| 2026-11-05 | Three upgrade types: add-on modules, plot-dependent upgrades, in-place upgrades | 5 |

## 14. Glossary

| Term | Meaning |
| --- | --- |
| Building area | A player-drawn, fenced polygon holding an operation's modules; never overlaps other building areas or plots |
| Work area | A separate polygon telling a production building's workers where to work; hidden by default; does not block construction |
| Area operation | A production building made of a building area, a mandatory core module, optional add-ons and a work area |
| Module | A functional part of a building area (e.g. storage yard); designated footprints start square and can be reshaped |
| Core module | A module the operation cannot work without (e.g. woodcutting hut) |
| Plot / burgage | A road-fronted area for a house and backyard; its size limits later upgrades |
| Family | A household; the unit the player assigns to workplaces |
| Oxen pool | All of the settlement's oxen, available to any hauling job |
| Camp-site | The pre-placed start location by the trunk road, with the starting stockpile and an oxen stand |
| Trunk road | A pre-placed road on the map that player roads can join |
| Pool | The shared inventory of all storage modules in one operation |
| Transport class | Item property deciding how it moves: heavy items by ox only, light items by hand |
| Bucking | Cutting a trunk into standard-length logs |
| Stage | One step in a data-defined process (felling, construction), which can be enabled or disabled |
| Starting stockpile | The free storage point at the camp-site holding starting materials |
| Citizen | A persistent simulated person within a family, independent of their visual model |
