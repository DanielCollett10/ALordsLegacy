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
| Speeds | 1, 2, 4, 8, 16, 32, 64 (64 from M22a) | × | TIME-A3 |
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
>
> Implementation note (Sunday meal PPL-21, Minimum sleep PPL-26): read by `WorkDay` (T-277, M8). Sunday work ends early by
> `SundayMealMinutes`; the commute keeps `MinSleepHours` of sleep at home.

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

> Implementation note (Meals every, PPL-21, PPL-23): no code reads this row. Daily eating (FD-15, `EatingSystem`, at
> midnight) replaced the weekly meal in M14. Owner to decide whether the row is removed.

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

> Implementation note (Dead-end extension tangent tolerance, RD-16): no code reads a 1° tolerance. The new curve uses the
> dead end's own arriving direction exactly (`StartTangent`/`EndTangent`, decision 0029), which is within the 1° allowed.

Where they live: per-type values (road width, max grade, area points, m², max slope) are on the type definitions
(`RoadTypeDef`, `AreaTypeDef`). Shared values (sample spacing, min point distance, tree clearance, curve flattening
spacing, slope sample grid, overlap tolerance, spatial index cell) are in `SimTuning`. Snap distance is a
presentation value in `ViewSettings`.

**Inspector config (M5, decision 0019):** every Sim value in this file (`SimTuning`, road types and area types) is
editable in the Unity Inspector on one `SimConfig` asset that `SimHost` reads. Tooltips name the rule ID. The
asset's defaults equal the code defaults. Edits made during Play take effect the next time Play starts (the world is
built once, at start). Presentation values (snap distances, sphere, dots) stay on `ViewSettings`.

> Implementation note (module recipes, decision 0019): the `SimConfig` module recipe fields are marked "NOT YET APPLIED".
> The Sim still reads `ModuleTypes` in code, so editing a recipe in the asset (granary, forager hut) changes nothing.

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
>
> Implementation note (Light work from age 8, POP-A2): `SimTuning.ForagerMinAge` (8) is read by foraging dispatch only (FD-23).
> Foraging is the only light task, so other children still do no work. Heavy work uses `OperationStatus.WorkingAgeYears` (14).
>
> Implementation note (Adult, may marry from 18, POP-17): not in code. Nothing marries.

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
| Stall cost | 2 planks, carried by the granary family | | MKT-03, PL-10 |
| Hand carry per trip (no container) | 10 | items | MKT-03e |
| Load / unload time per trip | 10 | game minutes | PROPOSED |
| Market area | 100–2,000 | m² | MK-02 |
| Stall footprint / recipe | 3 × 2 m / 2 planks, 60 | m / worker-minutes | MK-21 |
| Granary families collect from forager huts | any distance, fullest first | | MK-24 |
| Stall capacity | 30 | food items | MK-22 |
| Stall slot cell | 4 × 5 | m (stall 3 × 2, 1 m side gap, 3 m aisle) | MK-05 |
| Markets per town | 1 | | MK-04 |
| Ground pile no-build circle | 2 | m radius around a pile with items | PL-15 |
| Store-family worker age | 8+ | years (light work) | MK-24 |
| Hungry days before leaving | 30 | days in a row (`HungryDaysToLeave`, T-496) | GR-03 |
| Arrival food surplus | 30 | days of food incl. the new family (`ArrivalFoodDays`) | GR-21 |
| Arrival no-hunger window | 7 | days with no Hungry household (`ArrivalNoHungerDays`) | GR-21 |
| Arrival gap | 7 | days between arrivals (`ArrivalGapDays`) | GR-21 |
| Map edge tolerance | 2 | m (`MapEdgeToleranceM`) | GR-21 |
| Arrival cooldown | 7 | days | GR-22 |
| Arrival food | 30 | days of the arriving family's need (equal rations of meat, berries, mushrooms) | GR-22b |

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

## Foraging

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Bush baskets when full | 3 | baskets (`SimTuning.BushBaskets`, `SimConfig.bushBaskets`) | FD-21 |
| Bush regrow time | 14 | game days to full (`BushRegrowDays`) | FD-21 |
| Forager minimum age | 8 | years (`ForagerMinAge`) | FD-23 |
| Forager hut store capacity | 30 | food items (`ForagerHutCapacity`) | FD-23b |
| Gather time per basket | 30 | game minutes (`GatherMinutesPerBasket`) | FD-24 |
| Hand carry per trip | 10 | items (`HandCarryItems`) | FD-24, MKT-03e |
| Plank hand carry per trip | 2 | planks, odd-jobber to a site (`HandCarryPlanks`) | PL-16 (PLACEHOLDER) |
| Other hand carry per trip | rods 10, baskets 5, panels 2 | items (`HandCarryRods`, `HandCarryBaskets`, `HandCarryPanels`) | ST-43 (PLACEHOLDER) |

