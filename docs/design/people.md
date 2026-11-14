# People: families, citizens and their week (live)

Rules for citizens, families, oxen and what they do with their time, from M7 (People walk the land) onward.
Wins over GDD section 10, TIME-05 and FOOD-A9 where they disagree. Movement is in `movement.md`, the start
camp in `camp.md`.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Who exists (M7)

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-10 | The start has **3 families, 10 citizens** (6 adults, 4 children), listed explicitly in the start layout (`camp.md`): each citizen has an id, name, sex, birth date and family. No random generation in MVP-1 | DECIDED (counts from MVP-1 scope) |
| PPL-11 | A family has an id, a name (the surname), its members and a workplace (none in M7). Each family has one **home**: while Homeless (POP-A1) that is its tent spot at the camp | DECIDED |
| PPL-12 | Age is computed from the birth date at the ageing rate (POP-27, 2×). M7 only shows it. Work-age rules (POP-A2) apply from M8 | DECIDED |
| PPL-13 | Oxen are records too (id, name, home stand). The start has 2 (`StartingOxen`). In M7 they stand at the camp's ox stand and never move on their own. An ox moves only when a citizen leads it (M8) | DECIDED |
| PPL-14 | Every citizen and ox has a position, a facing and an activity the UI can show (Sleeping, Milling about, Walking, Working, Waiting, plus the task name from M8) | DECIDED |

> Implementation note (PPL-13): the 2 starting oxen come from the start layout's `OxCount`. `SimTuning.StartingOxen` is not read (see `tuning.md`).

## Distances (DECIDED, owner 10/11/2026)

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-01 | At 1× a walking citizen always looks like a person walking at a real pace (TIME-A3). Distance is a cost the player manages | DECIDED |
| PPL-02 | The start camp sits at the forest edge, so a first woodcutting operation can be placed close to camp and trees (WORLD-06) | DECIDED |
| PPL-03 | To shorten commutes without anyone looking faster at 1×: (A) a game day lasts **30 real minutes at 1×** (TIME-A2 changed), which doubles walk speed per game minute at the same on-screen pace, and (C) **people commute outside daylight** (PPL-25). A 300 m walk is now about 2.9 game hours, and it no longer comes out of working daylight | DECIDED (owner 10/11/2026) |
| PPL-06 | Workers sleep at home (the camp while Homeless). They do not lodge at the workplace | DECIDED (for now) |

## The week (DECIDED, owner 10/11/2026)

**Citizens eat once a week, on Sunday, at home.** There is no meal break in the working day and no trip to
fetch food during the week. This keeps walking for work, not for food, and removes daily food logistics.
How often people eat (weekly, daily, monthly) is a tuning value and may be refined later; weekly is set now.

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-20 | Working days run from sunrise to sunset (TIME-A5), with **no meal break**. Replaces TIME-05's daily meal at home and FOOD-02's walk home | DECIDED |
| PPL-21 | **Sunday:** everyone works (no church yet, so no rest day, FOOD-A4b), but the working day is **shorter**. Workers stop `SundayMealMinutes` (120) early and walk home. The weekly meal is **not simulated as an action**: the time is simply taken off the Sunday working day, and the food is consumed at home that evening (MVP-2). Batching the week's eating into one evening frees the other six days for work | DECIDED (minutes PLACEHOLDER) |
| PPL-22 | The weekly meal consumes a week of need at once (MVP-2, food system). Replaces FOOD-A9's daily eating. The Sunday allotment and fetching trip (FOOD-A6, FOOD-A7) are unchanged and come with markets | DECIDED |
| PPL-23 | Meal frequency is a tuning value (`MealsEveryDays` = 7) | PLACEHOLDER |
| PPL-24 | Night: citizens walk home and sleep there. A task that isn't finished when the citizen stops work is **paused**: it keeps its progress and reservations, and the citizen goes back to it and continues the next morning (TSK-15, TSK-16) | DECIDED |
| PPL-25 | **Commuting happens outside daylight.** A worker leaves home early enough to reach the workplace (or the task's first target) at sunrise, works until sunset, then walks home in the dark. The commute time comes from the route's travel time, worked out when they set off | DECIDED |
| PPL-26 | Minimum sleep: `MinSleepHours` (6) at home each night. If the commute would cut sleep below that, the worker sets off later and starts work after sunrise. This is how a far workplace still costs daylight, mostly in summer when nights are short. Purpose (owner): sleep is always met without using more of the night than needed, so travel moves into the dark and daylight goes to work | DECIDED (owner 11/11/2026; hours PLACEHOLDER) |
| PPL-27 | In M7 there is no work, so citizens follow the milling routine (PPL-31). PPL-25/26 and the Sunday rule (PPL-21) start with work in M8 | DECIDED |

## When there is nothing to do

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-30 | A citizen with no task **mills about** near home during daylight. Later idle choices (go to the nearest market, the tavern if one exists, chat with nearby citizens, tidy their burgage) are added as data without changing the system | DECIDED |
| PPL-31 | M7 milling: pick a random spot within `MillRadiusM` (25 m) of the camp fire on walkable ground, walk there, stay 20–90 game minutes, repeat. Each citizen's choices come from the seeded Sim random, and start times are spread so people don't move in step | PLACEHOLDER (values) |
| PPL-32 | Children under working age always mill about near home (POP-11) | DECIDED |
| PPL-33 | Debug only: a **send to** action moves one selected citizen to a clicked point, to test movement. After arriving they stay 30 game minutes, then go back to their routine. Not in player builds | DECIDED |

> Implementation note (PPL-33): built. `DebugSendController` sends `SendCitizenTo` on a right click with a citizen selected,
> and only in the Editor and development builds (`Debug.isDebugBuild`), so the "not in player builds" gate is in the Unity layer
> (decision 0026).
>
> Implementation note (PPL-30, PPL-31, PPL-32): every unassigned citizen, children included, mills around the camp fire
> (PPL-31), not near home. Assigned workers with no task mill around their family home instead (`RoutineSystem`, from M8).
> No children-only rule is coded. Owner to decide whether children should stay near their home tent, and whether
> assigned workers should mill at the fire too.

## Odd jobs (unassigned families)

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-40 | Every working-age citizen of a family **not assigned** to a workplace does **odd jobs** (POP-04, TSK-A1, TSK-A2) | DECIDED |
| PPL-41 | MVP odd jobs: **construction only**: hauling materials to construction sites (Deliver) and doing the build work (Build) | DECIDED |
| PPL-42 | Later, some build steps (upgrades, certain buildings) need an **artisan builder**. Odd-jobbers still deliver materials to those sites but never take the artisan steps. This is a per-step eligibility in data (decision 0005), not a special case in code | DECIDED |
| PPL-43 | An odd-jobber with no odd job mills about (PPL-30) | DECIDED |
| PPL-44 | Heavy steps (POP-A3: construction, leading oxen) are for 14+ only, so children don't do odd jobs in MVP-1 | DECIDED |
| PPL-45 | Once a family is assigned, it does only its workplace's tasks plus its own burgage extension if it has one (non-artisan: allotment garden, orchard and so on, from MVP-2 and later). If that work stops (storage full, no trees), it **mills about**. It never falls back to odd jobs | DECIDED |

> Implementation note (PPL-40, PPL-41, PPL-43): not built (construction is M10). The only task today, `task.fell_tree`, is
> assigned-family-only, so unassigned families mill about (PPL-43). Haul tasks (`task.haul_logs`) are assigned-family-only too.
