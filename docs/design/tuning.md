# Tuning values and timing (live)

Every number here is a **PLACEHOLDER** for balancing. Each one lives in data (`SimTuning` or a
definition), never in code, so it can be changed without touching code (DEF-03).
**When a default changes, update this file in the same change.** The rule behind each value
is in the GDD or `gdd-amendments.md` (IDs in the last column).

## Time

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Sim tick | 1 | game minute | CLK-01, decision 0004 |
| RealMinutesPerGameDayAt1x | 15 | real minutes | TIME-A2 |
| Speeds | 1, 2, 4, 8, 16, 32 | × | TIME-A3 |
| DaysPerYear | 365 | days | TIME-01 |
| Start | 1 Mar, Year 1, 06:00, Monday | | gdd-amendments Time |
| Min / max daylight | 8 / 16 | game hours | TIME-A5 |
| Rest day | Sunday, on | switch | FOOD-A4 |
| MaxTicksPerFrame | 8 | ticks | CLK-05 (above this the game slows down instead of skipping) |

> Implementation note (MaxTicksPerFrame, CLK-05): `TickPacer.Advance` returns the cap and drops the excess
> ticks and the fractional carry, so game time falls behind real time and `SlowedDown` shows the top-bar notice.
> Dropped ticks are never run later. The tech spec says "slows itself rather than skipping simulation steps".
> Owner to decide whether dropping is the intended behaviour or whether due ticks should be carried and run later.

> Implementation note (Rest day, FOOD-A4): `SimTuning.RestDayEnabled` exists (default on), but no Sim system reads it yet.

Calendar: DayOfYear counts from 1 Jan (1 Jan = day 1, so 1 Mar = day 60). The year number
goes up on **1 January**, so Year 1 is a short first year (1 Mar – 31 Dec) and Year 2 starts
on 1 Jan. No leap years.

Derived: clock runs 96× real time at 1×. A year is 91 h at 1×, 11.4 h at 8×, 5.7 h at 16×,
2.9 h at 32×. At 32× the Sim runs about 51 ticks per real second.

## Movement

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Walk speed | 0.87 | m per game minute (= 5 km/h on screen at 1×) | TIME-04, decision 0004 |
| Ox speed | 0.52 | m per game minute (= 3 km/h on screen at 1×) | TIME-04 |
| Road speed multiplier | 1.5 | × | ROAD-10 |

> Implementation note (Walk and ox speed, TIME-04): `SimTuning` holds both values, but no Sim system moves anyone yet.
> The road multiplier is not in code.

Derived (walking, off-road): 52 m per game hour; quarter day ≈ 310 m; half day ≈ 630 m;
full day ≈ 1.25 km. Crossing a 25 km world ≈ 20 game days. If walk speed or day length
changes, recompute these and the market table below.

## Population and oxen

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Starting families / citizens | 3 / 10 (6 adults, 4 children) | | MVP-1 |
| Light work from age | 8 | years | POP-A2 |
| Heavy work and troops from age | 14 | years | POP-A2 |
| Adult, may marry from | 18 | years | POP-17 |
| Ageing rate | 2 | × calendar | POP-27 |
| Starting oxen | 2 | | Oxen |

> Implementation note (Starting oxen): `SimTuning.StartingOxen` = 2 exists, but no oxen are created yet.

## Food

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Adult need | 1 | ration per day | FOOD-A1 |
| Child (under 14) need | 0.5 | ration per day | FOOD-A1 |
| Meat | 3 | rations per item | FOOD-A1b |
| Berries, mushrooms | 1 | ration per basket | FOOD-A1b |
| Starting food | 400 meat (1,200 rations) | | FOOD-A1b |
| Larder fill on Sunday | 7 | days of the household's need | FOOD-A6 |
| Granary capacity | 100 | food items | FOOD-04 |

## Markets and carrying

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Market range | 630 | m walking distance (≈ ½ day) | MKT-06 |
| Stall cost | 1 log (later 2 planks) | | MKT-03 |
| Hand carry per trip (no container) | 10 | items | MKT-03e |
| Load / unload time per trip | 10 | game minutes | PROPOSED |

**Stall throughput.** Rations a stall family delivers per day =
`working adults × trips per day × items per trip × average rations per item`, where
`trips per day = daylight minutes ÷ (2 × distance ÷ walk speed + load time)`.
With today's defaults (2 adults, 10 items, about 2 rations per item, 16 h summer daylight):

| Store to market | Trips per adult per day | Feeds about |
| --- | --- | --- |
| 100 m | ~4 | 150 people |
| 300 m | ~1–2 | 50 people |
| 600 m | ~1 | 25 people |

Winter (8 h daylight) roughly halves these. Containers (wishlist below) raise items per trip.

## Construction and timber

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Woodcutting hut | 8 logs, 960 | worker-minutes | CON-A1, WCO-04 |
| Oxen stand | 1 log, 60 | worker-minutes | CON-A1 |
| House (MVP-2) | 6 logs, 720 | worker-minutes | CON-A1 |
| Granary | 6 logs, 600 | worker-minutes | CON-A1 |
| Demolish refund / rebuild cost | 50% / 50% | of materials | BLD-11, MIL-08 |
| Standard log length | 4 | m | TMB-04 |
| Usable trunk length | 12–20 | m (3–5 logs) | TMB-06 |
| Ox load | 4 | logs per trip | WCO-07 |
| Log storage | 20 | logs at minimum size | INV-A3 |
| Woodcutting family slots | 2 | per hut | WCO |

> Implementation note (Trunk length and tree density, TMB-06, DEF-03): the 12–20 m trunk range is in both
> `SimTuning` (`TrunkLengthMinM`/`MaxM`, read by nothing) and `ForestScatter` (`MinTrunkM`/`MaxTrunkM`, which the
> scatter actually uses). Tree density (493 per hectare) and the seed are editor constants in `ForestScatter`, not in
> `SimTuning`. Owner to decide where these belong.

Woodcutting check: trees 100 m from the log storage → ox round trip about 6.4 game hours
→ about 2 trips per summer day → about 8 logs per ox per day.
