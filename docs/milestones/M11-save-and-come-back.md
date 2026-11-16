# M11: Save and come back

**Shipped (player-visible):** Esc opens the game menu (Resume, Save, Load, Options, Quit). The Sim pauses while it is
open and the pause state is restored on Resume. **Save** asks for a name, pre-filled with the game date. **Load** lists
saves newest first and reloads the game exactly as saved: people mid-walk, oxen mid-haul, half-built sites, clock and
pause. Speed resets to the default. A save from a different map is refused with a clear message. **Autosave** overwrites
one slot every 30 real minutes while a game runs and the menu is closed. Options and Quit are shown but disabled.

**Design source:** `docs/design/saving.md` (SV-01..34, GDD SAVE-01/02). Technical shape: decision 0034.

## Main technical changes by system

- **Sim save format (`Assets/Sim/Save/`):** `SimSave.Write` / `Read`, `SaveWriter` / `SaveReader`, magic `LLSV`, int32
  version (1 at M11), a header (name, real time, tick, map id and content hash), then sections as id + length.
  `SimWorld.ForRestore` is the restore path (no start layout, no systems stepped).
- **Sections (fixed order at M11):** Core (tick, pause, random, tuning, catalogs), Land (trees, roads, areas), Structures,
  Ledger, Population, Movement, Tasks. Read ends with a full `Nav.Sync`. Later milestones added Bushes, Homes and Food.
- **Map check (SV-30):** `MapData.ContentHash` (FNV-1a of the map bytes). A mismatch throws `SaveMapMismatchException`.
- **Tuning kept (SV-14):** the Core section stores the tuning and catalogs the world was created with.
- **Unity menu (`Assets/Game/UI/GameMenu/`):** `GameMenuView` and `GameMenuState` (Esc, pause, input blocking),
  `SavePanelController` (name, Confirm, "Saved" note), `LoadPanelController` (newest first, map check, sets `PendingLoad`).
- **Unity save files (`Assets/Game/Saving/`):** `SaveStore` (in `persistentDataPath/Saves/`, header-only list),
  `PendingLoad` (bytes handed to the next scene; `SimHost` takes them on start, so renderers start from a fresh world).
- **Autosave (SV-11):** `AutosaveTimer` (plain C#, real-time count, runs only while a world is loaded and the menu is
  closed) and `AutosaveController` (`Autosave.llsave`, 30 minutes, "Saved" note for 2 s). A manual save restarts the count.
- **Process rule:** new Sim state goes in a save section in the same task and must pass the round-trip test (CLAUDE.md).

## Decisions made

- 0034 save format: own binary format with sections, Sim-only restore through `ForRestore`, loads by scene reload.
- Owner decisions in `saving.md`: SV-01 (menu contents), SV-11 (30-minute autosave, one slot), SV-14 (save keeps its
  tuning), SV-20 (newest-first list).

## Known gaps and debt

- **Unreadable saves are hidden, not shown.** `SaveStore.List` skips files whose header cannot be read. SV-34 (greyed row) came in M12.
- **No delete or rename** at M11 (SV-23 came in M12).
- **Version message:** `SaveReader` refuses an unknown version with a generic text, not the SV-33 wording (decision 0038).
- **Not saved:** `CommandLog`, nav cell contents (only `Nav.Version`), speed (decision 0006), and step-runner runtime state
  (walk orders, ox tracks, rest targets). SV-22 is covered by `SaveLoadScreenMatchTests` for mid-haul and mid-build.
- **Options and Quit** are disabled by design (SV-03).
- **Untested end to end:** the Save panel's `Saved` event reaching `AutosaveController`. Only the controller method is tested.
  Menu rules SV-01..04 are covered by `GameMenuTests`, but their feel in play is not checked.
- **Tuning:** editing `SimConfig` does not change existing saves. There is no "load with current tuning" option.
- **Map changes:** a re-bake breaks old saves by hash (SV-30, SV-32). Accepted during development only.

## Follow-ups

- Before the first player build: versioned map files (SV-31) and a migration step per format version (SV-33).
- Optional dev-only "load with current tuning" for balancing old saves (SV-14).

## Tests

- Sim: `Sim.Tests/Save/` (`SimSaveRoundTripTests`, `SimSaveTests`, `SaveFormatTests`, one `*SectionTests` per section).
- Unity EditMode: `SaveStoreTests`, `AutosaveTimerTests`. PlayMode: `GameMenuTests`, `LoadPanelTests`,
  `AutosaveControllerTests`, `SaveLoadScreenMatchTests`.
