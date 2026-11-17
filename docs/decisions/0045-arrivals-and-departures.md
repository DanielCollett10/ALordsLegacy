# 0045: Arrivals and departures

**Context.** M16 Growth (`design/growth.md`) adds families that walk in and walk out. Many systems read families
(routine, work day, dispatch, food trips, move-in, operations), so we need one clear way to mark a family as passing
through, and a way to tell the player what happened.

**Decision.**
- A family has a `Passage` state: `None`, `Arriving` or `Leaving` (saved in the Population section). Every reader that
  hands out work, food trips or a house skips a family whose state is not `None`; `RoutineSystem` leaves its members alone.
- Leaving starts in one place, `FamilyDeparture.Begin`, which reuses the unassign path (shared helper, also used by the
  `UnassignFamily` command), frees the house, cancels tasks and reservations and drops what members carry.
- Map exits are **derived, never saved**: points where a road polyline reaches the map edge (`MapExits`). With none, the
  nearest map-edge point. Arrivals enter at the trunk road's map-edge end.
- An arriving family **reserves** its house in `HomeRegistry` (saved in the Homes section). `MoveInSystem` never gives a
  reserved house to anyone else.
- Arrival and departure messages go into a small Sim **settlement log** (tick + text, last 20, saved). The Unity layer
  decides how long to show them. No UI-only event list.

**Consequences.** Adding a new family reader means checking `Passage`. Removing a citizen happens only when it reaches
the exit; the family record goes with its last member. Deaths (later) can reuse the removal path.
