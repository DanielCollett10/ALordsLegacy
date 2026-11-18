# 0053: A fence is its own non-blocking footprint

**Context.** M21 lets the player fence a built house (`design/household-levels.md` HL-10..13). Construction sites are
keyed by footprint id and a built footprint has no site record (`ConstructionSite`, decision 0033). Putting a site on
the house footprint would make the house "unbuilt" again and break move-in and occupancy.

**Decision.**
- `BuildFence` adds a new **footprint of type `footprint.fence`**: non-blocking, no tree clearing, no placement checks
  beyond "the house is built and has no fence". Its rectangle is a small marker at the plot's frontage access point,
  which is also where materials are delivered.
- The fence's real shape is **derived, never saved**: the plot polygon's edges minus the road frontage edges
  (`FenceLine.Of(plot)`), used for the panel count and for drawing.
- Recipe: `ceil(length / FencePanelLengthM)` panels, `FenceMinutesPerPanel` each. The normal construction flow
  (SiteMaterials, hand carry, build progress) builds it; no new site kind.
- `HomeRecord.FenceFootprintId` (0 = none) links house and fence and is saved. State is derived: no id = None;
  site record = Building; no site record = Built.
- House removal (`HouseRemoval`) removes the fence footprint too, refunding via the normal deconstruction share.

**Consequences.** No change to the construction registry. A later house upgrade (renovation) can use the same trick:
a separate upgrade footprint at the house, so the house itself stays built while the work happens.
