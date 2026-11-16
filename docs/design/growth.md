# Growth: arrivals and leaving (M16)

The settlement grows when housing and food allow it, and shrinks when it starves. First slice of MVP-3 (GDD section 11).
Builds on POP-A4..A8 (`gdd-amendments.md`), `homes.md` and `food.md`. Owner decisions 16/11/2026 marked DECIDED.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Hunger

| ID | Rule | Tag |
| --- | --- | --- |
| GR-01 | Hunger **never slows work** (keeps FD-16; overrides FOOD-A2 and FOOD-14's lower output) | DECIDED |
| GR-02 | Each household counts **hungry days in a row**: +1 on every day it is Hungry at midnight eating (FD-15), reset to 0 on a day it eats in full | DECIDED (owner 16/11/2026) |
| GR-03 | At **30 hungry days** (PLACEHOLDER, tuning value) the family **leaves** (GR-10) | DECIDED |
| GR-04 | The house and citizen panels show *Hungry (n / 30 days)* once n ≥ 1 | PROPOSED |
| GR-05 | Future: prolonged hunger causes deaths (FOOD-14, POP-24). Not in M16 | DECIDED (future) |

## Leaving

| ID | Rule | Tag |
| --- | --- | --- |
| GR-10 | A leaving family is **unassigned** from its workplace (its tasks cancelled as by `UnassignFamily`), and its house becomes empty at once, so another family can move in (HM-11) | PROPOSED |
| GR-11 | Its members drop anything they carry where they stand, then **walk to the nearest map exit along the road network** (DECIDED). A map exit is a point where a road meets the map edge. With no road exit, they walk to the nearest map edge point | DECIDED (fallback PROPOSED) |
| GR-12 | Larder food stays in the house larder for the next family | PROPOSED |
| GR-13 | While walking out they show *Leaving (hungry)*, cannot be assigned, and take no tasks. Each member is removed when they reach the exit; the family record goes with its last member | PROPOSED |
| GR-13b | Future: a family **homeless for 30 days** leaves the same way (`camp.md` CMP-26). Not in M16 unless the owner pulls it in | DECIDED (future) |
| GR-14 | A message line reads *The <name> family is leaving: hungry for 30 days* (GR-30) | PROPOSED |

## Arrivals

| ID | Rule | Tag |
| --- | --- | --- |
| GR-20 | Checked once a day at dawn. A family arrives only when there is **free housing**: a built, empty house with no Homeless family waiting for it (POP-A5, HM-11 fills houses first) | DECIDED |
| GR-21 | And a **food surplus**: village food (`FoodStats`) covers at least **30 days** of the current population plus the arriving family (PLACEHOLDER), and no household was Hungry in the last 7 days | DECIDED (owner 16/11/2026; values PLACEHOLDER) |
| GR-22 | At most **one arrival every 7 days** (PLACEHOLDER), so growth is steady, not a flood | DECIDED (PLACEHOLDER) |
| GR-22b | Arrivals **bring a month of food**: 30 days of the family's own need as a **mix of meat, berries and mushrooms** (owner 16/11/2026; equal rations of each, PLACEHOLDER), new stock entering the world. It is carried by the family's members while they walk in (no hand-carry cap for this, like FD-14) and goes into their larder on move-in, or their camp larder if they end up Homeless. Later, with baskets (CNT-*), they carry it in baskets | DECIDED (mix split PROPOSED) |
| GR-23 | Make-up follows POP-A7 weights in data: mostly a couple with 2–4 children; sometimes a single parent with children, a couple without children, or a family with adult children. Names from the start-layout name lists, ages from the same ranges, seeded random | DECIDED (weights PLACEHOLDER) |
| GR-24 | Arrivals **walk in along the trunk road** from its map-edge end (DECIDED). The chosen house is reserved for them; they walk to it and move in on arrival (HM-12). If the house is lost on the way, they become Homeless at the camp | DECIDED (reservation PROPOSED) |
| GR-25 | POP-A6 (adults filling an incomplete family) is **not** in M16; every arrival is a new family | PROPOSED |
| GR-26 | Message line: *The <name> family has arrived* | PROPOSED |

## What the player sees

| ID | Rule | Tag |
| --- | --- | --- |
| GR-30 | A small **message log** under the top bar shows the last few arrivals and departures, fading after 30 s real time | PROPOSED |
| GR-31 | Top bar population shows families and citizens, with the number arriving or leaving in the tooltip | PROPOSED |

## Not in M16

Deaths, births (POP-A4), coming of age forming families (POP-A8, POP-19), happiness, household levels (M17).
