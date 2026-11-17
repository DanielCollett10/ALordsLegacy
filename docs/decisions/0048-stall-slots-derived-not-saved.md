# 0048: Stall slots are derived from the market polygon and not saved

**Context.** A market holds one stall per family, and each stall needs a free slot. The slot grid depends only on the
market's polygon and the tuning sizes. Saving the grid would let it drift from the polygon, and a stall that moved with
its slot would need extra save state.

**Decision.**
- `StallGrid.Slots(polygon, tuning)` is a pure function of the market polygon and the cell sizes. `StallSystem` recomputes
  it each time it needs a stall slot, then takes the first slot no footprint occupies. Nothing about slots is stored.
- A stall's saved record is its footprint and its family link (`Markets` section, id 11).
- Slots keep a 2 m clear strip from every edge (`EdgePaddingMm`), so stalls never touch the market boundary.

**Consequences.** Changing the polygon or the cell size moves the free slots without a save change. Stall placement
depends on the free-footprint check, not on a stored slot list. Grid tests are in `Sim.Tests/StallGridTests.cs`.
