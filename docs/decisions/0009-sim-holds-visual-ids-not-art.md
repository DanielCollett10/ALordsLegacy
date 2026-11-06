# 0009: The Sim holds visual ids, not art

**Context.** Art is placeholder now, and the owner's models arrive later. Swapping art must not touch
game code. The Sim cannot reference Unity types (decision 0001).

**Decision.** Things the Unity layer draws get their mesh and materials from a `VisualDefinition` asset,
found by string id through `VisualRegistry` (`TryGet`, or `Get`, which logs an error and returns a built-in
fallback). Sim data holds only the id, e.g. `visual.tree.generic`. Definitions stay marked `IsPlaceholder`
until real art replaces them, and a menu item lists the placeholders (ART-03).

**Consequences.** Replacing art means editing a definition, not Sim or view code. An unknown id shows a
visible fallback and an error log, not a crash. Ids are a contract: renaming one means updating the registry
asset. Today the tree id is a constant in `TreeRenderer`; no Sim data holds a visual id yet. Carried loads
(ART-A2) should use the same pattern when they arrive.
