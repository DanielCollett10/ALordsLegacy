# Food on the table (M14)

Foraging, the granary, larders, eating and hunger. Builds on `gdd-amendments.md` FOOD-A1..A11 (those rules win where
this file is silent). Markets with stall families are **M15**; in M14 the camp-site is the only market (FOOD-A10b).

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Food items and stock

| ID | Rule | Tag |
| --- | --- | --- |
| FD-01 | Food items: **meat** (3 rations), **berries** and **mushrooms** (1 ration per basket) (FOOD-A1, A1b). Adults need 1 ration a day, children under 14 need 0.5 | DECIDED (values PLACEHOLDER) |
| FD-02 | Food lives in the same ledger as logs, by item id (the ledger already keys by item, `Items`), so food and logs never mix (MVP-2-04). A log storage only takes logs; a granary and a larder only take food | PROPOSED |
| FD-03 | Start: **400 meat** in the camp food store (FOOD-A1b) | PLACEHOLDER |
| FD-04 | Top bar: **Food: N days** (village food ÷ daily need, FOOD-A3), raw counts in the tooltip | PROPOSED |

## Larders, the weekly allotment and eating

| ID | Rule | Tag |
| --- | --- | --- |
| FD-10 | Every house has a larder; every Homeless family has a camp larder at its tent (FOOD-A5) | DECIDED |
| FD-11 | **Camp market:** the camp-site is a market with a range of **630 m walking** (MKT-06). Households outside it get nothing (MKT-05) | DECIDED (range PLACEHOLDER) |
| FD-12 | **The camp is only a stop-gap.** The camp market sells only what is in the camp food store (the starting meat). Granaries never refill it. Granary food reaches households once markets exist (M15). Later, a granary will take over what is stored at the camp | DECIDED (owner 15/11/2026) |
| FD-13 | **Sunday dawn allotment** (FOOD-A6, A8): nearest households first, each topped up to 7 days of need, mixed in proportion to what's in stock, reserved where it lies | DECIDED |
| FD-14 | **Sunday trip** (FOOD-A7): one adult per household walks to the camp food store, collects it in one go, walks home and puts it in the larder. Trips start spread over the morning. No carry cap on this trip in M14 | DECIDED (no cap PROPOSED) |
| FD-15 | **Daily eating** at midnight (FOOD-A9): each household eats its need from its larder, whole items first, fraction carried. Short → **Hungry** that day | DECIDED |
| FD-16 | **Hungry** is a status the player sees. It has **no effect on work** for now (overrides FOOD-A2's lower output). A happiness modifier may come later. No deaths or leaving | DECIDED (owner 15/11/2026) |
| FD-17 | No spoilage in M14 (FOOD-A11 later) | PROPOSED |

## Foraging camp

| ID | Rule | Tag |
| --- | --- | --- |
| FD-20 | **Forage bushes** are Sim records scattered at new-game time with the seeded random, in clearings and forest edges (density PLACEHOLDER). The map file is not re-baked, so old maps and saves stay valid | PROPOSED |
| FD-21 | A bush holds **3 baskets** (berries or mushrooms by bush kind). Picked empty, it regrows to full after **14 days** (FOOD-06; no seasons yet) | PLACEHOLDER |
| FD-22 | The foraging camp is an area operation (building-tool.md): core **forager hut** (8 logs, 480 min; logs DECIDED owner 15/11/2026, minutes PLACEHOLDER), no storage module. Work area: default circle, 300 m (PLACEHOLDER), or drawn (WC-12) | PROPOSED |
| FD-23 | Foraging is **light work** (POP-A3): family members **8+** forage (the first light step, POP-A2) | DECIDED (owner 15/11/2026; age PLACEHOLDER) |
| FD-23b | **A forager camp stores food locally.** The forager hut holds **30 items** (PLACEHOLDER; food only). A forager camp works with no granary at all. When its store and every reachable granary are full it shows *Storage full* and stops gathering | DECIDED (owner 15/11/2026; storing in the hut rather than a separate module PROPOSED) |
| FD-24 | A forager picks the nearest bush with baskets left (30 min per basket, PLACEHOLDER), carries up to **10 items** by hand (MKT-03e), then walks to the **nearest store with room**: its own hut store or any built granary, whichever is nearer. Foragers never deliver to the camp (FD-12) | PROPOSED |

## Granary

| ID | Rule | Tag |
| --- | --- | --- |
| FD-30 | A separate building with a **fixed size**, placed as one piece with no drawn area (GDD section 5). Building **8 × 6 m**, capacity **100 items** (FOOD-04), recipe **8 logs, 600** min (CON-A1) | DECIDED (owner 15/11/2026; values PLACEHOLDER) |
| FD-30b | The granary's **plot is the building plus 5 m on every side** (18 × 16 m for an 8 × 6 m building). The whole plot must be clear of other footprints, areas and roads, and clears trees. Only the building blocks walking; the 5 m yard is walkable. The placement ghost shows both building and yard. It rotates with Q/E like a module | DECIDED (owner 15/11/2026: 5 m; blocking and rotation PROPOSED) |
| FD-31 | Panel: stock by type and capacity, state, Deconstruct (contents become a ground pile at the site, CN-55) | PROPOSED |
