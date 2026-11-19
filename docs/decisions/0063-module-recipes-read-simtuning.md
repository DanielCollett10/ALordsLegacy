# 0063: Module recipes and workshop numbers read SimTuning

**Context.** M19, M20 and M21 each left gameplay numbers hard-coded in `ModuleTypes` (house, thatcher's hut, weaver,
wattle yard, daub pit, fence marker) and in the Weaving and Wattling `WorkshopDef`s. SimTuning fields for some of them exist
but are never read. That breaks the CLAUDE.md rule against hard-coded gameplay numbers. The owner confirmed on 19/11/2026.

**Decision.**
- Every recipe, build time, size and yield in `ModuleTypes` and every `WorkshopDef` reads `SimTuning`. The catalogs take the
  tuning (or are built from it per world), and no definition holds a literal gameplay number.
- New definitions do this from day one (for example `ModuleTypes.OxenPost` in M22c, ox-0 and ox-1).
- The existing debt is paid in **M24**. Until then, `arch-1`'s ignored "M24 debt" tests list each item, and each M24 fix
  turns one of those tests back on.
- Unused tuning fields (the Weaver*, Wattle* and ThatcherHut* fields that are saved but never read) are wired up, not deleted.

**Consequences.** Balancing becomes a SimConfig edit for every building. Pre-change sites keep their saved recipes
(`SiteRecipe`, MAT-40b), so old saves load unchanged.
