# Time, calendar and pacing

**Purpose:** turn ticks into game dates, sun times and real-time pacing. Pure, with no state except the pacer.

## Key types and files

| Type | File | Role |
|---|---|---|
| `GameCalendar` | `Assets/Sim/Time/GameCalendar.cs` | Tick → `GameTime`. 365-day year, no leap years; the year increments on 1 January. |
| `GameTime` | `Time/GameTime.cs` | Year, date, weekday, time of day, season. |
| `Season`, `Weekday` | `Time/Season.cs`, `Time/Weekday.cs` | Value types used by `GameTime`. |
| `Daylight` | `Time/Daylight.cs` | Seasonal sunrise and sunset from min/max daylight hours → `SunTimes`. Pure. |
| `TickPacer` | `Time/TickPacer.cs` | Owns the speed (`SetSpeed`, `CurrentSpeed`, `Speeds`). Turns real seconds into whole ticks (`Advance`) and keeps the remainder (`Fraction`). |

The namespace is `LordsLegacy.Sim.Calendar`, so it does not clash with `UnityEngine.Time`.

## How data moves

`SimHost.Update` calls `TickPacer.Advance(deltaTime)` → whole ticks → `SimWorld.Step(n)`. Over `MaxTicksPerFrame`
the excess is dropped and `SlowedDown` is set (the top bar shows "running slower"). While paused the host calls
`Reset()` so the carry does not build up. Pause lives in the world, not the pacer (decision 0006).

Sim code asks `GameCalendar` for `GameTime` and `Daylight` for sun times. Routine, work day and sunset logic use these.

## Extending

- A new calendar rule goes in `GameCalendar` as a pure function of the tick.
- Speed options go in `SimTuning` (speeds list); the top bar reads `Pacer.Speeds`.

## Gotchas

- One tick = one game minute. Real seconds exist only in `TickPacer`.
- The game starts on the date in `SimTuning` (start date). Speed is not saved, so a load starts at the default speed.

## Tests

`Sim.Tests/GameCalendarTests.cs`, `DaylightTests.cs`, `TickPacerTests.cs`.
