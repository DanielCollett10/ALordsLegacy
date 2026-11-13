# Construction (live)

How buildings and modules get built, from M10 (Build it first) onward. Wins over GDD section 6 (CON-01..07),
WCO-04 and `woodcutting.md` WC-07 where they disagree. Odd jobs: `people.md` PPL-40..45. Costs: `tuning.md`
"Construction and timber". Technical shape: decision 0033.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## What gets built

| ID | Rule | Tag |
| --- | --- | --- |
| CN-01 | Construction is **generic**: any footprint type with a recipe (logs + build worker-minutes) is placed as a **construction site**. M10 uses it for the woodcutting modules; houses and the granary (MVP-2) reuse it unchanged | DECIDED |
| CN-02 | Recipes are data on the footprint type: hut **8 logs, 960** worker-minutes; oxen stand **1 log, 60**; log storage **0 logs, 0** (designated ground). A recipe of 0 logs and 0 minutes is **built at once** on placement (as today) | DECIDED (values PLACEHOLDER, CON-A1) |
| CN-03 | Site states: **Delivering** (materials on the way) → **Building** → **Built**. A built footprint is what M9 already has | DECIDED |
| CN-04 | Until a module is Built it does nothing: an unbuilt hut means *Missing hut* (BLD-08, MVP-1-05), an unbuilt storage adds no capacity, an unbuilt stand holds no ox | DECIDED |
| CN-05 | The site's footprint exists from placement: it clears trees (WC-08), blocks movement if its type blocks, and stops other modules overlapping it | PROPOSED |

## Paying for it

| ID | Rule | Tag |
| --- | --- | --- |
| CN-10 | **Placement is refused if the settlement can't pay.** The placement preview shows *Not enough logs (have 5, need 8)* in red, and `PlaceModule` is rejected. No Planned-without-materials state for now | DECIDED (owner 13/11/2026) |
| CN-11 | "Can pay" = available logs (total − reserved, INV-05) across all **sources**: the camp stockpile, every operation's built log storage, and unowned ground piles (CN-32). Pools may be combined (INV-09) | DECIDED |
| CN-12 | On placement the whole recipe is **reserved at once** from the sources, nearest source to the site first. Reserved logs can't be used by another site (CON-03). The check and the reservation happen in the same command, so two placements can never spend the same logs | DECIDED |
| CN-13 | Taking logs from a woodcutter's storage frees room there, so a *Storage full* operation starts felling again (MVP-1-11) | DECIDED |

## Who builds it (odd jobs)

| ID | Rule | Tag |
| --- | --- | --- |
| CN-20 | Construction tasks go to **odd-jobbers** only: working-age (14+) members of families with no workplace (PPL-40, PPL-44). Assigned families never build | DECIDED |
| CN-21 | **Deliver:** an odd-jobber fetches an ox (`OxPool.FindFor`: an ox assigned to the site's operation first, otherwise the nearest free pool ox), leads it to the source, loads up to 4 logs (WCO-07), leads it to the site, unloads, and returns the ox. Same step kinds and timings as the M9 haul. Logs never move by hand (INV-12) | DECIDED |
| CN-22 | Delivered logs sit on the site as a visible pile (one log per log, INV-06) | DECIDED |
| CN-23 | **Build progresses as materials arrive.** Build work can start once the first log is on site. Progress is capped at the share delivered: cap = recipe minutes × (logs delivered so far ÷ logs needed). Several odd-jobbers may build one site at once (up to `MaxBuildersPerSite`, 4); progress is the sum of their worker-minutes. At the full recipe total the module is Built | DECIDED (owner 13/11/2026; 4 PLACEHOLDER) |
| CN-23b | **Logs are used up as the work goes on:** each time progress passes one log's share (recipe minutes ÷ logs needed), one log leaves the site pile (consumed in the ledger). The pile shrinks as the scaffold grows | PROPOSED |
| CN-23c | **Builders haul when they're blocked.** Builders and haulers are the same odd-jobbers. A builder who reaches the cap stops building and is free again, so the dispatcher can give them a Deliver (to this site or any other) instead of leaving them standing | DECIDED (owner 13/11/2026) |
| CN-24 | Building is heavy work (POP-A3); a scaffold placeholder shows progress (CON-05) | DECIDED |
| CN-25 | Odd-jobbers who have construction work keep the same **working day** as assigned workers: work sunrise to sunset, commute in the dark, short Sunday, minimum sleep (PPL-20..26). An unfinished Build pauses overnight and keeps its progress. A Deliver still under way at sunset is wound down like WC-35: logs dropped where the ox stands (still reserved for that site), ox returned, and the next day's Deliver picks them up from there | PROPOSED |
| CN-26 | With no construction work, odd-jobbers mill about (PPL-43) | DECIDED |

## Which site first

| ID | Rule | Tag |
| --- | --- | --- |
| CN-30 | Each site has a **priority**: Normal (default) or High, set from the operation panel (CON-06) | DECIDED (owner 13/11/2026; two levels PROPOSED) |
| CN-31 | The dispatcher picks for a free odd-jobber: **highest priority first, then the nearest job to that worker**, the same rule as other task picking. For a Deliver, "nearest" is the worker's distance to the ox; for a Build, the distance to the site. So far-away sites wait unless the player raises them, and distant settlements cost time (owner's intent) | DECIDED (owner 13/11/2026) |

## Removing a site

| ID | Rule | Tag |
| --- | --- | --- |
| CN-32 | Removing an unbuilt site releases every undelivered reservation. Logs already on site, or on an ox mid-delivery, are dropped as an **unowned ground pile** at the site (CON-07). Unowned piles are construction sources (CN-11), so those logs get used again | PROPOSED |
| CN-33 | Removing a **built** module returns nothing in M10 (as today). The 50% refund (BLD-11) comes later | PROPOSED |

## What the player sees

| ID | Rule | Tag |
| --- | --- | --- |
| CN-40 | The operation panel lists each module's state: *Delivering 4 / 8 logs*, *Building 40%*, *Built*, plus a High priority toggle per unbuilt module | DECIDED |
| CN-41 | The citizen panel shows odd-job tasks (*Delivering logs to Hut*, *Building Hut*) | DECIDED |
| CN-42 | A site shows a placeholder scaffold whose height or fill follows build progress, with its delivered logs beside it | PROPOSED |
| CN-43 | The camp stockpile and storage stacks go down by exactly the logs taken (MVP-1-04: hut placed → the stockpile ends 8 lower) | DECIDED |

## Not in M10

Reshaping a confirmed area while it's under construction (FG-05), demolition refunds, artisan build steps (PPL-42),
clearing trees as a felling job (BLD-10-C), stages beyond Deliver and Build (CON-04 allows them as data later).
