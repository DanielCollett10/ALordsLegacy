# Building materials: coppice, thatch, wattle and baskets

Gathering and production buildings for the tier-1 house materials. Owner brief and answers 17/11/2026. Milestones
after Growth, Planks and the Storage yard (`future-goals.md`). Builds on GDD CON-14..19, HSE-21..23, POP-A3 (light work), CNT-01..10.

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
| MAT-14 | The coppice hut stores up to **40 willow rods and 40 hazel rods** | DECIDED (owner 17/11/2026) |

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
| MAT-34 | Reed and rush are cut at **full rate in winter** (Dec–Feb). Outside winter, rush is cut at **1/4 of the winter rate** (each cut takes 4× as long), a trickle that never matches winter. Curing and collecting run in any season | DECIDED (owner 17/11/2026; spring/autumn = summer rate PROPOSED) |
| MAT-35 | **1 sheaf = 1 bundle.** Each bundle cures **7 days** where it was set up. A worker carries **1 sheaf per trip** on the shoulder. A cart takes 5 (carts not built yet). Cut minutes per sheaf PLACEHOLDER | DECIDED (owner 17/11/2026) |
| MAT-36 | A new game starts with **90 thatch sheaves** at the camp (9 houses at 10 each) | DECIDED (owner 17/11/2026) |
| MAT-37 | Thatcher families **stay assigned all year** (slow cutting outside winter). Seasonal reassignment is a later feature | DECIDED (owner 17/11/2026) |
| MAT-38 | The thatcher's hut keeps a small store of sheaves (capacity PLACEHOLDER, e.g. 40) | DECIDED (owner 17/11/2026; amount PROPOSED) |

## Houses

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-40 | A house costs **10 logs + 10 thatch sheaves + 10 wattle panels** (placeholder; replaces HM-06's 10 logs) | DECIDED (owner 17/11/2026) |
| MAT-41 | Wattle panels also build the plot's perimeter fence (panels by perimeter length, OPEN), which is **not** required to move in | DECIDED (fence with tier-1 houses) |
| MAT-42 | Daub (earth or clay) and household levels 1–2 come with tier-1 houses (HSE-20..23) | DECIDED (M19) |

## Baskets

| ID | Rule | Tag |
| --- | --- | --- |
| MAT-50 | A carrier with a willow basket carries **twice as much** food or forage per trip (simple CNT-02) | DECIDED (owner 17/11/2026) |
| MAT-51 | Baskets **do not wear out** yet. Made at the weaver, they go to general goods: the camp's general store, or the storage yard once one is built (`storage.md`) | DECIDED (owner 17/11/2026) |
| MAT-52 | A carrier takes one basket from the nearest general store when its family is assigned to foraging or a granary, keeps it while assigned, and returns it on unassign | PROPOSED |

## Gaps (17/11/2026)

- **G-1 Pacing:** answered by MAT-34 (summer trickle) and MAT-36 (90 starting sheaves).
- **G-2 Storage:** each building keeps a small store of its own goods (MAT-14, MAT-38, PL-06); general goods go to the
  storage yard (`storage.md`).
- **G-3 Delivery:** construction only takes logs by ox today. Multi-item recipes and hand delivery come in M17 (PL-12).
- **G-4 Wattle yard:** answered: panels are in the house recipe (MAT-40).

## Art notes (future, not gameplay)

- Outdoor stacks: large piles of cured straw, reed and hazel rods, covered with old thatch or turf to shed rain.
- Tool storage: a lean-to or part of a cottage with ladders, wooden mallets (leggatts), billhooks and shears.
- Work yard: open space for soaking and twisting hazel into spars and straw into rope (hey-bands).

## Ideas later (not scheduled)

- **Spars**: U-shaped hazel pins that peg thatch down. A link between coppice hut and thatcher (spars in the house recipe).
- **Hey-bands**: straw rope twisted in the thatcher's yard.
- More links between thatcher, wattle yard, weaver and coppice hut.
