# 0024: SimWorld is partial and systems run in one fixed list

## Context
Many milestone tasks add world state and per-tick logic to `SimWorld`. If every task edits `SimWorld.cs`, they
conflict, and the order in which logic runs is left to chance. CLK-07 needs a fixed order. CLAUDE.md forbids static
mutable state and reflection-based discovery is not allowed in Sim code.

## Decision
- `SimWorld` is `partial`. Each subsystem puts its state in `Assets/Sim/Core/SimWorld.<Area>.cs`.
- Per-tick logic is an `ISimSystem` (`Step(world)`, `OnCommandApplied(world, result)`). The one ordered list is
  `SimSystems.CreateDefault()` in `Assets/Sim/Core/SimSystems.cs`. Order: sync, routine, dispatch, steps, movement,
  operations.
- The world copies its list at construction. `Step` runs every system once per tick. `Execute` notifies systems after an
  accepted command only.

## Consequences
- Adding a system means one line in the right phase block of `SimSystems.cs` and a file of its own. `SimWorld.cs` stays
  stable.
- A paused world runs no systems on `Step`. Commands still notify while paused.
- Systems keep no static state. Anything they hold lives on the world or on their own instance.
- Tests can pass their own ordered list through the `systems` constructor argument.
