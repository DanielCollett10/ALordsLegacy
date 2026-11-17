# 0047: Workshops are data

**Context.** M17 adds two standalone production buildings with the same shape: the hewing station (log → beam) and
the sawpit (beam → 3 planks). M19 adds the weaver and the wattle yard, which work the same way. Writing a system per
building would copy the same fetch, work and status code four times.

**Decision.**
- A **workshop** is an area-less operation (like the granary, decision 0041) whose type carries a `WorkshopDef`:
  input item, output item, outputs per input, inputs per working day per family, input stock capacity, output
  capacity, and how inputs arrive (ox for heavy items, hand for light ones, decision 0046).
- One `WorkshopSystem` serves every workshop: it opens input-fetch tasks from the nearest source with free stock
  (ground piles, stores, or another workshop's output store) and **one work task per assigned family** at a time.
  Output is fixed per family per day, however many members work.
- Status (No workers, Waiting for <input>, Waiting for ox, Store full) is computed from the def, never stored.
- A new workshop is a footprint type, an operation type and a `WorkshopDef`. No new system.

**Consequences.** The hewing station and sawpit share all code; the weaver and wattle yard are mostly data. Recipes
with several inputs (e.g. a future wattle panel from hazel and willow) need `WorkshopDef` to grow a list of inputs.
Do that when the first such recipe is built.
