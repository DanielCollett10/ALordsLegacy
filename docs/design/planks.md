# Sawpit and planks (M17)

A second production chain: logs become planks at a sawpit. MVP-4 part 1 (GDD section 11). Owner decisions 17/11/2026.
The wider production chain and building costs will be refined later; this milestone proves a chain that turns one
item into another.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Sawpit

| ID | Rule | Tag |
| --- | --- | --- |
| PL-01 | The sawpit is **its own building** with a fixed footprint, placed like the granary (a standalone structure). It is **not** a module of the woodcutting operation | DECIDED (owner 17/11/2026) |
| PL-02 | Footprint **6 × 4 m**, recipe **6 logs, 480** worker-minutes, **1 family slot** | PROPOSED (PLACEHOLDER) |
| PL-03 | Families are assigned to it like any operation. Sawing is heavy work (POP-A3): workers aged 14+ | PROPOSED |
| PL-04 | **The sawyers fetch their own logs** by ox from the **nearest** source with free logs: a ground pile (felled logs) or a log storage, whichever is closer | DECIDED (owner 17/11/2026) |
| PL-04b | Oxen for the sawpit come from the free ox pool, like construction deliveries | PROPOSED |
| PL-05 | Output: **2 planks per working day per sawpit** (PLACEHOLDER). 1 log makes 2 planks, so a sawpit uses 1 log a day | DECIDED (rate, owner 17/11/2026); 2 planks per log PROPOSED |
| PL-06 | Planks are General items kept in the sawpit's own store, **up to 20 planks**. Full: *Store full*, sawing stops | DECIDED (owner 17/11/2026) |
| PL-07 | Status lines like the woodcutter: No workers, Waiting for logs, Waiting for ox, Store full | PROPOSED |

## What uses planks

| ID | Rule | Tag |
| --- | --- | --- |
| PL-10 | A market stall costs **2 planks** (its real MKT-03 cost), replacing the 1-log placeholder | DECIDED |
| PL-11 | Tier 2 houses will use planks (not built yet). Nothing else uses planks in M17 | DECIDED (owner 17/11/2026) |
| PL-12 | Construction learns **recipes with more than one item** and **hand delivery** for light items (planks by hand; logs still by ox). Thatch and panels reuse this | PROPOSED |
| PL-13 | Existing stalls keep their log cost; only new stalls cost planks. With no planks, a stall site waits (*Waiting for planks*) | PROPOSED |
| PL-14 | A new game starts with **10 planks** at the camp (5 stalls), so the market works before the sawpit runs | PROPOSED |

## Oxen

| ID | Rule | Tag |
| --- | --- | --- |
| PL-20 | An ox carries **2 logs** per trip (was 4). `SimTuning.OxLoadLogs`, exposed in `SimConfig` | DECIDED (owner 17/11/2026) |
