# 0058: SimLog is a debug ring buffer and is not saved

**Context.** M23 (labour, haul board) needs a way to see why the Sim chose what it did. Debug traces today are ad hoc.
CLAUDE.md requires new Sim state to have a save section (0034); a debug log should not bloat saves or break
byte-exact round trips. Owner direction 19/11/2026.

**Decision.**
- `SimLog` is an instance on `SimWorld` (no static state). Entries are `(tick, category, text)`; categories are a fixed
  enum (Tasks, Haul, Construction, Food, Growth, Placement, Commands, ...). Fixed-size ring buffer (size in `SimTuning`).
- Systems write through `world.Log.Write(category, ...)`; writing is skipped cheaply when the category is off.
- **No save section.** This is the one named exception to the 0034 rule. After a load the log is empty.
- Writing to the log must never change Sim state or random draws, so determinism tests are unaffected.
- Unity shows it in an F5 overlay with category filters. It is separate from `SettlementLog` (player messages).

**Consequences.** Save round-trip tests ignore `SimLog`. A reviewer must reject any rule that reads the log.
