# 0034: Save format, round-trip testing and loading by scene reload

**Context.** M11 adds save/load (`docs/design/saving.md`, tech spec SAV-01..06). The Sim has many registries with
private state, id counters that are never reused, in-flight move orders and reservations.

**Decision.**
- Our own binary format (SAV-03): magic `LLSV`, int32 format version, a header (name, created real time written
  by Unity, game tick, map id, map content hash), then one **section per subsystem** (section id + byte length), so
  sections are written and read independently. Code in `Assets/Sim/Save/` (`SaveWriter`/`SaveReader`, `SimSave.Write(world)`
  / `SimSave.Read(bytes, mapData)`).
- The map itself is not in the save: terrain and the original trees come from the map file; the save stores tree
  state changes (removed, felled, stump, reservations). The map is found by id and hash (saving.md SV-30).
- Everything else is saved exactly, including id/name counters, `SimRandom.State`, move orders with their paths,
  routine/work-day state, the ledger journal, and the tuning and type catalogs the world was created with, so a load
  continues identically even if `SimConfig` was edited since. Applying new tuning to an old save is out of scope.
- `CommandLog` is not saved.
- **Round-trip test (SAV-06):** `Write(Read(Write(w))) == Write(w)` byte for byte, and both worlds stepped for a game
  day must write identical bytes again. Every new Sim system must extend its section and these tests pass.
- **Unity loads by reloading the Game scene** with the save bytes handed to `SimHost`, so renderers always build
  from a fresh world (SAVE-02) and never handle a world swap.

**Consequences.** Each task that adds Sim state adds it to a save section in the same task (to go into CLAUDE.md
after M11). Registries get internal restore methods for the reader.