Food values in the tables above also map to `SimConfig` fields: starting meat 400 (`StartingMeat`), adult 1 and child
0.5 rations per day (`AdultRations`, `ChildRations`), market range 630 m (`MarketRangeM`). The Foraging values are
PLACEHOLDER until playtested.

## Construction and timber

| Value | Default | Unit | Rule |
| --- | --- | --- | --- |
| Woodcutting hut | 8 logs, 960 | worker-minutes | CON-A1, WCO-04 |
| Oxen stand | 1 log, 60 | worker-minutes | CON-A1 |
| Starting planks | 10 | planks in the camp General goods store (`StartingPlanks`) | PL-14, ST-02 |
| Starting daub | 90 | daub in the camp General goods store (`StartingDaub`, save v38) | HL-04 |
| House (MVP-2) | 10 logs, 720 | worker-minutes | CON-A1, HM-06 |
| House footprint / plot size | 6 × 8 m / 60–400 m² | m / m² | HM-03, HM-04 |
| Granary | 6 logs, 600 | worker-minutes | CON-A1 |
| Storage yard | 8 logs, 480; 8 × 6 m; 300 items in total; 2 family slots | worker-minutes; `StorageYardLogs`, `StorageYardMinutes`, `StorageYardCapacity`, `StorageYardFamilySlots` | ST-03, ST-05, ST-08 (PLACEHOLDER) |
| Yard collect threshold | 0 | share of a workshop store full before the yard collects; 0 = any free item (`YardCollectThreshold`) | ST-42 |
| Workshop input reserve | 5 | items kept in storage per workshop input; 0 = none; each workshop can be set (`WorkshopDefaultReserve`) | PL-30b |
| Demolish refund / rebuild cost | 50% / 50% | of materials | BLD-11, MIL-08 |
| Standard log length | 4 | m | TMB-04 |
| Usable trunk length | 12–20 | m (3–5 logs) | TMB-06 |
| Ox load | 1 (owner 17/11/2026; was 4) | logs per trip (`SimTuning.OxLoadLogs`, saved in Core; exposed in `SimConfig` as `oxLoadLogs`) | WCO-07 |
| Workshop rates | hewing 3 logs/day → 1 beam each; sawing 3 beams/day → 3 planks each; input stock 10 each, hewing beam store 10, sawing plank store 20 | `SimTuning.HewingLogsPerDay`, `HewingBeamsPerLog`, `SawingBeamsPerDay`, `SawingPlanksPerBeam`, `*StockCapacity`, `*StoreCapacity` (saved in Core) | HW-02, HW-05, PL-05 |
| Workshop shift | 0 = today's daylight window | minutes; work minutes per input = shift ÷ inputs per day (`SimTuning.WorkshopShiftMinutes`; >0 overrides). Default gives ~3 a day in every season. A sawpit's 20-plank store holds 18 (3 per beam, 2 slots left over) and then shows Store full | HW-05 |
| Max builders per site | 4 | odd-jobbers building one site at once (`SimTuning.MaxBuildersPerSite`) | CN-23 |
| Deconstruct time share | 0.5 | of the recipe's build minutes (hut 480) (`SimTuning.DeconstructTimeShare`, saved in Core) | CN-53 |
| Deconstruct refund share | 0.5 | of the recipe's logs, rounded down (hut 4, oxen stand 0) (`SimTuning.DeconstructRefundShare`, saved in Core) | CN-54 |
| Log storage | 20 | logs at minimum size | INV-A3 |
| Woodcutting family slots | 2 | per hut | WCO |
| Fell / Delimb (off) / Buck | 90 / 30 / 15 per log | game minutes (1 game min = 1.25 real s at 1×) | woodcutting.md, TMB-01 |
| Load / unload ox | 10 / 10 | game minutes | woodcutting.md |
| Default work radius | 420 | m from the hut | WC-11 |
| Hut / log storage / oxen stand | 6×4 / 8×6 / 3×3 | m | WC-02 |

> Implementation note (Granary recipe, CON-A1 and FD-30): `ModuleTypes.Granary` uses 8 logs and 600 worker-minutes, as FD-30
> says. The Granary row above says 6 logs. Owner to confirm which, then fix the other.
>
> Implementation note (Trunk length and tree density, TMB-06, DEF-03): the 12–20 m trunk range is in both
> `SimTuning` (`TrunkLengthMinM`/`MaxM`, read by nothing) and `ForestScatter` (`MinTrunkM`/`MaxTrunkM`, which the
> scatter actually uses). Tree density (493 per hectare) and the seed are editor constants in `ForestScatter`, not in
> `SimTuning`. Owner to decide where these belong.

Woodcutting check (ox 1.04 m per game minute): trees 100 m from the log storage → ox round trip about
3.2 game hours of walking, plus loading → about 4 trips per 16 h summer day → about 4 logs per ox per day at
1 log per trip (16 at the old 4), before fetching the ox from its stand. A 10-log house now needs 10 ox trips.
