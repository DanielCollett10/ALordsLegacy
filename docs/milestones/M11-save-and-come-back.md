# M11: Save and come back

**Shipped (player-visible):** pressing Esc opens the game menu (Resume, Save, Load, Options, Quit). The Sim pauses while it is
open and the game restores its pause state on Resume. **Save** asks for a name, pre-filled with the game date, and writes a
new save file. **Load** lists saves newest first (name, real date and time, game date) and reloads the game exactly as it was
saved, including people mid-walk, oxen mid-haul, half-built sites, the clock and pause. Speed resets to the default. A save made
on a different version of the test map is refused with a clear message. **Autosave** overwrites one `Autosave` slot every 30 real
minutes while a game runs and the menu is closed. Options and Quit are shown but disabled.

**Design source:** `docs/design/saving.md` (SV-01..34, implementing GDD SAVE-01/02 and tech spec SAV-01..06). Technical shape:
decision 0034.

## Main technical changes by system

- **Sim save format (`Assets/Sim/Save/`):** `SimSave.Write` / `SimSave.Read`, `SaveWriter` / `SaveReader`, magic `LLSV`,
  int32 format version (1 at M11), a header (name, real creation time from the caller, game tick, map id, map content hash),
  then sections, each as id + byte length. Format core and restore path: T-358, T-360 (`SimWorld.ForRestore`). Sections
  T-361..T-367, full save and round-trip tests T-368.
- **Sections (fixed order):** Core (1: tick, pause, random state, `SimTuning`, road and area catalogs), Land (2: trees, roads,
  areas), Structures (3: footprints, camp, operations, construction sites), Ledger (4: locations, reservations, journal,
  id counters), Population (5: families, citizens, oxen), Movement (6: move orders, follow links, routine, work day, debug
  send), Tasks (7: task records by def id, steps, progress). Read ends with a full `Nav.Sync`.
- **Map check (SV-30):** `MapData.ContentHash` (FNV-1a of the map file bytes). `SimSave.Read` throws
  `SaveMapMismatchException`, whose text is the SV-30 message.
- **Tuning kept (SV-14):** the Core section stores the tuning and catalogs the world was created with, so a load does not
  follow later `SimConfig` edits.
- **Unity menu (`Assets/Game/UI/GameMenu/`):** `GameMenuView` and `GameMenuState` (Esc, pause and restore, input blocking,
  T-370), `SavePanelController` (name, Confirm, "Saved" note; T-371), `LoadPanelController` (newest first, map check, sets
  `PendingLoad`, reloads the scene; T-372).
- **Unity save files (`Assets/Game/Saving/`):** `SaveStore` (files in `persistentDataPath/Saves/`, header-only list),
  `PendingLoad` (bytes handed to the next scene), `SimHost` takes them on start, so renderers always build from a fresh world.
- **Autosave (SV-11):** `AutosaveTimer` (plain C#, real-time count, runs only while a world is loaded and the menu is closed;
  Sim pause still counts), `AutosaveController` (30 minutes by default, `Autosave.llsave`, "Saved" note for 2 s). Any manual
  save restarts the count. Wired into the Game scene by T-373 and T-374.
- **Process rule:** new Sim state goes into a save section in the same task and must pass the round-trip test (CLAUDE.md,
  T-369).

## Decisions made

- 0034 save format: own binary format with sections, Sim-only restore through `ForRestore`, and loads by scene reload.
  Status updated in T-369.
- Owner decisions recorded in `saving.md`: SV-01 (menu contents), SV-11 (30-minute autosave, one slot), SV-14 (save keeps its
  tuning), SV-20 (newest-first list).

## Known gaps and debt

- **Unreadable saves are hidden, not shown.** `SaveStore.List` skips any file whose header cannot be read, so a save from another
  format version or another map does not appear in the Load list. SV-34 (greyed row with its reason) is M12 work.
- **No delete.** The Load list has no delete (SV-23 is M12 work), and saves cannot be renamed.
- **Older format version message.** `SaveReader` refuses an unknown version with a generic "Unknown save format version" text,
  not the SV-33 wording. The SV-33 text is part of the M12 rule in decision 0038.
- **Not saved:** `CommandLog`, nav cell contents (only `Nav.Version`), speed (decision 0006), and the step runner's own runtime
  state (walk orders, ox tracks, rest targets). SV-22 is covered by `SaveLoadScreenMatchTests` for mid-haul and mid-build.
- **Options and Quit** are disabled by design (SV-03). Their behaviour is not built.
- **Menu rules in play:** SV-01..04 are covered by `GameMenuTests` in PlayMode. Their feel in real play has not been checked.
- **Tuning:** a save keeps its tuning (SV-14), so editing `SimConfig` does not change existing saves. There is no "load with
  current tuning" option.
- **Map changes:** a re-bake breaks old saves by hash (SV-30 and SV-32). That is accepted during development only; SV-31 is
  needed before the first player build.

## Follow-ups

- M12: delete with confirmation (SV-23), greyed rows for unreadable saves (SV-34), and the SV-33 version message, with the
  "bump the version per change" rule (decision 0038).
- Rename saves (SV-23 says "not yet").
- Optional dev-only "load with current tuning" for balancing old saves (SV-14).
- Before the first player build: versioned map files (SV-31) and a migration step per format version (SV-33).

## Tests

- Sim: `Sim.Tests/Save/` (`SimSaveRoundTripTests`, `SimSaveTests`, `SaveFormatTests`, and one `*SectionTests` per section).
- Unity EditMode: `SaveStoreTests`, `AutosaveTimerTests`.
- Unity PlayMode: `GameMenuTests`, `LoadPanelTests`, `AutosaveControllerTests`, `SaveLoadScreenMatchTests`.
