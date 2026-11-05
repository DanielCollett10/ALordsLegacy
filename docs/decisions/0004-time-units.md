# 0004: Time and speed units in the Sim

**Context.** The GDD mixes real seconds, game seconds and "worker-seconds". With a
1 game-minute tick (CLK-01), anything shorter than a minute is meaningless, and the real-time
pace (how many game minutes pass per real second) will keep being tuned (TIME-08, TIME-A2).

**Decision.**
- The Sim tick is 1 game minute. The Sim only knows ticks and game time.
- Every duration in data is in **game minutes** (stage durations, build work, regrowth).
- Work amounts are **worker-minutes** of game time (e.g. a hut needs 960 worker-minutes:
  2 workers for 8 game hours). Work only counts while its materials are on site.
- Every speed in data is in **metres per game minute**.
- How many game minutes run per real second at 1× is a single tuning value
  (`GameMinutesPerRealSecondAt1x`, default 1.6 = a 15-minute day, TIME-A2), used only
  by the Unity layer's tick pacer.

**Consequences.** Retuning the pace never changes Sim code. Speeds that should *look*
real at 1× are derived once in the tuning data: at 1.6 game min per real second, walking at
5 km/h on screen is 0.87 m per game minute, and an ox at 3 km/h is 0.52. At 32× the Sim runs
about 51 ticks per real second, so per-tick work must stay cheap (pathfinding on demand and
cached, not per tick).
