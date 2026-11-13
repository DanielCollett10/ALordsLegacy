# 0033: Construction sites are records keyed by footprint, with one generic system

**Context.** M10 adds construction (`docs/design/construction.md`). MVP-2 needs the same thing for houses and
the granary, so it must not live inside the woodcutting code. Modules today are `Footprint`s built at once.

**Decision.**
- The recipe (`ConstructionRecipe`: item counts + build worker-minutes) is data on `FootprintTypeDef`. An empty
  recipe means "built at placement", so log storage and the camp keep today's behaviour.
- `SimWorld.Construction` holds one `ConstructionSite` per unbuilt footprint (footprint id, state, reservation ids,
  ledger location of the site pile, build progress, priority). A footprint with no site record is Built.
  Anything that asks "is this module working?" asks `world.IsBuilt(footprintId)`.
- Materials sit in a ledger location of kind `Site` (decision 0032), so the ledger audit and the log renderer cover them.
- One `ConstructionSystem` (operations phase) opens Deliver and Build tasks from each site's state (decision 0031
  style). It knows nothing about woodcutting. Deliver reuses the M9 ox step kinds.
- Construction tasks use `EligibilityPolicy.HelpersOnly` (decision 0005).

**Consequences.** Houses and the granary only add footprint types with recipes. Save/load (M11) stores the
site records with the ledger. Placement commands for any footprint type must run the affordability check and
reservation together (construction.md CN-10..12).
