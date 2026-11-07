# 0011: Player commands go through the Sim and are validated there

**Context.** Roads and areas are the first player actions that change Sim state. The tech spec says
tools only produce commands and the Sim re-validates them (CMD-01, CMD-02). Drawing must work while paused.

**Decision.**
- Every player action is a small command class in `Assets/Sim/Commands/` (e.g. `DrawRoad`, `DeleteRoadSegment`,
  `DesignateArea`, `DeleteArea`). Commands hold plain data only (ids, integer-mm points, type ids).
- `SimWorld.Execute(command)` validates and applies the command **immediately and synchronously**, between ticks,
  and returns a `CommandResult` (accepted with the created/removed ids, or rejected with a reason code and text).
  Nothing changes on rejection.
- Execute works while paused. It never advances time. It records `(tick, command)` in an in-memory command log
  (CMD-03) for debugging and future replay.
- The same validator classes are called by the Unity tools for the live preview (`Validate` without applying), so
  the preview and the Sim cannot disagree.
- Unity code calls `Execute` only through `SimHost`.

**Consequences.** Determinism holds: a command runs at a known tick and the same commands give the same world.
Tests can drive every rule without Unity. A queue that applies commands at the start of the next tick can be added
later (e.g. for networking) without changing command classes.
