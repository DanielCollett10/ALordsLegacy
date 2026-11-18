# 0053: A fence is its own footprint and blocks along its line

**Context.** M21 lets the player fence a built house (`design/household-levels.md` HL-10..13). Construction sites are
keyed by footprint id and a built footprint has no site record (`ConstructionSite`, decision 0033). Putting a site on
the house footprint would make the house "unbuilt" again and break move-in and occupancy. The owner wants a built fence
to be solid (HL-12): people go round it and in and out through the open road frontage.

**Decision.**
- `BuildFence` adds a new **footprint of type `footprint.fence`**, with no tree clearing and no placement checks
  beyond "the house is built and has no fence". Its rectangle is a small marker at the plot's frontage access point,
  which is also where materials are delivered. The marker itself does not block.
- The fence's real shape is **derived, never saved**: the plot polygon's edges minus the road frontage edges
  (`FenceLine.Of(plot)`), used for the panel count, drawing and blocking.
- Recipe: `ceil(length / FencePanelLengthM)` panels, `FenceMinutesPerPanel` each. The normal construction flow
  (SiteMaterials, hand carry, build progress) builds it; no new site kind.
- **Blocking:** once built, `NavGrid` marks every cell the fence line passes through as blocked. `NavSyncSystem`
  recomputes those cells when a fence is built or removed. While under construction it does not block.
- `HomeRecord.FenceFootprintId` (0 = none) links house and fence and is saved. State is derived: no id = None;
  site record = Building; no site record = Built.
- House removal (`HouseRemoval`) removes the fence too, refunding via the normal deconstruction share.

**Consequences.** No change to the construction registry. Nav cells are 2 m, so a fence blocks a 2 m strip; plots that
butt together share one blocked strip. Fences can wall off ground behind rows of plots; moves with no path fail as
today. A later house upgrade can use the same separate-footprint trick.
