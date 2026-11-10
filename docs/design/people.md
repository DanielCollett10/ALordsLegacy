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

## Distances (DECIDED, owner 10/11/2026)

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-01 | At 1× a walking citizen always looks like a person walking at a real pace (TIME-A3). Distance is a cost the player manages | DECIDED |
| PPL-02 | The start camp sits at the forest edge, so a first woodcutting operation can be placed close to camp and trees (WORLD-06) | DECIDED |
| PPL-03 | The owner wants a 300 m commute to cost noticeably less than "most of a day" without people looking wrong at 1×. How: see the open question in `movement.md` (day length, walk pace, walking outside daylight) | OPEN |
| PPL-06 | Workers sleep at home (the camp while Homeless). They do not lodge at the workplace | DECIDED (for now) |

## The week (DECIDED, owner 10/11/2026)

**Citizens eat once a week, on Sunday, at home.** There is no meal break in the working day and no trip to
fetch food during the week. This keeps walking for work, not for food, and removes daily food logistics.
How often people eat (weekly, daily, monthly) is a tuning value and may be refined later; weekly is set now.

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-20 | Working days run from sunrise to sunset (TIME-A5), with **no meal break**. Replaces TIME-05's daily meal at home and FOOD-02's walk home | DECIDED |
| PPL-21 | **Sunday:** everyone works as on any other day (no church yet, so no rest day, FOOD-A4b) and eats **at home in the evening**, when they come back at sunset. No extra trip | DECIDED (evening timing PROPOSED) |
| PPL-22 | The weekly meal consumes a week of need at once (MVP-2, food system). Replaces FOOD-A9's daily eating. The Sunday allotment and fetching trip (FOOD-A6, FOOD-A7) are unchanged and come with markets | DECIDED |
| PPL-23 | Meal frequency is a tuning value (`MealsEveryDays` = 7) | PLACEHOLDER |
| PPL-24 | Night (sunset to sunrise): citizens walk home and sleep there. A task that isn't finished at sunset is **suspended**: it keeps its progress and reservations and resumes the next morning (TSK-15, TSK-16) | DECIDED |

## When there is nothing to do

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-30 | A citizen with no task **mills about** near home during daylight. Later idle choices (go to the nearest market, the tavern if one exists, chat with nearby citizens, tidy their burgage) are added as data without changing the system | DECIDED |
| PPL-31 | M7 milling: pick a random spot within `MillRadiusM` (25 m) of the camp fire on walkable ground, walk there, stay 20–90 game minutes, repeat. Each citizen's choices come from the seeded Sim random, and start times are spread so people don't move in step | PLACEHOLDER (values) |
| PPL-32 | Children under working age always mill about near home (POP-11) | DECIDED |
| PPL-33 | Debug only: a **send to** action moves one selected citizen to a clicked point, to test movement. After arriving they stay 30 game minutes, then go back to their routine. Not in player builds | DECIDED |

## Odd jobs (unassigned families)

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-40 | Every working-age citizen of a family **not assigned** to a workplace does **odd jobs** (POP-04, TSK-A1, TSK-A2) | DECIDED |
| PPL-41 | MVP odd jobs: **construction only**: hauling materials to construction sites (Deliver) and doing the build work (Build) | DECIDED |
| PPL-42 | Later, some build steps (upgrades, certain buildings) need an **artisan builder**. Odd-jobbers still deliver materials to those sites but never take the artisan steps. This is a per-step eligibility in data (decision 0005), not a special case in code | DECIDED |
| PPL-43 | An odd-jobber with no odd job mills about (PPL-30) | DECIDED |
| PPL-44 | Heavy steps (POP-A3: construction, leading oxen) are for 14+ only, so children don't do odd jobs in MVP-1 | DECIDED |
| PPL-45 | A family assigned to a workplace whose work has stopped (storage full, no trees) does **not** fall back to odd jobs; it mills about | OPEN (lead's default; owner to confirm) |
