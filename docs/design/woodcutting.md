# Woodcutting operation (live)

The first production building (GDD section 9, MVP-1), from M8 onward. Wins over GDD sections 8 and 9 and
WORK-07/08 where they disagree. People and the working day: `people.md`. Movement: `movement.md`.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Modules and placement

| ID | Rule | Tag |
| --- | --- | --- |
| WC-01 | The player draws a **woodcutting building area** (the area tool, building kind) and places modules inside it | DECIDED |
| WC-02 | Modules are **fixed rectangles** (footprints, `movement.md`): hut **6 × 4 m**, log storage **8 × 6 m**, oxen stand **3 × 3 m**. Reshaping them comes later | DECIDED (sizes PLACEHOLDER) |
| WC-03 | A module must sit **fully inside** its building area and must not overlap another module | DECIDED |
| WC-04 | Placing a module: it follows the cursor and can be rotated freely. With snapping on, an edge snaps flush to the nearest area edge (and turns to match it) when within the snap distance. Placement shows red with a reason when it's invalid | DECIDED |
| WC-05 | Hut: exactly 1, mandatory. Log storage: at least 1, 20 logs each (INV-A3). Oxen stand: optional, holds 1 ox | DECIDED |
| WC-06 | Blocking: the hut blocks movement. Log storage and the oxen stand don't (people and oxen walk onto them to unload and tie up) | DECIDED |
| WC-07 | **M8: placed modules are built instantly** (placeholder). Real construction by odd-jobbers comes in M9 | DECIDED |
| WC-08 | Trees under a placed module are removed and yield nothing (BLD-10-A) | DECIDED |

## Where they cut (work area)

