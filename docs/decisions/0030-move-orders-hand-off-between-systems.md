# 0030: Move orders are the hand-off between systems

**Context.** M7 has more than one system that walks people: `RoutineSystem` (milling, going home), the debug
send-to (decision 0026), and M8 work will add more. They must not fight over one citizen's walk, and movement must not
need to know who asked for it.

**Decision.**
- A citizen or ox walks only because someone set its `Move` (a `MoveOrder`, target in metres). The setter creates it.
  `MovementSystem` plans and advances orders and never chooses a target itself.
- An order is owned by whoever set it. A system that finds `Move` set and unfinished by someone else leaves the agent
  alone (`RoutineSystem` does this). A finished order stays on the agent until its owner clears it.
- A system that needs a citizen exclusively (debug send-to) marks that in its own state on the citizen, and the routine
  checks that state first (decision 0026).

**Consequences.** Any new walking task gets correct handling with no change to `MovementSystem`. The cost is that a
busy citizen ignores new orders until the current one finishes, so a system that must interrupt has to clear or own the
order explicitly.
