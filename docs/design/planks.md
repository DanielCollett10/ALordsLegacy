# Sawpit and planks (M17)

A second production chain: logs become planks at a sawpit. MVP-4 part 1 (GDD section 11). Owner decisions 17/11/2026.
The wider production chain and building costs will be refined later; this milestone proves a chain that turns one
item into another.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Sawpit

| ID | Rule | Tag |
| --- | --- | --- |
| PL-01 | The sawpit is **its own building** with a fixed footprint, placed like the granary (a standalone structure). It is **not** a module of the woodcutting operation | DECIDED (owner 17/11/2026) |
| PL-02 | Footprint **6 × 4 m**, recipe **6 logs, 480** worker-minutes, **1 family slot** | DECIDED (owner 17/11/2026; PLACEHOLDER) |
| PL-03 | Families are assigned to it like any operation. Sawing is heavy work (POP-A3): workers aged 14+ | PROPOSED |
| PL-04 | **The sawyers fetch their own logs** by ox from the **nearest** source with free logs: a ground pile (felled logs) or a log storage, whichever is closer | DECIDED (owner 17/11/2026) |
| PL-04b | Oxen for the sawpit come from the **free ox pool**, like construction deliveries | DECIDED (owner 17/11/2026) |
| PL-04c | The sawpit holds a small log stock for sawing (capacity 4 logs, PLACEHOLDER); a fetch is made when there is room for an ox load | PROPOSED |
| PL-05 | **1 log makes 2 planks**; output is **fixed at 2 planks per working day per assigned family**, however many members it has (one saw task at a time; sawing one log takes the whole shift; minutes PLACEHOLDER). Other adults in the family fetch logs | DECIDED (owner 17/11/2026) |
| PL-06 | Planks are General items kept in the sawpit's own store, **up to 20 planks**. Full: *Store full*, sawing stops | DECIDED (owner 17/11/2026) |
| PL-07 | Status lines like the woodcutter: No workers, Waiting for logs, Waiting for ox, Store full | PROPOSED |

## What uses planks

| ID | Rule | Tag |
| --- | --- | --- |
| PL-10 | A market stall costs **2 planks** (its real MKT-03 cost), replacing the 1-log placeholder | DECIDED |
| PL-11 | Tier 2 houses will use planks (not built yet). Nothing else uses planks in M17 | DECIDED (owner 17/11/2026) |
| PL-12 | Construction learns **recipes with more than one item** and **hand delivery** for light items (planks by hand; logs still by ox). Thatch and panels reuse this | PROPOSED |
| PL-13 | Stalls already placed keep their log cost; only new stalls cost planks. With too few planks no stall is placed and the granary family shows *Waiting for planks* (as today with logs) | PROPOSED |
| PL-16 | Planks are carried **by hand, 2 per trip** (PLACEHOLDER), decision 0046. **Stall planks are carried by the stall's own granary family**; planks for any other building by odd-jobbers, like log deliveries. May change later | DECIDED (owner 17/11/2026) |
| PL-14 | A new game starts with **10 planks** at the camp (5 stalls), so the market works before the sawpit runs | DECIDED (owner 17/11/2026) |
| PL-15 | Until the storage yard (M18), starting planks sit in a new camp **General goods** store (`storage.md` ST-02) | PROPOSED |

## Oxen

| ID | Rule | Tag |
| --- | --- | --- |
| PL-20 | An ox carries **2 logs** per trip (was 4). `SimTuning.OxLoadLogs`, exposed in `SimConfig` | DECIDED (owner 17/11/2026) |
