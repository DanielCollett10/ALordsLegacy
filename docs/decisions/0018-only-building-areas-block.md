# 0018: Only building areas block; work areas stay out of the building index

**Context.** Two area kinds exist (docs/design/areas.md): building areas block other building areas and roads and
clear trees; work areas may overlap anything and never clear trees or land (WORK-03, WORK-05, BLD-10-B). The area
validator, the road check and tree clearing all need one shared rule for which areas take part.

**Decision.**
- `AreaTypeDef` carries the kind (`Building` or `Work`), `ClearsTrees` and `MaxSlopeDegrees`. Types are built in code
  (`AreaTypes`) until the definition pipeline exists.
- Only building-kind areas are added to the `SpatialGrid` in `AreaRegistry`. The overlap check (AR-15) and the road
  crossing check (RD-13) query that grid, so work areas never block either.
- `AreaValidator` returns Valid for work kinds after the shape and size checks; slope, overlap and road checks are skipped.
- Confirming a building area removes the trees inside it. Work areas never remove trees.

**Consequences.**
- A new area kind that must block gets its kind set to Building in its type definition. No special case goes in the validator.
- `AreaTypeDef.BlocksRoads` is stored but nothing reads it: the road check uses the building index. Remove the flag or wire it in.
- A work area that must block something later (e.g. a felling zone) is a new decision, not a flag change.
