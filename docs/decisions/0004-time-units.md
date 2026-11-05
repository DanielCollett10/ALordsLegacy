# 0004: Time and speed units in the Sim

**Context.** The GDD mixes real seconds, game seconds and "worker-seconds". With a
1 game-minute tick (CLK-01), anything shorter than a minute is meaningless, and the real-time
pace (how many game minutes pass per real second) is still being tuned (TIME-08).

**Decision.**
- The Sim tick is 1 game minute. The Sim only knows ticks and game time.
- Every duration in data is in **game minutes** (stage durations, build work, regrowth).
- Work amounts are **worker-minutes** of game time (e.g. a hut needs 960 worker-minutes:
  2 workers for 8 game hours). Work only counts while its materials are on site.
- Every speed in data is in **metres per game minute**.
- How many game minutes run per real second at 1× is a single tuning value
  (`GameMinutesPerRealSecondAt1x`, today 32/60), used only by the Unity layer's tick pacer.

**Consequences.** Retuning the real-time pace never changes Sim data. Walking at 5 km/h
on screen at 1× with a 32× clock is 2.6 m per game minute; if the pace changes, walking
speed data is re-derived from that once, in the tuning file, not in code.
