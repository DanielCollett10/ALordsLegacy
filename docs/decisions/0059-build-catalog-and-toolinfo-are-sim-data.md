# 0059: The build catalog and ToolInfo are Sim data

**Context.** Build bar entries are hard-coded in `ToolEntry.Defaults()` with hand-written tooltips. The spec wants bars
generated from building definitions (UIW-25) and tooltips that show cost, needs and produces (ui.md UIS-05).

**Decision.**
- A `BuildCatalog` in `Assets/Sim/Catalog/` lists every placeable thing as a `BuildEntry`: tool id, label, category
  (Road, House, Processing, Logistics, Gathering, Agriculture), order, and the definition it places (module type,
  operation type or structure).
- `ToolInfo.Of(world, entryId)` derives a plain read model: cost (from the recipe), needs (inputs, workers, area),
  produces (outputs), and the current "why not" reason. Nothing in it is hand-typed when a definition already says it.
- Unity `BuildBarView` and the tooltip service read only `BuildCatalog` and `ToolInfo`. `ToolEntry.Defaults()` goes.
- Labels are presentation text kept beside the definition; renaming (e.g. "Sawyers yard") is a catalog edit.

**Consequences.** A new building appears on its bar by adding one catalog line. Category is Sim data, so a future
3D view or a test can read it. Catalog contents need no save section (they are code definitions, not state).
