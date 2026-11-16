# 0041: The granary is an operation without an area

**Context.** The granary is placed as one piece with no drawn area (FD-30). Players still need to assign families to it
(FD-32), and the existing AssignFamily / UnassignFamily commands, the family slot rule and the Workplace text all work on
operations. A second, parallel "workplace" concept would duplicate that.

**Decision.**
- `PlaceStructure(granary)` also creates an `operation.granary` operation with `AreaId` 0 and the footprint as its only module.
- `AreaId` 0 means "no area": `OperationRegistry.FindByArea(0)` never matches, so any number of granaries can coexist.
  `OperationTypes.FindByBuildingArea` never returns the granary type (null building area type).
- The granary footprint is the operation's hut: AssignFamily refuses until it is built, then allows 2 slots (PLACEHOLDER).
- `OperationRegistry.FindByModule(footprintId)` finds the owner of a module.
- Removing the granary (finished deconstruction, or cancelling the unbuilt site) unassigns its families and removes the
  operation (`DeconstructionWork.RemoveGranaryOperation`).
- Its build and deconstruct tasks keep owner `HouseOwnerId` (-1), so they stay helpers-only like a house's.

**Consequences.** No new saved state: the operation uses the existing operations section. Woodcutting, foraging and haul
systems skip it by type. Moving-in ignores families whose operation has no area. Granary work comes with M15 (MKT-03b).
