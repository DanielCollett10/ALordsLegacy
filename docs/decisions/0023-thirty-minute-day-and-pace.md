# 0023: A 30-minute day at 1×, with walk speeds tied to it

**Context.** TIME-A2 set a 15-real-minute game day. At real walking speed (5 km/h on screen) that is 0.87 m per
game minute, so a 300 m commute took 5.7 game hours and woodcutting only worked within about 100 m of camp. The
owner wanted distances to cost less without anyone looking faster at 1× (`movement.md` MOV-20..23, 10/11/2026).

**Decision.**
- `RealMinutesPerGameDayAt1x` = **30**. The clock runs 48× real time at 1×.
- Walk and ox speeds in m per game minute follow from the on-screen pace:
  `km/h × 1000 ÷ 60 × (RealMinutesPerGameDayAt1x × 60 ÷ 1440) ÷ 60`. That gives walk 1.74 and ox 1.04. If the day
  length changes, both speeds change in the same commit.
- Dirt path multiplier 1.25 (was 1.5). Rest day off until churches exist (FOOD-A4b).
- Commutes happen outside daylight, with a 6-hour minimum sleep (`people.md` PPL-25/26).

**Consequences.** A year is 182 h at 1× and 5.7 h at 32×. About 26 ticks per real second at 32× (was 51), so tick
cost has more headroom. Every test or tool that hard-codes 15 minutes, 0.87, 0.52 or 1.5 must read the tuning value
instead (m7-pace, m7-pace-asset). Sunrise and sunset times are unchanged, because they are in game time.
Derived tables in `tuning.md` were recomputed.
