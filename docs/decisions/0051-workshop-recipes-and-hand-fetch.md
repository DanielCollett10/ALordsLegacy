# 0051: Workshop recipes with several inputs, no inputs and hand fetch

**Context.** Decision 0047 made workshops data with one input item fetched by ox. M19 needs a wattle panel from
6 hazel + 2 willow rods and a basket from 6 willow rods, with rods fetched by hand (`design/materials.md` MAT-22..24).
M21 needs a daub pit with no input at all (`design/household-levels.md` HL-01).

**Decision.**
- `WorkshopDef` holds a **recipe**: a list of `(input item, count)` (may be empty) and `(output item, count)` per batch,
  plus **batches per family-day**. Hewing becomes 1 log → 1 beam, sawing 1 beam → 3 planks: same numbers, no play change.
- Input stock is one `WorkshopStock` location **per input item**, each with its own capacity from the def.
- A work task starts only when every input of one batch is in stock and the output store has room for one batch;
  both are reserved at start, as today.
- Each input has a **carry class**: Ox (logs, beams; `task.fetch_input`, construction first) or Hand (rods;
  a new `task.fetch_input_hand` done by the workshop's own family, load from the item's hand load in tuning).
- The workshop fetches the input it is **shortest of** first (lowest share of its capacity).
- Each input carries a **village reserve** (`design/planks.md` PL-30..33, added in M18 before this refactor): no
  fetch that would leave less than the reserve in free village stock. The refactor keeps it per input.
- Status *Waiting for <item>* names the first missing input.

**Consequences.** Weaver, wattle yard and daub pit are data plus a footprint type. Hewing and sawpit tests must still pass
unchanged after the refactor; the refactor bumps the save version once.
