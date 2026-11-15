# Save and load

**Purpose:** write the whole Sim to bytes and rebuild it exactly, so a load continues as if it never stopped (decision 0034,
`docs/design/saving.md`). The Sim side is in `Assets/Sim/Save/`; the Unity side is in `Assets/Game/Saving/` and `Assets/Game/UI/GameMenu/`.

## Key types and files

| Type | File | Role |
|---|---|---|
| `SimSave` | `Assets/Sim/Save/SimSave.cs` | `Write(world, name, createdRealTime, mapId, mapHash)`, `Read(bytes, map)`. |
| `SaveWriter` / `SaveReader` | `Save/` | Little-endian primitives, strings, counted arrays. `SaveReader.ReadHeaderOnly` for the Load list. |
| `SaveHeader` | `Save/SaveHeader.cs` | Name, real time (int64, from the caller), tick, map id, map hash. |
| `SaveSections` | `Save/SaveSections.cs` | `CreateDefault()` lists the sections in order. |
| `ISaveSection` | `Save/ISaveSection.cs` | One section: `Write` and `Read`. Sections live in `Save/Sections/`. |
| `SaveDiff` | `Save/SaveDiff.cs` | `FirstDifference(a, b)`: names the first differing section id (0 = header). Used by tests. |
| `SaveFormatException`, `SaveMapMismatchException` | `Save/` | Bad format; wrong map id or hash (text starts with the SV-30 rule). |
| `SavePanelController`, `LoadPanelController` | `Assets/Game/UI/GameMenu/` | Save with a name, list and load. Load checks the map, sets `PendingLoad`, reloads the Game scene. |
| `AutosaveController`, `AutosaveTimer`, `SaveStore`, `PendingLoad` | `Assets/Game/Saving/` | One autosave slot every 30 real minutes (`Autosave.llsave`), files in `persistentDataPath/Saves/`, and the bytes handed to the next scene. |

## Format

Magic `LLSV`, int32 version (`SaveWriter.CurrentVersion` = 1), the header, then sections, each as int32 id + int32 byte length.
A reader can skip unknown ids. Sections in order:

| Id | Section | Holds |
|---|---|---|
| 1 | Core | Tick, pause, random state, tuning, road and area catalogs. |
| 2 | Land | Trees as changes, road network, areas. |
| 3 | Structures | Footprints, camp, operations, construction sites. |
| 4 | Ledger | Locations, reservations, journals, id counters. Re-attaches the camp stockpile and runs `Audit`. |
| 5 | Population | Families, citizens, oxen. |
| 6 | Movement | Move orders, follow links, debug sends, routine state, work-day plans. |
| 7 | Tasks | Task records (by def id) and their steps. |

## How data moves

- **Write:** sections in the order above, each to its own writer.
- **Read:** header → refuse a map id or hash mismatch → Core → `SimWorld.ForRestore(map, core)` (no start layout, no systems
  stepped) → each list section in order (a wrong id throws) → full `Nav.Sync`.
- **Restore methods:** each registry has internal restore methods that set records and id counters directly.
- **Unity load:** `LoadPanelController` → `PendingLoad` → Game scene reload → `SimHost.Start` builds the world from the bytes.

## Rules for new state

Any task that adds Sim state adds it to a section in the same task. Extend the test `SimSaveRoundTripTests`:
`Write(Read(Write(w)))` equals `Write(w)` byte for byte, and both worlds stepped for a game day write identical bytes.
Not saved: `CommandLog`, nav cells (only `Nav.Version`), speed (decision 0006), and step-runner runtime state (walk orders,
ox tracks, rest targets).

## Extending

- A new section gets the next id and goes at the end unless it must load earlier. Record that in decision 0034.
- A format change bumps the version. Old versions are refused with a message; there are no migrations yet (SV-33).
- A new task def id must be in `TaskDefCatalog`, or old saves with it cannot load.

## Gotchas

- Tuning in the save wins over current `SimConfig` (SV-14). Changing tuning does not change an old save.
- Re-baking the map breaks old saves by hash (SV-30). That is accepted in development (SV-32).
- Autosave runs while the Esc menu is closed. Sim pause still counts.
- Speed is not saved, so a load starts at the default speed.

## Tests

`Sim.Tests/Save/` (`SimSaveRoundTripTests`, `SimSaveTests`, `SaveFormatTests`, `CoreSectionTests`, `LandSectionTests`,
`StructuresSectionTests`, `LedgerSectionTests`, `PopulationSectionTests`, `MovementSectionTests`, `TasksSectionTests`).
Unity EditMode: `Assets/Tests/EditMode/SaveStoreTests.cs`, `AutosaveTimerTests.cs`. PlayMode: `AutosaveControllerTests.cs`,
`LoadPanelTests.cs`, `SaveLoadScreenMatchTests.cs`, `GameMenuTests.cs`.