| ID | Rule | Tag |
| --- | --- | --- |
| WC-10 | **Default:** with no drawn work area, the operation harvests **in a circle around the hut**, nearest tree first and working outwards. Nearest is measured by straight-line distance from the hut's access point | DECIDED |
| WC-11 | Default circle radius: **420 m** (`DefaultWorkRadiusM`; at most about ⅓ of a day's walk at ox speed). Because trees are taken nearest first, the radius only matters once the near trees are gone | DECIDED (value PLACEHOLDER) |
| WC-12 | The player can instead **draw a work area from the hut panel** (WORK-02). The drawn area replaces the circle. The order inside it is still nearest to the hut first. A "Use default" button goes back to the circle | DECIDED |
| WC-13 | The work area (circle or drawn) is shown only while the operation's panel is open (WORK-04, MVP-1-03) | DECIDED |
| WC-14 | Trees are reserved one per feller (TMB-08), so overlapping work areas never share a tree (WORK-05) | DECIDED |
| WC-15 | No standing tree left in the work area → *Inactive: no trees* (WCO-09) | DECIDED |

## Who does what (a family's workers)

The task system decides who does what. There are **no fixed roles**: whoever is free takes the next task, and
nobody stands idle.

| ID | Rule | Tag |
| --- | --- | --- |
| WC-20 | Workers are the working-age members of the assigned families (14+ for these heavy steps, POP-A3b). Up to 2 families per hut | DECIDED |
| WC-21 | **Felling always comes first.** If the operation has no ox (none assigned and none free in the pool), every worker fells | DECIDED |
| WC-22 | A **Haul** task opens when a full load (4 logs, WCO-07) is waiting at a tree site, or less when no standing tree is left to add to it. It goes to a **free** worker (one not in the middle of a tree), nearest to the ox first | DECIDED |
| WC-23 | **Fetch in good time:** when a tree's logs will make a full load, the Haul opens early, at the moment the time left on that tree ≤ the free worker's walk to the ox + the ox's walk to the tree. That way the ox arrives about when the logs are cut | PROPOSED |
| WC-24 | **After a haul the handler goes back to felling.** They leave the ox at the log storage (or its stand), and the next Haul goes to whoever is free. If nobody else is free, the previous handler takes it once their current tree is done. With one worker, they simply alternate | DECIDED |
| WC-26 | A worker in the middle of a tree finishes that tree before taking a Haul (no abandoned trees) | DECIDED |
| WC-25 | Felling stops when logs already cut (on the ground plus in storage) reach the total storage capacity, so logs never pile up with nowhere to go. Shows *Storage full* (INV-08) | PROPOSED |

## Oxen

| ID | Rule | Tag |
| --- | --- | --- |
| WC-30 | Oxen are a settlement-wide pool (CON-11). The building's **Oxen tab** can **assign** an ox to it. An assigned ox serves only that building until it's unassigned or the building is removed | DECIDED |
| WC-31 | An assigned ox rests at its building's oxen stand if it has one, otherwise beside the log storage. Assigning an ox doesn't need a stand | DECIDED (rest spot PROPOSED) |
| WC-32 | A pool ox goes back after a haul to the nearest free oxen stand (the camp stand or any operation's empty stand) | DECIDED |
| WC-33 | Leading an ox: the handler walks to the ox, they walk together at ox speed (MOV-16), load up to 4 logs at the tree (WCO-07), drag them to the log storage with free space nearest the tree (WCO-08), and unload | DECIDED |
| WC-34 | No ox can be had and logs are waiting → *Waiting for ox* (WCO-06) | DECIDED |
| WC-35 | **Haul cut short at sunset:** if a haul is still under way at the end of the working day, the handler **unloads the logs where they are** (a new ground pile at that spot, INV-11), takes the ox back to its stand, then walks home. The next day a new Haul picks the pile up from there. Unloading takes the normal unload time | DECIDED |
| WC-36 | The worker who needs the ox walks to fetch it (MVP). Possible later change: an ox assigned to a building gets its own **ox operator**, who does the hauling when the woodcutters are busy. Not in MVP-1 | DECIDED (future idea noted) |

## Timber chain timing (TMB-01..03)

Durations are in **game minutes** (decision 0004; the Sim tick is one game minute). With the 30-minute day, one game
minute is 1.25 real seconds at 1×.

| Stage | Enabled | Duration (game min) | At 1× (real) | Tag |
| --- | --- | --- | --- | --- |
| Fell | yes (M8) | 90 | ~1 min 53 s | PLACEHOLDER |
| Delimb | no (its time is added to the next enabled stage, TMB-02) | 30 | ~38 s | PLACEHOLDER |
| Buck (cut to length) | yes (M8) | 15 per log | ~19 s per log | PLACEHOLDER |
| Load / unload ox | yes (M9) | 10 each | ~13 s | PLACEHOLDER |

A 4-log tree takes about 3 game hours, or about 3¾ real minutes at 1×.

## Milestones (owner 11/11/2026)

- **M8 Woodcutters fell trees:** the woodcutting area, placing modules (built instantly), assigning families, the
  work schedule, felling and bucking into log piles on the ground, stumps, and the operation panel.
- **M9 Oxen haul logs:** the inventory ledger, the camp stockpile, ox leading, storage stacks, the Oxen tab and
  oxen stand, *Waiting for ox*, *Storage full*, and a second storage raising capacity to 40.
- **M10 Build it first:** construction by odd-jobbers (`people.md` PPL-40..44). Completes MVP-1.

## Felling times (DECIDED owner 11/11/2026: "Current", tuning values)

The owner chose **Current: fell 90, buck 15 per log** (stage table above). They're tuning values and will be
refined. A quick feller can fill storage fast, but the **ox haul is the intended bottleneck** for a long time
(see "Ox side" below), so felling speed isn't the main lever.

Options that were compared:

Assumptions: one feller, one tree at a time, about 3 min walking between neighbouring trees, about 3.5 logs per
tree (12–20 m trunks ÷ 4 m), Delimb off, the whole of daylight worked (commutes happen in the dark). With the
30-minute day, **1 game minute = 1.25 real seconds at 1×** (0.16 s at 8×, 0.04 s at 32×). 1 game hour = 75 real s at 1×.

| Option | Fell | Buck per log | One tree (game min) | One tree at 1× (real) | Trees / logs per worker, 12 h day | Summer 16 h | Winter 8 h |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Quick | 45 | 15 | ~100 | ~2 min | 7 / 25 | 10 / 34 | 5 / 17 |
| Brisk | 60 | 15 | ~115 | ~2.4 min | 6 / 22 | 8 / 29 | 4 / 15 |
| Current | 90 | 15 | ~145 | ~3 min | 5 / 17 | 7 / 23 | 3 / 12 |
| Heavy (closer to real axe work) | 120 | 30 | ~230 | ~4.8 min | 3 / 11 | 4 / 15 | 2 / 7 |

Ox side for comparison (4 logs per trip, load and unload 20 min): trees 50 m from storage ≈ 2 h per round trip ≈
24 logs per 12 h day; 200 m ≈ 6.8 h ≈ 7 logs per day. So hauling, not felling, becomes the limit as the forest
recedes, which is what makes oxen stands, closer storage and a second operation worth building.

## Operation panel (M8)

Selecting the hut or its area opens the panel (GDD section 9 inspector):
- modules and their state;
- assigned families with Assign / Remove (0 to 2 slots; never automatic, WCO-05, POP-03);
- each worker's current task;
- the **Oxen tab**: assigned oxen with Assign / Remove, and pool availability;
- the pool as total / reserved / available against capacity;
- logs waiting at tree sites and standing trees left;
- Show / draw / reset the work area;
- Pause / Resume;
- the inactive cause (*Missing hut*, *No workers*, *No trees*, *Storage full*, *Waiting for ox*, *Paused*).
