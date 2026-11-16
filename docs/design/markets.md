# Markets and stall families (M15)

How granary food reaches households. Completes MVP-2. Builds on `gdd-amendments.md` MKT-01..08 and FOOD-A6..A10
(those win where this file is silent) and on `food.md` (larders, the Sunday trip, eating).
Owner, 16/11/2026: the **full stall-family system** (MKT-03b/c), not the MKT-09 fallback. It starts once the granary
can take families from its panel (T-450).

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## The loop in one line

Foragers fill the granary. The granary's families carry food by hand to their stall at a market during the week.
On Sunday, households in range are allotted food from that market's stalls and fetch it home.

## Placing a market

| ID | Rule | Tag |
| --- | --- | --- |
| MK-01 | Build bar **Market** draws an `area.market` with the area tool (4–8 points, bends, snapping). Free, no cost gate (MKT-03) | DECIDED |
| MK-02 | Area **100–2,000 m²** (PLACEHOLDER). Too small: *Too small for a market*. Same overlap, steepness and road-crossing refusals as other areas. No road frontage needed | PROPOSED |
| MK-03 | A market area is walkable and does not block pathing; only its stalls do not block either (open tables, FG-06) | PROPOSED |
| MK-04 | Several markets may exist (MKT-01). Deleting a market area removes its stalls; food on them drops as a ground pile at each stall (CN-55) | PROPOSED |

## Linking stores to markets

| ID | Rule | Tag |
| --- | --- | --- |
| MK-10 | The granary panel has **Supplies: <market> / None** with a pick-on-map button. A granary supplies **at most one market**; a market can be supplied by many granaries (MKT-02) | PROPOSED |
| MK-11 | A new granary supplies nothing until linked. Only built granaries can be linked | PROPOSED |
| MK-12 | Forager hut stores are **not** linkable (they are production stores, FD-23b). Their food reaches markets through a granary (MK-24) | PROPOSED |

## Stalls

| ID | Rule | Tag |
| --- | --- | --- |
| MK-20 | Each family assigned to a **linked** granary gets **one stall** at that market (MKT-03b). Assigning the family, or linking a granary that already has families, queues the stall as an ordinary construction site | DECIDED |
| MK-21 | Stall: **3 × 2 m** footprint, recipe **1 log, 60** worker-minutes (MKT-03, PLACEHOLDER until planks). Auto-placed in free space inside the market area, the first free spot nearest the area's centre on a 1 m search grid. No room: the family shows *No room at the market* and does nothing until room exists | PROPOSED |
| MK-22 | A stall holds **30 food items** (MKT-08, PLACEHOLDER). Food only | PROPOSED |
| MK-23 | When the family is unassigned or the link changes, its stall becomes **unstaffed**: it keeps its food (still sold on Sunday) and is reused by the next family that needs a stall at that market. The player can deconstruct a stall from the market panel | PROPOSED |

## Store-family work (weekdays and Sunday)

| ID | Rule | Tag |
| --- | --- | --- |
| MK-24 | During the working day each idle working member of a store family (age **8+**: hand carrying is light work, POP-A3) picks, in order: (1) **stock the stall**: if its stall has room and the granary has food, take up to **10 items** (MKT-03e) in proportion to granary stock (MKT-03d), walk to the stall, put them on it; (2) **fill the granary**: if the granary has room and a forager hut store within **market range** (630 m walking) has food, fetch up to 10 items from the fullest one into the granary; (3) otherwise mill about | PROPOSED |
| MK-25 | Food in a carrier's hands is real ledger stock (FOOD-A7). Load and unload take **10 min** each (`tuning.md`) | DECIDED (time PLACEHOLDER) |
| MK-26 | Store families **work on Sundays** (MKT-03e); everyone else rests | DECIDED |
| MK-27 | A store family's reservations never take food already allotted to a household | PROPOSED |

## Sunday allotment and trip with markets

| ID | Rule | Tag |
| --- | --- | --- |
| MK-30 | The camp stays a market for its own food store (FD-12, FOOD-A10b). Built markets join it in the allotment | DECIDED |
| MK-31 | At Sunday dawn, each household tries the markets whose range covers it **nearest first**, topping up to 7 days of need, until full or no market in range has stock left. Per market, households are still served nearest first (FOOD-A6) | PROPOSED (MKT-01 reads "the nearest market"; this falls back to the next one, so a new empty market never cuts a house off from camp meat) |
| MK-32 | A market's stock is the food on its stalls (MKT-03c); granary stock never counts. The allotment reserves item by item on the stalls, fullest stall first, mixed in proportion (FOOD-A8) | DECIDED |
| MK-33 | The trip walks to the market, not always to the camp: the household's adult walks to the nearest reserved stall, collects from every reserved stall in that market, then from the next market if it has one, then walks home | PROPOSED |
| MK-34 | Range is a walking distance (MKT-06, 630 m) from the market area's centre point to the house access point | PROPOSED |

## What the player sees

| ID | Rule | Tag |
| --- | --- | --- |
| MK-40 | **Market panel** (click the area or a stall): name "Market <id>", food on stalls by type and total, stalls (family or *Unstaffed*, items on it), linked granaries, households served last Sunday and how many were short, Deconstruct | PROPOSED |
| MK-41 | **Range preview:** while placing or when selected, houses in range are tinted green and out-of-range houses red. A ground overlay of reachable land (MKT-06) is later | PROPOSED |
| MK-42 | Citizen panel task lines: *Carrying food to stall*, *Fetching food from forager camp* | PROPOSED |
| MK-43 | Stalls draw as a placeholder table; food on a stall shows as a few coloured boxes (placeholder, see `placeholder-art.md`) | PROPOSED |

## Not in M15

Storage yards and non-food goods on stalls (MKT-03d later), market upgrades (MKT-04), pack stations (MKT-03f), the
reachable-ground overlay, spoilage (FOOD-A11), containers (CNT-*), moving the camp meat into a granary (FD-12 later).
