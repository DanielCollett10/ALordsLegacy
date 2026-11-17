# Storage yard and general goods (M18)

Where goods that are not logs or food live. Owner decisions 17/11/2026. M18, after planks and before the materials
buildings (`future-goals.md`). INV-A1 categories: Log, Food, General.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Stores

| ID | Rule | Tag |
| --- | --- | --- |
| ST-01 | Each production building keeps a **small store of its own goods** (sawpit 20 planks, coppice hut 40 of each rod, thatcher's hut sheaves). Producers never deliver to the camp | DECIDED (owner 17/11/2026) |
| ST-02 | The camp holds two stores: **Food stuffs** (exists) and **General goods** (new: starting planks, thatch, panels, baskets) | DECIDED (owner 17/11/2026) |
| ST-03 | The **storage yard** is a building that stores General goods (footprint, recipe and capacity PLACEHOLDER: 8 × 6 m, 8 logs, 100 items) | DECIDED (numbers PROPOSED) |
| ST-04 | Once a storage yard is built, **everything in the camp's General store is carried to it** and the camp General store closes. Carried by hand, light items | DECIDED (owner 17/11/2026; hand carry PROPOSED) |
| ST-05 | Who carries goods to and from the yard: families assigned to it, like granary families (2 family slots) | PROPOSED |
| ST-07 | The yard stores **beams** (and other General goods). It **never stores logs**; logs stay in log storage and ground piles. Sawpits fetch beams from the yard as well as from hewing stations (PL-04) | DECIDED (owner 17/11/2026) |
| ST-06 | Construction takes planks and other General items from the nearest store with stock (production store, camp or yard) | PROPOSED |

## Workstation kit (baskets and other tools)

| ID | Rule | Tag |
| --- | --- | --- |
| ST-20 | A production or gathering building that uses a kit item (baskets now; tools later) has a **local kit store** with a fixed number of slots per item (e.g. 5 baskets) | DECIDED (owner 17/11/2026; slot counts PLACEHOLDER) |
| ST-21 | It **claims** kit from General goods (camp store or storage yard) until its slots are full or none are left. Example: 10 baskets in the village, a building with 5 slots takes 5 | DECIDED (owner 17/11/2026) |
| ST-22 | Its own workers fetch the claimed kit by hand, nearest store first. Claimed kit **stays** at the building | PROPOSED (who fetches) |
| ST-23 | If the building is deconstructed or deleted, its kit is **dropped on the ground** there, and storage yard families collect it | DECIDED (owner 17/11/2026) |
| ST-24 | When several buildings want the same kit, the oldest claim is served first | PROPOSED |

## Market stalls

| ID | Rule | Tag |
| --- | --- | --- |
| ST-10 | A storage yard has a **Supplies market** toggle, like the granary. It is **shown greyed out** (*No demand yet*) until households buy General goods. Level 1 households need only food | DECIDED (owner 17/11/2026) |

## Camp

| ID | Rule | Tag |
| --- | --- | --- |
| ST-30 | The camp **stays** while its General store holds goods and no storage yard exists, even if everyone is housed and its food is gone | DECIDED (owner 17/11/2026) |
| ST-31 | When the camp's removal conditions are met (everyone housed, food handed over) **and** a storage yard exists, the camp is removed and its General goods are **dropped on the ground** (like the leftover pile, T-467). Storage yard families collect them | DECIDED (owner 17/11/2026) |

## Not decided

- What the yard accepts (an accept list per item, GDD logistics settings) or whether it takes everything.
