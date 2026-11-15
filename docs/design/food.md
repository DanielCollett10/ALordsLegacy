# Food on the table (M14)

Foraging, the granary, larders, eating and hunger. Builds on `gdd-amendments.md` FOOD-A1..A11 (those rules win where
this file is silent). Markets with stall families are **M15**; in M14 the camp-site is the only market (FOOD-A10b).

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Food items and stock

| ID | Rule | Tag |
| --- | --- | --- |
| FD-01 | Food items: **meat** (3 rations), **berries** and **mushrooms** (1 ration per basket) (FOOD-A1, A1b). Adults need 1 ration a day, children under 14 need 0.5 | DECIDED (values PLACEHOLDER) |
| FD-02 | Food lives in the same ledger as logs, by item type, so food and logs never mix (MVP-2-04). Decision 0038 | PROPOSED |
| FD-03 | Start: **400 meat** in the camp food store (FOOD-A1b) | PLACEHOLDER |
| FD-04 | Top bar: **Food: N days** (village food ÷ daily need, FOOD-A3), raw counts in the tooltip | PROPOSED |

## Larders, the weekly allotment and eating

| ID | Rule | Tag |
| --- | --- | --- |
| FD-10 | Every house has a larder; every Homeless family has a camp larder at its tent (FOOD-A5) | DECIDED |
| FD-11 | **Camp market:** the camp-site is a market with a range of **630 m walking** (MKT-06). Households outside it get nothing (MKT-05) | DECIDED (range PLACEHOLDER) |
| FD-12 | **Bridge until M15:** the camp market's stock is the camp food store **plus every granary inside its range**. M15 replaces this with stall families | PROPOSED (needs owner OK) |
| FD-13 | **Sunday dawn allotment** (FOOD-A6, A8): nearest households first, each topped up to 7 days of need, mixed in proportion to what's in stock, reserved where it lies | DECIDED |
| FD-14 | **Sunday trip** (FOOD-A7): one adult per household walks to where its allotment is reserved (camp store or a granary), collects it in one go, walks home and puts it in the larder. Trips start spread over the morning. No carry cap on this trip in M14 | DECIDED (no cap PROPOSED) |
| FD-15 | **Daily eating** at midnight (FOOD-A9): each household eats its need from its larder, whole items first, fraction carried. Short → **Hungry** that day | DECIDED |
| FD-16 | **Hungry** citizens work at **half speed** (task step time × 2). No deaths or leaving (FOOD-A2) | PROPOSED (value PLACEHOLDER) |
| FD-17 | No spoilage in M14 (FOOD-A11 later) | PROPOSED |

## Foraging camp

| ID | Rule | Tag |
| --- | --- | --- |
| FD-20 | **Forage bushes** are Sim records scattered at new-game time with the seeded random, in clearings and forest edges (density PLACEHOLDER). The map file is not re-baked, so old maps and saves stay valid | PROPOSED |
| FD-21 | A bush holds **3 baskets** (berries or mushrooms by bush kind). Picked empty, it regrows to full after **14 days** (FOOD-06; no seasons yet) | PLACEHOLDER |
| FD-22 | The foraging camp is an area operation (building-tool.md): core **forager hut** (4 logs, 480 min, PLACEHOLDER), no storage module. Work area: default circle, 300 m (PLACEHOLDER), or drawn (WC-12) | PROPOSED |
| FD-23 | Foraging is **light work** (POP-A3): family members **8+** forage (the first light step, POP-A2) | PROPOSED |
| FD-24 | A forager picks the nearest bush with baskets left (30 min per basket, PLACEHOLDER), carries up to **10 items** by hand (MKT-03e), then walks to the nearest food store with room: a built granary, else the camp food store | PROPOSED |

## Granary

| ID | Rule | Tag |
| --- | --- | --- |
| FD-30 | Fixed footprint, placed alone with no area (GDD section 5): **8 × 6 m**, capacity **100 items** (FOOD-04), recipe **6 logs, 600** min (CON-A1) | PLACEHOLDER |
| FD-31 | Panel: stock by type and capacity, state, Deconstruct (contents become a ground pile at the site, CN-55) | PROPOSED |
