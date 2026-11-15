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
| CN-04b | **A family can't be assigned until the hut is Built** (MVP-1-06). `AssignFamily` is refused with "Hut not built yet" while the operation's hut is unbuilt, and the panel's Assign button is disabled with that text | DECIDED (lead, following GDD MVP-1-06) |
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
| CN-23b | **Logs are used up as the work goes on:** each time progress passes one log's share (recipe minutes ÷ logs needed), one log leaves the site pile (consumed in the ledger). The pile shrinks as the scaffold grows | DECIDED (owner 13/11/2026) |
| CN-23c | **Builders haul when they're blocked.** Builders and haulers are the same odd-jobbers. A builder who reaches the cap stops building and is free again, so the dispatcher can give them a Deliver (to this site or any other) instead of leaving them standing. A Build task is only offered while the site has capped work left (cap − progress > 0), so a capped builder can't take Build again until more logs arrive | DECIDED (owner 13/11/2026; no-retake rule lead) |
| CN-24 | Building is heavy work (POP-A3); a scaffold placeholder shows progress (CON-05) | DECIDED |
| CN-25 | Odd-jobbers who have construction work keep the same **working day** as assigned workers: work sunrise to sunset, commute in the dark, short Sunday, minimum sleep (PPL-20..26). An unfinished Build pauses overnight and keeps its progress. A Deliver still under way at sunset is wound down like WC-35: logs dropped where the ox stands (still reserved for that site), ox returned, and the next day's Deliver picks them up from there | PROPOSED |
| CN-25b | **How the dropped logs stay the site's:** the dropped logs go into a new ground pile **owned by the site** (owner = site footprint id). The Deliver's reservation on the carrier is released, and a new stock reservation for the same count is made on that pile for the site and added to the site's reservation list, in the same step, so the site's reserved total never changes. Site-owned piles are not sources for other sites (CN-11). Each morning `ConstructionSystem` opens Deliver tasks from the site's own piles before its other reserved sources. `Ledger.Audit` must pass before and after the drop | DECIDED (lead, from plan review) |
| CN-26 | With no construction work, odd-jobbers mill about (PPL-43) | DECIDED |

## Which site first

| ID | Rule | Tag |
| --- | --- | --- |
| CN-30 | Each site has a **priority**: Normal (default) or High, set from the operation panel (CON-06) | DECIDED (owner 13/11/2026; two levels PROPOSED) |
| CN-31 | The dispatcher picks for a free odd-jobber: **highest priority first, then the nearest job to that worker**, the same rule as other task picking. For a Deliver, "nearest" is the worker's distance to the ox; for a Build, the distance to the site. So far-away sites wait unless the player raises them, and distant settlements cost time (owner's intent) | DECIDED (owner 13/11/2026) |

## Removing a site

| ID | Rule | Tag |
| --- | --- | --- |
| CN-32 | Removing an unbuilt site releases every undelivered reservation. Logs already on site, or on an ox mid-delivery, are dropped as an **unowned ground pile** at the site (CON-07). Site-owned piles left by a sunset wind-down (CN-25b) become unowned too. Unowned piles are construction sources (CN-11), so those logs get used again | PROPOSED |
| CN-34 | Removing a built log storage that holds logs reserved for a construction site is **refused** ("Logs here are reserved for <site>"). Reservations therefore never point at a storage that has gone | PROPOSED |
| CN-33 | Removing a **built** module returns nothing in M10 (as today). **Replaced in M12 by CN-50..58** (deconstruction with a 50% refund) | DECIDED |

## Deconstruction (M12)

