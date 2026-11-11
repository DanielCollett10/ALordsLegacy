# Tuning values and timing (live)

Every number here is a **PLACEHOLDER** for balancing. Each one lives in data (`SimTuning` or a
definition), never in code, so it can be changed without touching code (DEF-03).
**When a default changes, update this file in the same change.** The rule behind each value
is in the GDD or `gdd-amendments.md` (IDs in the last column).

## Time

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Sim tick | 1 | game minute | CLK-01, decision 0004 |
| RealMinutesPerGameDayAt1x | 30 (was 15) | real minutes | TIME-A2, MOV-20 |
| Speeds | 1, 2, 4, 8, 16, 32 | × | TIME-A3 |
| DaysPerYear | 365 | days | TIME-01 |
| Start | 1 Mar, Year 1, 06:00, Monday | | gdd-amendments Time |
| Min / max daylight | 8 / 16 | game hours | TIME-A5 |
| Rest day | Sunday, **off** until a church exists (was on) | switch | FOOD-A4, FOOD-A4b, PPL-21 |
| Sunday meal (shorter Sunday work) | 120 | game minutes taken off the end of Sunday's working day | PPL-21 |
| Minimum sleep | 6 | game hours at home per night | PPL-26 |
| MaxTicksPerFrame | 8 | ticks | CLK-05 (above this the game slows down instead of skipping) |

> Implementation note (MaxTicksPerFrame, CLK-05): `TickPacer.Advance` returns the cap and drops the excess
> ticks and the fractional carry, so game time falls behind real time and `SlowedDown` shows the top-bar notice.
> Dropped ticks are never run later. The tech spec says "slows itself rather than skipping simulation steps".
> Owner to decide whether dropping is the intended behaviour or whether due ticks should be carried and run later.

> Implementation note (Rest day, FOOD-A4): `SimTuning.RestDayEnabled` exists (default off, as above), but no Sim system reads it yet.

Calendar: DayOfYear counts from 1 Jan (1 Jan = day 1, so 1 Mar = day 60). The year number
goes up on **1 January**, so Year 1 is a short first year (1 Mar – 31 Dec) and Year 2 starts
on 1 Jan. No leap years.

Derived: the clock runs 48× real time at 1×. A year is 182 h at 1×, 22.8 h at 8×, 11.4 h at 16× and
5.7 h at 32×. At 32× the Sim runs about 26 ticks per real second.

## Movement

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Walk speed | 1.74 (was 0.87) | m per game minute (= 5 km/h on screen at 1× with the 30-minute day) | TIME-04, MOV-21, decision 0004 |
| Ox speed | 1.04 (was 0.52) | m per game minute (= 3 km/h on screen at 1×) | TIME-04, MOV-21 |
| Road speed multiplier (dirt path) | 1.25 (was 1.5) | × (walk animation sped up to match) | ROAD-10-A, MOV-09 |
| Max walkable slope | 30 | degrees (slope never slows walking) | MOV-01, MOV-02 |
| Nav grid cell | 2 | m | decision 0022 |
| Milling radius / stay | 25 / 20–90 | m from camp fire / game minutes | PPL-31 |
| Debug send-to linger | 30 | game minutes | PPL-33 |
| Meals every | 7 | days (Sunday evening, at home) | PPL-21, PPL-23 |
| Camp footprint | 24 × 16 | m | CMP-02 |

Derived (walking, off-road): 104 m per game hour, a quarter day ≈ 625 m, a half day ≈ 1.25 km,
a full day ≈ 2.5 km. Crossing a 25 km world ≈ 10 game days. A 300 m commute ≈ 2.9 h (2.3 h on a road).
If walk speed or day length changes, recompute these numbers and the tables below.

