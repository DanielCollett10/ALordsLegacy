# Timber chain: hewing station, sawpit, beams and planks (M17)

A production chain that turns one item into another: **logs → hewed beams → planks**. MVP-4 part 1 (GDD section 11).
Owner decisions 17/11/2026. The wider chain and building costs will be refined later.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Hewing station

| ID | Rule | Tag |
| --- | --- | --- |
| HW-01 | The hewing station is **its own building**, placed like the granary (standalone, fixed footprint). Same footprint as the sawpit for now (6 × 4 m), 6 logs + 480 worker-minutes to build | DECIDED (owner 17/11/2026) |
| HW-02 | **1 family slot.** It hews **1 log into 1 hewed beam**, **3 beams per working day per family**, however many members (heavy work, 14+) | DECIDED (owner 17/11/2026) |
| HW-03 | The hewers fetch their own logs by ox (pool oxen) from the **nearest** ground pile or log storage with free logs (as PL-04 was) | PROPOSED (moved from the sawpit) |
| HW-04 | Log stock 6, beam store 20 (PLACEHOLDER). Status lines: No workers, Waiting for logs, Waiting for ox, Store full | DECIDED (owner 17/11/2026) |
| HW-06 | Hewers may take any **unreserved** logs (ground piles, woodcutter piles, log storage, camp stockpile); logs reserved for a construction site are never taken. When a construction delivery and a workshop fetch both want a free pool ox, **construction always goes first**. An ox **assigned** to a building always works that building and is never taken by construction or another building (already true in `OxPool.FindFor`). Teach this in the tutorial (`tutorial-notes.md`) | DECIDED (owner 17/11/2026) |
| HW-05 | **Every rate, ratio and capacity in this file is tuning** (`SimTuning`, mirrored in `SimConfig`) from the first build, so the chain can be rebalanced without code | DECIDED (owner 17/11/2026) |

## Sawpit

| ID | Rule | Tag |
| --- | --- | --- |
| PL-01 | The sawpit is **its own building** with a fixed footprint, placed like the granary. It is **not** a module of the woodcutting operation | DECIDED (owner 17/11/2026) |
| PL-02 | Footprint **6 × 4 m**, recipe **6 logs, 480** worker-minutes, **1 family slot** | DECIDED (owner 17/11/2026; PLACEHOLDER) |
| PL-03 | Sawing is heavy work (POP-A3): workers aged 14+ | PROPOSED |
| PL-04 | The sawyers fetch **beams** (not logs) from the nearest source with free beams: a **hewing station** store, or a **storage yard** once that exists (M18). Carried **by ox, 2 per trip** (pool oxen), like logs. A hauled cart may replace dragging later | DECIDED (owner 17/11/2026) |
| PL-05 | A beam is sawn **in thirds: 1 beam makes 3 planks.** Output is fixed per assigned family, one saw task at a time: **3 beams (9 planks) per working day**, matching one hewing station | DECIDED (owner 17/11/2026; PLACEHOLDER) |
| PL-06 | Planks are General items kept in the sawpit's own store, **up to 20 planks**. Full: *Store full*, sawing stops | DECIDED (owner 17/11/2026) |
| PL-07 | Beam stock 6 (PLACEHOLDER, two days' ox loads). Status lines: No workers, Waiting for beams, Waiting for ox, Store full | PROPOSED |
| PL-08 | A new game starts with **no beams** | DECIDED (owner 17/11/2026) |

## What uses timber

| ID | Rule | Tag |
| --- | --- | --- |
| PL-10 | A market stall costs **2 planks** (its real MKT-03 cost), replacing the 1-log placeholder | DECIDED |
| PL-11 | **Tier 1 house: 5 logs** (was 10), switched **in M20 together with thatch and panels** (MAT-40). Until then houses stay at 10 logs | DECIDED (owner 17/11/2026) |
| PL-11b | **Tier 2 house: 10 beams** instead of logs (plus planks and other items, later). Tier 2 is not built yet | DECIDED (owner 17/11/2026; future) |
| PL-12 | Construction learns **recipes with more than one item** and **hand delivery** for light items (decision 0046) | PROPOSED |
| PL-13 | Stalls already placed keep their log cost; only new stalls cost planks. With too few planks no stall is placed and the granary family shows *Waiting for planks* | PROPOSED |
| PL-14 | A new game starts with **10 planks** at the camp (5 stalls), so the market works before the chain runs | DECIDED (owner 17/11/2026) |
| PL-15 | Until the storage yard (M18), starting planks sit in a new camp **General goods** store (`storage.md` ST-02) | PROPOSED |
| PL-16 | Planks are carried **by hand, 2 per trip**. **Stall planks are carried by the stall's own granary family**; planks for any other building by odd-jobbers. May change later | DECIDED (owner 17/11/2026) |

## Oxen

| ID | Rule | Tag |
| --- | --- | --- |
| PL-20 | An ox carries **2 logs** per trip (was 4). `SimTuning.OxLoadLogs`, exposed in `SimConfig` | DECIDED (owner 17/11/2026) |

## Throughput check

One hewing family makes 3 beams a day and one sawing family saws 3, so one hewing station feeds one sawpit: 9 planks a
day. With only stalls using planks (2 each), the sawpit's 20-plank store fills in about 2 days and it then waits on
*Store full*. That is expected until tier 2 houses need beams and planks.
