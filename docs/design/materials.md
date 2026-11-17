# Building materials: coppice, thatch, wattle and baskets (M18)

Gathering and production buildings for the tier-1 house materials. Owner brief and answers 17/11/2026. M18, after
M16 Growth and M17 Planks (`future-goals.md`). Builds on GDD CON-14..19, HSE-21..23, POP-A3 (light work), CNT-01..10.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Items

| ID | Item | Made by | Used for | Tag |
| --- | --- | --- | --- | --- |
| MAT-01 | **Willow rods** and **hazel rods** (coppice, cut sticks) | Coppice hut | Weaver, wattle yard | DECIDED |
| MAT-02 | **Thatch sheaf** (a bundle of reed or rush) | Thatcher's hut | House recipe (MAT-40) | DECIDED |
| MAT-03 | **Willow basket**, **willow box** (more later) | Weaver | Baskets: MAT-50. Box: no use yet | DECIDED |
| MAT-04 | **Wattle panel / fence** (more later) | Wattle yard | Plot perimeter fence; not needed for a house to be lived in | DECIDED |
| MAT-05 | All are General category (INV-A1), carried by hand (light) | | | PROPOSED |

## Coppice hut

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-10 | A work-area operation like the woodcutter: a drawn polygon (collection area) plus the **coppice hut** as main building | DECIDED |
| MAT-11 | Workers gather willow and hazel **from the wild, nearest first**, inside the area | DECIDED |
| MAT-12 | **Dedicated coppice fields** (planting hazel or willow) are not built now; revisit with farms and fields | DECIDED (future) |
| MAT-13 | They cut from **new hazel and willow stools**, scattered on the map like forage bushes. A cut stool regrows (like `BushRegrowSystem`); trees are untouched | DECIDED (owner 17/11/2026) |

## Weaver and wattle yard

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-20 | **Weaver**: a static-shape building (no work area). Turns willow rods into willow baskets and willow boxes | DECIDED |
| MAT-21 | **Wattle yard**: turns hazel and willow rods into wattle panels / fences | DECIDED |
| MAT-22 | Recipes (rods per basket, box, panel; minutes) | OPEN |

## Thatcher's hut

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-30 | Workers cut **water reed** or **river rush** from reed beds. Until Gaea maps with lowland and wetness masks (FG-07), beds are **scattered on low, flat ground** like bushes | DECIDED (owner 17/11/2026) |
| MAT-31 | A **grown grain** (straw, chaff from threshing) toggle on the hut panel, **shown greyed out: "Needs farming"** until fields exist. Straw is a summer source | DECIDED (owner 17/11/2026) |
| MAT-32 | Cut stalks are tied into bundles and **set upright on the bank to dry**. After a curing time the workers go back, collect the dry sheaves and carry them **home to the hut store** | DECIDED |
| MAT-33 | Dry sheaves are stored in **ricks** (conical piles) or open-sided shelters so they stay dry | DECIDED (art and storage detail OPEN) |
| MAT-34 | Reed and rush are cut **in winter only**. Curing and collecting may run past winter's end | DECIDED (owner 17/11/2026) |
| MAT-35 | Curing time, sheaves per bundle, carry amount | OPEN |
| MAT-36 | A new game starts with **20 thatch sheaves** at the camp | DECIDED (amount: see gap G-1) |
| MAT-37 | What a thatcher family does outside winter (odd jobs like unassigned families, or idle) | OPEN |

## Houses

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-40 | A house costs **10 logs + 10 thatch sheaves** (placeholder; replaces HM-06's 10 logs) | DECIDED |
| MAT-41 | Wattle panels build the plot's perimeter fence, which is **not** required to move in | DECIDED |
| MAT-42 | Daub (earth or clay) and household levels 1–2 come with tier-1 houses (HSE-20..23) | DECIDED (M19) |

## Baskets

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-50 | A carrier with a willow basket carries **twice as much** food or forage per trip (simple CNT-02) | DECIDED (owner 17/11/2026) |
| MAT-51 | Who takes a basket, from where, and whether baskets wear out (CNT-06) | OPEN |

## Gaps found 17/11/2026 (need answers before M18 tasks)

- **G-1 Pacing:** the game starts on 1 March and reed is winter-only, so the first harvest is 9 game months away
  (about 135 real hours at 1×). 20 starting sheaves house only 2 of the 3 start families, and growth (M16) stops.
- **G-2 Storage:** only log storage and food stores exist. Rods, sheaves, baskets and panels need a home (hut stores,
  or the greyed-out storage yard).
- **G-3 Delivery:** construction only takes logs by ox today. A two-material recipe needs hand delivery of light items
  (M17 builds the multi-material recipe for planks first).
- **G-4 Wattle yard:** panels have no use until the fence (M19). Build it in M18 anyway, or in M19 with the fence.

## Art notes (future, not gameplay)

- Outdoor stacks: large piles of cured straw, reed and hazel rods, covered with old thatch or turf to shed rain.
- Tool storage: a lean-to or part of a cottage with ladders, wooden mallets (leggatts), billhooks and shears.
- Work yard: open space for soaking and twisting hazel into spars and straw into rope (hey-bands).

## Ideas later (not scheduled)

- **Spars**: U-shaped hazel pins that peg thatch down. A link between coppice hut and thatcher (spars in the house recipe).
- **Hey-bands**: straw rope twisted in the thatcher's yard.
- More links between thatcher, wattle yard, weaver and coppice hut.
