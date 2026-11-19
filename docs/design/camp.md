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
| CMP-06 | Stockpile: **40 logs** (owner 15/11/2026, was 20 under CON-08) with no capacity limit (INV-10). It holds items from M8, when the inventory ledger exists; in M7 the spot is only drawn | DECIDED |
| CMP-07 | The 2 starting oxen stand at the ox stand (PPL-13) | DECIDED |
| CMP-08 | The test map's layout is written by an editor script that finds a flat spot at the forest edge, so it can be regenerated with the map | PROPOSED |
| CMP-09 | Placeholder look: a few tents, a fire, a log pile and a tie post | DECIDED |

## The camp hands over and goes (owner 16/11/2026, M15)

> **Amended M22b (owner direction 19/11/2026); these lines win over CMP-20, CMP-21, CMP-23 and CMP-24 below:**
> - **CMP-20a** Citizens eat from the camp until a **built market and a built granary both exist**. That is the handover. The granary's Supplies market toggle no longer triggers it.
> - **CMP-21a** At the handover everyone eats from the market. Granary families carry the camp's food into a granary, as in CMP-21.
> - **CMP-23a** The camp stays while any family is homeless. Once all are housed **and** the handover has happened, the camp is removed.
> - **CMP-24a** At removal the camp's contents drop as **three grouped ground piles**: food, General goods, construction goods. Ground piles of General goods are valid construction sources. Granary families collect the food pile. Pool oxen move to an Oxen post if one has room. If none does, they **linger where they are** (no stand) until a stand or post with room exists.

| ID | Rule | Tag |
| --- | --- | --- |
| CMP-20 | **Food handover:** once a **built granary supplies the market** (markets.md MK-10), the camp stops being a food supply. From the next Sunday, households get food only from the market (MKT-05) | DECIDED (owner 16/11/2026: switch at once, no waiting for stock) |
| CMP-21 | After the handover the camp **no longer stores food**: granary families carry what is left in the camp food store into a granary, as from a forager hut (MK-24 step 2). Once empty, the camp food store is removed | DECIDED |
| CMP-22 | The handover never reverses: switching the granary off, or losing it, does not make the camp a supply again | PROPOSED |
| CMP-23 | **Camp removal:** when **every family has a house** (no Homeless family) **and** the food handover has happened, the camp is removed: tents, fire, food store and footprint go | DECIDED (needs handover PROPOSED) |
| CMP-24 | Anything left in the camp at removal (logs in the stockpile, any food) is **dropped as an unowned ground pile** at the stockpile spot (like CN-54). Unowned log piles are construction sources (CN-11), so the logs get used. The pile blocks building over it while it holds items (PL-15). Oxen at the camp stand move to the nearest oxen stand, or rest where they are | DECIDED (owner 16/11/2026) |
| CMP-25 | **Homeless after removal** (a house deconstructed later, an arrival whose house was lost, GR-24): the family **dosses about the town** until a house is free: it mills about and sleeps rough around the market centre (the old camp fire spot if there's no market). No tent is drawn; it shows *Homeless (sleeping rough)*. HM-11 moves it into the next free house as usual. The camp never comes back | DECIDED (owner 16/11/2026; spot PROPOSED) |
| CMP-26 | Future (not M15): a family homeless for **30 days** (default, matching GR-03) leaves town as in `growth.md` GR-10..13 | DECIDED (future) |

> Implementation note (CMP-06, CMP-01): the start layout's `Stock` (default 40 `item.log`, T-287) is produced into a
> ledger `Stockpile` location at the stockpile spot, with no capacity limit. It landed in M9, while CMP-06 says M8.
> Owner to decide whether the milestone label should change. No operation storage counts it (WC-37).

> Implementation note (CMP-02, site search): `StartLayoutGenerator` looks for a flat spot with slope at most 5°
> (`MaxCampSlopeDeg`), just outside the forest edge, with at least 50 trees within 50 m (`MinTrees`). The design gives no
> tree count. Owner to confirm the 50.
