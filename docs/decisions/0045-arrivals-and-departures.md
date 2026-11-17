# 0045: Arrivals and departures

**Context.** M16 Growth (`design/growth.md`) adds families that walk in and walk out. Many systems read families
(routine, work day, dispatch, food trips, move-in, operations), so we need one clear way to mark a family as passing
through, and a way to tell the player what happened.

**Decision.**
- A family has a `Passage` state: `None`, `Arriving` or `Leaving` (saved in the Population section). Readers that hand
  out work, food trips or a house check one helper, `Family.IsInPassage`; `RoutineSystem` leaves its members alone.
- Leaving starts in one place, `FamilyDeparture.Begin`: shared unassign helper (also used by `UnassignFamily`), each
  open task failed through the step runner's normal fail path (so per-task drops like `FailStock` and `FailFoodTrip`
  run), the house freed without moving its larder, and carriers emptied with `ForagingSystem.DropAll` (moved to a shared helper).
- **Map edge:** a point within `MapEdgeToleranceM` (tuning, 2 m) of the terrain bounds. A **road exit** is any road
  node (segment end or junction) on the edge. Exits are **derived, never saved** (`MapExits`). With no road exit, the
  nearest walkable edge point.
- **Entry point is saved, not re-derived:** `StartLayoutApplier` computes `SimWorld.EntryPoint` once at a new game
  (via `MapExits.ComputeEntry`) and the Core section saves it. Loads never need the start layout, and road edits
  later do not move it. Exits for leaving families are derived; a Leaving walk keeps its target in the saved `MoveOrder`.
- **Entry point:** the trunk road's end on the map edge. The default layout's 80 m trunk road is extended to the nearest
  edge (owner, GR-24b); final maps will ship with roads to the edges (FG-09). As a safety net only, if a trunk road does
  not reach the edge the entry is the walkable edge point nearest its closer end.
- **System order (routine phase):** `EatingSystem` (midnight) → `DepartureSystem` → `ArrivalSystem` (dawn). Departures are
  decided before arrivals on any tick.
- An arriving family **reserves** its house in `HomeRegistry` (saved in the Homes section). `MoveInSystem` never gives a
  reserved house to anyone else. Arrival food is **new supply**: `Produce`d into the family's own larder at spawn
  (journalled, so `Audit` passes).
- Arrival and departure messages go into a small Sim **settlement log** (tick + text, last 20, saved).

**Consequences.** A new family reader must call `IsInPassage`. Citizens are removed only at the exit; the family record
goes with its last member; views must cope with ids disappearing. Deaths (later) can reuse the removal path.
