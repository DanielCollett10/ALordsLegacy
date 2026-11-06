# 0006: Who owns the clock, pause and game speed

**Context.** Pause appeared in two places (T-001 on `SimWorld`, and the tick pacer), and the
selected speed had no owner. The template `SimWorld` also counted real seconds
(`SecondsPerTick`), which contradicts decision 0004.

**Decision.**
- `SimWorld` owns **game time and pause**. One tick = one game minute. It is built from a
  `SimTuning`, exposes `Tick`, `Now` (a `GameTime`; not called `Time`, to avoid confusion with
  `UnityEngine.Time`) and `Paused` (from T-001), and has no notion of real seconds. `Step` does
  nothing while paused; commands may still be queued (CLK-06).
- Calendar and pacing types live in `Assets/Sim/Time/` under the namespace
  `LordsLegacy.Sim.Calendar` (not `...Time`, which would shadow `UnityEngine.Time`).
- `TickPacer` owns **the selected speed** and real-time pacing: `SetSpeed(index)`,
  `CurrentSpeed`, `Advance(realSeconds)` returning ticks due, `Fraction`, and a `SlowedDown`
  flag (CLK-05). It has no pause state of its own.
- The Unity **`SimHost`** has `Tick(float realSeconds)`: if `world.Paused`, run nothing (and
  reset the pacer's carry-over); otherwise `world.Step(pacer.Advance(realSeconds))`. `Update()`
  calls `Tick(Time.deltaTime)` unless `AutoUpdate` is off. Tests turn `AutoUpdate` off and call
  `Tick` with fixed values, and can call `Initialize(SimTuning, MapData)` to start at any date
  or on a small synthetic map. No test depends on real frame time.
- UI: the pause button calls the host, which pauses or resumes `SimWorld`; speed buttons call
  `pacer.SetSpeed`.

**Consequences.** One source of truth for pause (saved with the world); speed is a
presentation/session setting and is not part of the save. Unity-side tests are deterministic.
