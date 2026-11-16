# Markets and stall families (M15)

How granary food reaches households. Completes MVP-2. Builds on `gdd-amendments.md` MKT-01..08 and FOOD-A6..A10
(those win where this file is silent) and on `food.md` (larders, the Sunday trip, eating).
Owner, 16/11/2026: the **full stall-family system** (MKT-03b/c), not the MKT-09 fallback. It starts once the granary
can take families from its panel (T-450).

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## The loop in one line

Foragers fill their hut store. Granary families collect it into the granary and carry it by hand to their stall at the
town's one market during the week. On Sunday, households in range are allotted food from the stalls and fetch it home.

## Placing a market

| ID | Rule | Tag |
| --- | --- | --- |
| MK-01 | Build bar **Market** draws an `area.market` with the area tool (4–8 points, bends, snapping). Free, no cost gate (MKT-03) | DECIDED |
| MK-02 | Area **100–2,000 m²** (PLACEHOLDER). Too small: *Too small for a market*. Same overlap, steepness and road-crossing refusals as other areas. No road frontage needed | PROPOSED |
| MK-03 | A market area is walkable and does not block pathing; its stalls do not block either (open tables, FG-06) | PROPOSED |
| MK-04 | **One marketplace per town** (owner 16/11/2026; overrides MKT-01's "several" for now). Until towns exist the whole settlement is one town, so a second market is refused: *Only one market per town*, and the build bar entry is disabled with that reason. Deleting the market removes its stalls; food on them drops as a ground pile at each stall (CN-55) | DECIDED (refusal text PROPOSED) |
| MK-05 | **Stall slots:** designating the market subdivides it into a simple grid of stall slots, shown as outlines. Each slot is a cell of **4 × 5 m** (3 × 2 m stall, 1 m side gap, 3 m aisle in front, PLACEHOLDER), aligned to the area's longest edge, kept only where the whole cell lies inside the polygon. The slot count is the market's stall capacity. Fewer than 1 slot: *Too small for a market*. Set dressing comes later | DECIDED (grid sizes PLACEHOLDER) |

## Linking stores to markets

| ID | Rule | Tag |
| --- | --- | --- |
| MK-10 | The granary panel has **Supplies market: Yes / No** (a toggle; with one market there is nothing to pick). A market can be supplied by many granaries (MKT-02) | PROPOSED |
| MK-11 | A new granary supplies nothing until switched on. Only built granaries can supply, and only once a market exists | PROPOSED |
| MK-12 | Forager hut stores never supply a market (production stores, FD-23b). Their food reaches the market through a granary (MK-24, decision 0044) | DECIDED |

## Stalls

| ID | Rule | Tag |
| --- | --- | --- |
| MK-20 | Each family assigned to a **linked** granary gets **one stall** at that market (MKT-03b). Assigning the family, or linking a granary that already has families, queues the stall as an ordinary construction site | DECIDED |
| MK-21 | Stall: **3 × 2 m** footprint, recipe **1 log, 60** worker-minutes (MKT-03, PLACEHOLDER until planks). Built on the **free slot nearest the market centre** (MK-05). All slots taken: the family shows *No room at the market* and does nothing until a slot frees | DECIDED (owner 16/11/2026; values PLACEHOLDER) |
| MK-22 | A stall holds **30 food items** (MKT-08, PLACEHOLDER). Food only | DECIDED (PLACEHOLDER) |
| MK-23 | When the family is unassigned or the link changes, its stall becomes **unstaffed**: it keeps its food (still sold on Sunday) and is reused by the next family that needs a stall at that market. The player can deconstruct a stall from the market panel (MK-23b) | PROPOSED |
| MK-23b | **Deconstructing a stall** is offered only for an **unstaffed** stall; a staffed one shows *Staffed by <family>: unassign them first*, so StallSystem never rebuilds a stall the player just removed. It follows CN-50..57 (mark, then work): refund round down(1 log × 50%) = **0 logs**, food on it drops as a ground pile at the stall (PL-15), and its slot becomes free | DECIDED (owner 16/11/2026, for now; to be finalised later) |

## Store-family work (weekdays and Sunday)

| ID | Rule | Tag |
| --- | --- | --- |
| MK-24 | **Only store families move goods from production into stores** (owner 16/11/2026, decision 0044). During the working day each idle working member of a granary family (age **8+**: hand carrying is light work, POP-A3) picks, in order: (1) **stock the stall**: if the granary supplies the market, its stall has room and the granary has food, take up to **10 items** (MKT-03e) in proportion to granary stock (MKT-03d), walk to the stall, put them on it; (2) **collect from foragers**: if the granary has room and any forager hut store (or, after the handover, the camp food store, CMP-21) has food, fetch up to 10 items from the fullest one (ties: nearest) into the granary; (3) otherwise mill about. Step 2 works with no market at all | DECIDED (order PROPOSED) |
| MK-25 | Food in a carrier's hands is real ledger stock (FOOD-A7). Load and unload take **10 min** each (`tuning.md`) | DECIDED (time PLACEHOLDER) |
| MK-26 | Store families **work on Sundays** (MKT-03e); everyone else rests | DECIDED |
| MK-27 | A store family's reservations never take food already allotted to a household | PROPOSED |

## Sunday allotment and trip with markets

| ID | Rule | Tag |
| --- | --- | --- |
| MK-30 | The camp is **not a marketplace**: it is the stop-gap supply for its own food store only (FD-12, FOOD-A10b), **until the handover** (`camp.md` CMP-20..25) | DECIDED |
| MK-31 | **Before the handover** every household draws from the camp, if in camp range (as M14). Stalls only appear once a granary supplies the market (MK-20), which is also the handover, so the **first Sunday after switching on may be short** if the stalls aren't built and stocked yet; the switch-on confirm warns the player. **After the handover** (a built granary supplies the market) every household draws from the market if in range, otherwise gets nothing (MKT-05). There is never a mix of the two (owner 16/11/2026) | DECIDED |
| MK-32 | A market's stock is the food on its stalls (MKT-03c); granary stock never counts. The allotment reserves item by item on the stalls, fullest stall first, mixed in proportion (FOOD-A8). Households are served nearest first (FOOD-A6) | DECIDED |
| MK-33 | The trip walks to the supply point: the household's adult walks to the nearest reserved stall, collects from every reserved stall, then walks home | PROPOSED |
| MK-34 | Range is a walking distance (MKT-06, 630 m) from the market area's centre point to the house access point | PROPOSED |

## What the player sees

| ID | Rule | Tag |
| --- | --- | --- |
| MK-40 | **Market panel** (click the area or a stall): name "Market <id>", food on stalls by type and total, stalls built / slots (family or *Unstaffed*, items on it), supplying granaries, households served last Sunday and how many were short, Deconstruct | PROPOSED |
| MK-44 | The **house panel** shows **Market access: Yes / No** (owner 16/11/2026). Before the handover, a house in camp range reads *No (camp supplies)* | DECIDED (camp wording PROPOSED) |
| MK-41 | **Range preview:** while placing or when selected, houses in range are tinted green and out-of-range houses red. A ground overlay of reachable land (MKT-06) is later | PROPOSED |
| MK-42 | Citizen panel task lines: *Carrying food to stall*, *Fetching food from forager camp* | PROPOSED |
| MK-43 | Stalls draw as a placeholder table; food on a stall shows as a few coloured boxes (placeholder, see `placeholder-art.md`) | PROPOSED |

## Not in M15

Storage yards and non-food goods on stalls (MKT-03d later), market upgrades (MKT-04), pack stations (MKT-03f), the
reachable-ground overlay, spoilage (FOOD-A11), containers (CNT-*), moving the camp meat into a granary (FD-12 later),
towns and several markets (MKT-01, MKT-07), stall set dressing.

> Thought item (owner 16/11/2026, not to implement): citizens may have a hand-carry amount that baskets and carts raise
> (see CNT-02, CNT-07 in `gdd-amendments.md`).
