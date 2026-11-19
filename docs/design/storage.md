# Storage yard and general goods (M18)

Where goods that are not logs or food live. Owner decisions 17/11/2026. M18, after planks and before the materials buildings (`future-goals.md`). INV-A1 categories: Log, Food, General.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Stores

| ID | Rule | Tag |
| --- | --- | --- |
| ST-01 | Each production building keeps a **small store of its own goods** (sawpit 20 planks, coppice hut 40 of each rod, thatcher's hut sheaves). Producers never deliver to the camp | DECIDED (owner 17/11/2026) |
| ST-02 | The camp holds two stores: **Food stuffs** (exists) and **General goods** (new: starting planks, thatch, panels, baskets) | DECIDED (owner 17/11/2026) |
| ST-03 | The **storage yard** is a building that stores General goods (footprint, recipe and capacity PLACEHOLDER: 8 × 6 m, 8 logs, **300 items**, raised from 100 so the starting planks, thatch, panels and daub (10 + 90 + 90 + 90 by M21) fit in one yard, see ST-08) | DECIDED (numbers PROPOSED) |
| ST-04 | Once a storage yard is built, **everything in the camp's General store is carried to it** and the camp General store closes. Carried by hand, light items | DECIDED (owner 17/11/2026; hand carry PROPOSED) |
| ST-05 | Who carries goods to and from the yard: families assigned to it, like granary families (2 family slots) | PROPOSED |
| ST-07 | The yard stores **beams** (and other General goods). It **never stores logs**; logs stay in log storage and ground piles. Sawpits fetch beams from the yard as well as from hewing stations (PL-04) | DECIDED (owner 17/11/2026) |
| ST-06 | Construction takes planks and other General items from the nearest store with stock (production store, camp or yard) | PROPOSED |
| ST-08 | Capacity is **one total across all items** (a mix of planks, beams, thatch...). No accept list in M18: the yard takes every General item | PROPOSED |
| ST-09 | With several yards, goods go to the **nearest yard with room** | PROPOSED |

## Yard families (M18)

The yard is a granary for General goods. Its families plan trips with the same rules as granary families (`markets.md`, decision 0044), in this order of jobs each time one is idle:

| ID | Rule | Tag |
| --- | --- | --- |
| ST-40 | **1. Camp first:** while the camp General store holds goods, empty it into the yard as room allows. If the yard fills, the rest waits where it is (in the camp store, or in the dropped pile after ST-31) | PROPOSED |
| ST-41 | **2. Loose goods:** pick up unowned General ground piles (dropped kit ST-23, camp leftovers ST-31, deconstruction refunds) | PROPOSED |
| ST-42 | **3. Workshop output:** collect only from **workshops** (a building with an input and an output: hewing station, sawpit, later weaver and wattle yard) as soon as the output store holds **any free output item** (`YardCollectThreshold` 0; no half-full rule). Fullest store first. Owner 18/11/2026 rejected the half-full rule | DECIDED (owner 17/11/2026: workshops only; owner 18/11/2026: any output) |
| ST-42b | Gathering huts (coppice hut, thatcher's hut) and the daub pit (no input) are **not** collected. Workshops and sites fetch from their stores directly | DECIDED (owner 17/11/2026) |
| ST-43 | **Loads:** light items by hand, a per-item load from tuning (planks 2, rods 10, baskets 5, panels 2, thatch 1). **Beams by ox** (pool ox, 1 per trip, PL-21), same ox priority as workshops (construction first, HW-06) | PROPOSED |
| ST-44 | Reserved stock (for a site or another fetch) is never collected. Collected goods are reserved on the yard's capacity before the trip starts, so a full yard means no trip | PROPOSED |
| ST-45 | Panel status: No workers, Moving camp goods, Collecting, Yard full, Idle | PROPOSED |

> Implementation note (ST-45): the code has seven statuses, checked in this order: Under construction, No workers,
> Waiting for ox (a beam trip has no free ox), Moving camp goods, Collecting, Yard full, Idle. Under construction and
> Waiting for ox are not in this table. The owner decides whether to keep them.
>
> Implementation note (ST-43): thatch has no item yet, so its hand load (1) is not in `HandLoads`.
>
> Implementation note (ST-42, ST-21): the yard collects the weaver's baskets and the wattle yard's panels (T-562), and
> `KitFetchSystem` claims baskets from the weaver store too. ST-42b holds: coppice huts are never collected.

## Workstation kit (baskets and other tools), built in M19

| ID | Rule | Tag |
| --- | --- | --- |
| ST-20 | A production or gathering building that uses a kit item (baskets now; tools later) has a **local kit store** with a fixed number of slots per item (e.g. 5 baskets) | DECIDED (owner 17/11/2026; slot counts PLACEHOLDER) |
| ST-21 | It **claims** kit from General goods (camp store, storage yard, or the weaver's own store) until its slots are full or none are left. Example: 10 baskets in the village, a building with 5 slots takes 5 | DECIDED (owner 17/11/2026) |
| ST-22 | Its own workers fetch the claimed kit by hand, nearest store first. Claimed kit **stays** at the building | PROPOSED (who fetches) |
| ST-23 | If the building is deconstructed or deleted, its kit is **dropped on the ground** there, and storage yard families collect it | DECIDED (owner 17/11/2026) |
| ST-24 | When several buildings want the same kit, the oldest claim is served first | PROPOSED |
| ST-25 | A worker starting a carry trip **takes a free basket** from the kit store for that trip (one basket per trip, returned when the trip ends). More trips than baskets: the rest carry the normal load | PROPOSED |

## Market stalls

| ID | Rule | Tag |
| --- | --- | --- |
| ST-10 | A storage yard has a **Supplies market** toggle, like the granary. It is **shown greyed out** (*No demand yet*) until households buy General goods. Level 1 households need only food | DECIDED (owner 17/11/2026) |

## Camp

| ID | Rule | Tag |
| --- | --- | --- |
| ST-30 | The camp **stays** while its General store holds goods and no storage yard exists, even if everyone is housed and its food is gone | DECIDED (owner 17/11/2026) |
| ST-31 | When the camp's removal conditions are met (everyone housed, food handed over) **and** a storage yard exists, the camp is removed and its General goods are **dropped on the ground** (like the leftover pile, T-467). Storage yard families collect them | DECIDED (owner 17/11/2026) |
| ST-32 | When the camp would be removed (everyone housed, food handed over) but stays only because its General store holds goods and no yard exists, the message log says once: *The camp stays until a storage yard can take its goods* | PROPOSED (after plan review 18/11/2026) |

Not decided: an accept list per item (GDD logistics settings), not in M18 (ST-08).
