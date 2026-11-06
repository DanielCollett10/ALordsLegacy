# Architecture

_Living document. Agents update it when they change how parts fit together. Each task edits only
the section it belongs to; the section headings below are fixed and keep this order._

## Layers

| Layer | Folder | Namespace / assembly | Depends on | Tested by |
|---|---|---|---|---|
| Simulation core | `Assets/Sim/` | `LordsLegacy.Sim` (asmdef `LordsLegacy.Sim`, `noEngineReferences`) | nothing | `Sim.Tests/` via `dotnet test` |
| Unity layer | `Assets/Game/` | `LordsLegacy.Game` | `LordsLegacy.Sim`, UnityEngine | Unity EditMode/PlayMode tests |
| Editor tooling | `Assets/Editor/` | `LordsLegacy.EditorTools` | UnityEditor | by hand / editor lane |

## Simulation core

_Owner: the task that changes the Sim core. Other tasks do not edit this section._

- `Assets/Sim/Core/SimWorld.cs` (`LordsLegacy.Sim.Core`) owns the tick clock. `Step(n)` advances
  time; nothing else does. `Pause()` and `Resume()` set whether `Step` does anything.
- Pure C#, no `UnityEngine`. Tests live in `Sim.Tests/` and compile the same `Assets/Sim/**/*.cs`.
- `Assets/Sim/Time/Daylight.cs` (`LordsLegacy.Sim.Calendar`): seasonal sunrise and sunset from plain
  min/max daylight hours. `For(dayOfYear)` returns `SunTimes`; `IsDaylight(GameTime)` and
  `SunProgress(GameTime, tickFraction)` serve the lighting view. Pure, no state.
- `Assets/Sim/Tuning/SimTuning.cs` (`LordsLegacy.Sim.Tuning`) holds every PLACEHOLDER number from
  `docs/design/tuning.md`. Defaults are instance properties set by object initializer; derived
  values are computed properties. The Sim never hard-codes these numbers.
- `Assets/Sim/Time/TickPacer.cs` (`LordsLegacy.Sim.Calendar`) owns the selected speed (`SetSpeed(index)`,
  `CurrentSpeed`, `Speeds` from tuning) and real-time pacing. `Advance(realSeconds)` returns the whole
  ticks due and carries the rest (`Fraction`, in [0,1)). Capped at `MaxTicksPerFrame`; over the cap it
  drops the excess and sets `SlowedDown`. `Reset()` clears the carry-over (the host calls it while paused).
  The pacer has no pause state; `SimWorld` owns pause (decision 0006).

## Map pipeline

_Owner: the map and terrain tasks. Other tasks do not edit this section._

- Terrain and trees are baked into Sim map data (decision 0003). The Sim never asks Unity for
  terrain; an editor bake step writes a versioned map file that the Sim reads.
- `Assets/Sim/Terrain/`: `IHeightField` (HeightAt, SlopeDegAt, Contains, Bounds; out-of-bounds queries clamp)
  is the only terrain API Sim code uses. `HeightGrid` (metres, bilinear, central-difference slope; ~36 MB for
  6 km at 2 m) and `FlatHeightField` (no map) implement it.
- Planned Sim types: a
  tree list (stable ID, x/z position, usable trunk length).
- Coordinates: Sim x/z equal Unity world x/z in metres; Sim height equals Unity world y.

## Unity layer

_Owner: the Unity-layer tasks. Other tasks do not edit this section._

- `Assets/Game/` reads Sim state and draws it; it turns player input into Sim commands and holds
  no game rules (see `CLAUDE.md`).
- (Describe the view: how Sim state becomes something on screen.)

## Data flow

_Owner: whichever task changes the flow between layers. Other tasks do not edit this section._

Player input → Unity layer → Sim command → `SimWorld.Step` → Sim state → Unity layer renders.

## Scene

_Owner: the scene and prefab tasks. Other tasks do not edit this section._

- (Describe the scenes and how they are generated: Editor scripts write scenes and prefabs.)
