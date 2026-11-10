# People: families, citizens and the working day (live)

Rules for citizens, families and their daily routine, from M7 (People walk the land) onward.
Wins over GDD section 10 and TIME-05 where they disagree. Movement and pathfinding rules will be in
`movement.md`; woodcutting work in `woodcutting.md`.

_Status: being written. Only the sections marked DECIDED are agreed. Everything else is still open._

## Distances and the working day (DECIDED, owner 10/11/2026)

At current tuning a citizen walks 0.87 m per game minute (about 52 m per game hour; `tuning.md`). A 300 m
commute takes about 5.7 game hours each way, which would use up most of a working day. The owner chose to
**keep real walking speed and design around short distances**:

| ID | Rule | Tag |
| --- | --- | --- |
| PPL-01 | Walking speed stays real (TIME-A3, TIME-A4). Distance is a cost the player manages: building close to camp, the forest and storage is what makes an operation productive | DECIDED |
| PPL-02 | The start camp sits at the **forest edge**. The first woodcutting operation can be placed so that camp, hut, log storage and the nearest trees are all within about **100–150 m** of each other (WORLD-06) | DECIDED (distance PLACEHOLDER) |
| PPL-03 | **The midday meal is eaten on site.** At the meal break a worker stops where they are (at the tree, the yard or the build site), eats there, and then goes on with the same task. Nobody walks home to eat. Replaces TIME-05's "meal at home" and FOOD-02's walk home | DECIDED |
| PPL-04 | The meal break is a pause in the current task. The task keeps its reservations and resumes (TSK-15 and TSK-16 still apply to the night break) | DECIDED |
| PPL-05 | Meal break length: **60 game minutes**, starting at the midpoint between sunrise and sunset (`Daylight`). Both are tuning values | PLACEHOLDER |
| PPL-06 | Workers do not lodge at their workplace. They sleep at home (the camp-site while Homeless, POP-A1). Lodging was considered and set aside (it may come back as an option if commutes play badly) | DECIDED (for now) |

Consequence for later systems: in MVP-2 food is fetched only on Sunday (FOOD-A7), so eating on site needs no
food transport. The day's ration is still taken from the household larder by the daily deduction (FOOD-A9).
