# Start camp and start layout (live)

The pre-placed camp-site where the game starts (WORLD-09), from M7 onward. Wins over WORLD-08/09 and
MVP-1-01 where they disagree.

| ID | Rule | Tag |
| --- | --- | --- |
| CMP-01 | The game starts from a **start layout** held in data: camp position and rotation, the trunk road's control points, the families and citizens (PPL-10), the number of oxen, and the stockpile contents. Same layout, same start | DECIDED |
| CMP-02 | The camp is a **fixed rectangle footprint** (placeholder 24 × 16 m, set in the layout) at the test map's **forest edge**, on ground no steeper than 5°, with standing trees within about 50 m | DECIDED (size PLACEHOLDER) |
| CMP-03 | Inside the camp: one **tent spot per family** (its home while Homeless, POP-A1), a **camp fire** (the milling centre, PPL-31), the **stockpile** spot and the **ox stand**. All are fixed points in the layout, relative to the camp | DECIDED |
| CMP-04 | The camp footprint does not block walking (MOV-03). Trees under it are removed at start (BLD-10-A) | DECIDED |
| CMP-05 | The trunk road is an ordinary road made at start with the normal road command (WORLD-08-A), passing beside the camp. The player can delete or reroute it | DECIDED |
| CMP-06 | Stockpile: 20 logs (CON-08) with no capacity limit (INV-10). It holds items from M8, when the inventory ledger exists; in M7 the spot is only drawn | DECIDED |
| CMP-07 | The 2 starting oxen stand at the ox stand (PPL-13) | DECIDED |
| CMP-08 | The test map's layout is written by an editor script that finds a flat spot at the forest edge, so it can be regenerated with the map | PROPOSED |
| CMP-09 | Placeholder look: a few tents, a fire, a log pile and a tie post | DECIDED |

> Implementation note (CMP-06, CMP-01): the start layout's `Stock` (default 20 `item.log`, T-287) is produced into a
> ledger `Stockpile` location at the stockpile spot, with no capacity limit. It landed in M9, while CMP-06 says M8.
> Owner to decide whether the milestone label should change. No operation storage counts it (WC-37).
