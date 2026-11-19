# 0051: Workshop recipes take several inputs; hand inputs share the haul task

**Context.** The weaver needs one input (willow rods) and the wattle yard needs two (hazel and willow rods). A workshop
with no input is also possible. Oxen carry logs and beams, but rods, baskets and panels are light and go by hand
(`design/materials.md` MAT-20..24, `design/storage.md` ST-43).

**Decision.**
- A `WorkshopDef` recipe holds a list of `RecipeInput`s (item, count, `CarryClass`, stock cap). It may hold none.
  Hewing, sawing, weaving and wattling are all data on the same def type.
- Each input has its own `WorkshopStock` location in the building. A batch starts only when every input holds one
  batch and the output store has room for one batch. Inputs are consumed and outputs produced on finish.
- The fetch choice takes the input with the lowest share of its stock (ties in recipe order). Ox inputs use
  `task.fetch_input`. Hand inputs use the shared `task.haul_by_hand`, one open trip per working family.
  The task id stays `haul_by_hand`; no separate workshop task id is added.
- Rods come from the nearest coppice hut or working yard store.

**Consequences.** A new workshop is a new def with no new system. Weaver and wattle numbers are still hard-coded in
`WorkshopDef`, not read from `SimTuning` (see the implementation note in `design/materials.md`). Save v35 added per-input stock.
