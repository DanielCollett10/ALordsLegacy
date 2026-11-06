# 0002: Plain C# simulation; Unity DOTS only for rendering

**Status: PROVISIONAL.** Being verified by the performance spike (M1, epic "Architecture
check"). Locked or revised once `docs/findings/perf-spike.md` exists.

**Context.** Tech spec v0.2 (ARC-02, ENG-02, DEV-11, NAV-08) puts all game state in Unity
ECS/DOTS with Burst jobs. Decision 0001 and `CLAUDE.md` put all rules in `Assets/Sim` as
plain C# with no UnityEngine, tested with `dotnet test`. The owner asked the lead to choose.

**Decision.** The simulation stays plain C# (0001). It overrides ARC-02, ENG-02 (for sim),
DEV-11 (assembly list) and NAV-08 (Burst A*). Unity DOTS / Entities Graphics may be used
**only in the Unity layer** to draw large sets (trees, distant characters, PRS-01/03).
Everything else in the tech spec (commands, events, ledger, tasks, geometry, saving)
still applies, implemented as plain C# in `Assets/Sim`.

**Why.** Agents build and test sim code in seconds without opening Unity; save/load is our
own serialiser anyway (SAV-03); ticks are rare (1 game minute, ~0.5/s at 1×, ~17/s at 32×),
so moving thousands of citizens per tick is cheap with sensible data layout. The target
is now 5,000–10,000 citizens (gdd-amendments.md); the perf spike measures at 10,000.

**Consequences.** Sim uses its own math types. If profiling (P1, P5) shows a hot loop
(pathfinding) is too slow, optimise it inside the Sim first (caching, hierarchical search,
parallel pure queries applied in a fixed order). Escape hatch: the Sim defines an interface
(e.g. `IPathfinder`) and the Unity layer may supply a Burst implementation, while tests use
the plain C# one. Using it needs a new decision. Tech spec sections that say "ECS" mean "Sim state".