| ID | Rule | Tag |
| --- | --- | --- |
| CN-50 | Each module row in the operation panel has a **Deconstruct** button (and the house panel, M13). It asks for confirmation: *Deconstruct Hut? Returns 4 logs* | DECIDED (confirm PROPOSED) |
| CN-51 | On an **unbuilt** site, Deconstruct cancels it at once (CN-32: reservations released, delivered logs left as an unowned pile). Nothing was consumed past what the scaffold used | PROPOSED |
| CN-52 | On a **built** module, Deconstruct **marks** it: state *Deconstructing*. From that moment it stops working (a storage adds no capacity and takes no logs, a stand holds no ox, a hut means *Missing hut*) | PROPOSED |
| CN-53 | Odd-jobbers take a `task.deconstruct` at the site (same working day and priority rules as Build, CN-20..31). It takes **50% of the recipe's build minutes** (hut 480). A recipe of 0 minutes is removed at once | DECIDED (owner 15/11/2026; share PLACEHOLDER) |
| CN-54 | When finished: the footprint is removed and **round down(recipe logs × 50%)** logs appear as an **unowned ground pile** at the site (hut 4, oxen stand 0). Unowned piles are construction sources (CN-11). The share is a tuning value (`DeconstructRefundShare`) | DECIDED (owner: half; rounding PROPOSED) |
| CN-55 | A log storage still holding logs: its logs join the same unowned pile on completion. CN-34 still applies (refused while any are reserved for a site) | PROPOSED |
| CN-56 | Deconstructing the **hut** unassigns its families at the moment of marking (they become odd-jobbers). The confirm text says so | DECIDED (owner 15/11/2026) |
| CN-57 | **Cancel deconstruction** is offered while it's under way. The module is working again at once; progress is thrown away | PROPOSED |
| CN-58 | **Deconstruct operation** (panel button) marks every module. The area is removed when the last one is gone. Right-click delete of an operation area that still has modules is refused (*Deconstruct its buildings first*) | PROPOSED |

## What the player sees

| ID | Rule | Tag |
| --- | --- | --- |
| CN-40 | The operation panel lists each module's state: *Delivering 4 / 8 logs*, *Building 40%*, *Built*, plus a High priority toggle per unbuilt module | DECIDED |
| CN-41 | The citizen panel shows odd-job tasks (*Delivering logs to Hut*, *Building Hut*) | DECIDED |
| CN-42 | A site shows a placeholder scaffold whose height or fill follows build progress, with its delivered logs beside it | PROPOSED |
| CN-43 | The camp stockpile and storage stacks go down by exactly the logs taken (MVP-1-04: hut placed → the stockpile ends 8 lower) | DECIDED |

## Not in M10

Reshaping a confirmed area while it's under construction (FG-05), demolition refunds (now M12, CN-50..58), artisan build steps (PPL-42),
clearing trees as a felling job (BLD-10-C), stages beyond Deliver and Build (CON-04 allows them as data later).

## Implementation notes

Where the code (T-340..T-350) differs from or narrows the rules above. Code is the source of truth for what runs;
this list says what is still open.

- **Build has no walk to the site (CN-23).** `ConstructionTaskDefs.Build` is a single Work step with no target, so
  builders work where they stand. The dispatcher still measures travel to the site, so nearby odd-jobbers are picked
  first. A walk-to-site step is still to come.
- **Only Deliver winds down at sunset (CN-25).** An unfinished Build pauses overnight and keeps its progress, and it
  resumes next morning where the builder stands. Nothing is dropped. The Deliver wind-down follows CN-25b (site-owned pile).
- **Assignment gate (CN-04b) is enforced in `AssignFamily.Validate`.** The Sim refuses with "Hut not built yet", and
  `OperationPanelView` disables the Assign button with the same text.
- **Build tasks take the whole recipe minutes as their step length.** The cap (CN-23) ends a task early when a
  builder reaches it, so the step length is an upper bound, not the expected time.
- **Unity side is built.** `OperationPanelView` shows each module's state and the High toggle (CN-40, via
  `ConstructionProgress.Of` and `SetSitePriority`). `ConstructionRenderer` draws the scaffold (CN-42). The citizen panel's
  task line uses `ConstructionTaskDefs.VerbFor` (CN-41). Delivered logs are drawn by `LogPileRenderer` at the site.
- **Values are placeholders (CN-02, CN-23).** The recipe numbers, `MaxBuildersPerSite` 4 and the ox load/unload minutes
  are tuning placeholders until CON-A1 is decided.
- **Removing a built module returns nothing (CN-33).** The 50% refund is not built. The CN-34 refusal (a storage with
  logs reserved for a site cannot be removed) is in code.