## Roads and areas

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Dirt path width | 2.5 | m | RD-04 |
| Max road grade | 15 | % (rise ÷ run per 1 m step) | RD-05 |
| Road sample spacing | 1 | m | RD-03 |
| Min distance between road points | 1 | m | RD-02 |
| Tree clearance beyond road half width | 0.5 | m | RD-11 |
| Snap distance (area tools) | 12 | screen pixels (Unity side) | roads.md, areas.md |
| Snap distance (road tool, M5) | 16 | screen pixels (Unity side) | roads.md |
| Dead-end extension tangent tolerance | 1 | degrees | RD-16 |
| Area cursor sphere | 0.6 | m diameter (Unity side) | AR-20 |
| Road snap dot spacing / shown within | 2.5 / 25 | m / m from cursor (Unity side) | AR-22 |
| Near-road edge reach | 10 | m (area type value in the Sim, checked by the tool and the validator) | AR-23, AR-24 |
| Area points | 4–8 | vertices | AR-02 |
| Curve flattening spacing | 1 | m | AR-05 |
| Test area size / max slope | 100–5,000 / 10 | m² / degrees | AR-13, AR-14 |
| Test work area size | 100–40,000 | m² | AR-13 |
| Slope sample grid | 2 | m | AR-14 |
| Overlap tolerance | 0.1 | m² | AR-15 |
| Spatial index cell | 32 | m | decision 0012 |

Where they live: per-type values (road width, max grade, area points, m², max slope) are on the type definitions
(`RoadTypeDef`, `AreaTypeDef`). Shared values (sample spacing, min point distance, tree clearance, curve flattening
spacing, slope sample grid, overlap tolerance, spatial index cell) are in `SimTuning`. Snap distance is a
presentation value in `ViewSettings`.

**Inspector config (M5, decision 0019):** every Sim value in this file (`SimTuning`, road types and area types) is
editable in the Unity Inspector on one `SimConfig` asset that `SimHost` reads. Tooltips name the rule ID. The
asset's defaults equal the code defaults. Edits made during Play take effect the next time Play starts (the world is
built once, at start). Presentation values (snap distances, sphere, dots) stay on `ViewSettings`.

## Population and oxen

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Starting families / citizens | 3 / 10 (6 adults, 4 children) | | MVP-1 |
| Light work from age | 8 | years | POP-A2 |
| Heavy work and troops from age | 14 | years | POP-A2 |
| Adult, may marry from | 18 | years | POP-17 |
| Ageing rate | 2 | × calendar | POP-27 |
| Starting oxen | 2 | | Oxen |

> Implementation note (Starting oxen): `SimTuning.StartingOxen` (2) is not read. The start layout's `OxCount` (2) creates the oxen.

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
| Market range | 630 | m walking distance (≈ ¼ day since the 30-minute day; owner kept 630 m on 11/11/2026, to revisit with MVP-2) | MKT-06 |
| Stall cost | 1 log (later 2 planks) | | MKT-03 |
| Hand carry per trip (no container) | 10 | items | MKT-03e |
| Load / unload time per trip | 10 | game minutes | PROPOSED |

**Stall throughput.** Rations a stall family delivers per day =
`working adults × trips per day × items per trip × average rations per item`, where
`trips per day = daylight minutes ÷ (2 × distance ÷ walk speed + load time)`.
With today's defaults (2 adults, 10 items, about 2 rations per item, 16 h summer daylight):

| Store to market | Trips per adult per day | Feeds about |
| --- | --- | --- |
| 100 m | ~7–8 | 300 people |
| 300 m | ~2–3 | 100 people |
| 600 m | ~1–2 | 55 people |

(Recomputed for walking at 1.74 m per game minute. People still eat the same rations per week; PPL-22 only
changes *when* they eat.)

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
| Fell / Delimb (off) / Buck | 90 / 30 / 15 per log | game minutes (1 game min = 1.25 real s at 1×) | woodcutting.md, TMB-01 |
| Load / unload ox | 10 / 10 | game minutes | woodcutting.md |
| Default work radius | 420 | m from the hut | WC-11 |
| Hut / log storage / oxen stand | 6×4 / 8×6 / 3×3 | m | WC-02 |

> Implementation note (Trunk length and tree density, TMB-06, DEF-03): the 12–20 m trunk range is in both
> `SimTuning` (`TrunkLengthMinM`/`MaxM`, read by nothing) and `ForestScatter` (`MinTrunkM`/`MaxTrunkM`, which the
> scatter actually uses). Tree density (493 per hectare) and the seed are editor constants in `ForestScatter`, not in
> `SimTuning`. Owner to decide where these belong.

Woodcutting check (ox 1.04 m per game minute): trees 100 m from the log storage → ox round trip about
3.2 game hours of walking, plus loading → about 4 trips per 16 h summer day → about 16 logs per ox per day
(before fetching the ox from its stand).
