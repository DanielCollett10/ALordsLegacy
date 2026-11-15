# Save and load

**Purpose:** write the whole Sim to bytes and rebuild it exactly (decision 0034, `docs/design/saving.md`). Sim: `Assets/Sim/Save/`. Unity: `Assets/Game/Saving/`, `Assets/Game/UI/GameMenu/`.

## Key types and files

| Type | File | Role |
|---|---|---|
| `SimSave`, `SaveWriter` / `SaveReader` | `Save/` | `SimSave.Write(world, name, time, mapId, mapHash)` / `Read(bytes, map)`. Writer and reader hold primitives; `ReadHeaderOnly` feeds the Load list. |
| `SaveHeader`, `SaveSections` | `Save/` | Header: name, real time (int64), tick, map id and hash. `SaveSections.CreateDefault()` orders the sections. |
| `ISaveSection` | `Save/`, `Save/Sections/` | One section: `Write` and `Read`. |
| `SaveDiff`, exceptions | `Save/` | `SaveDiff.FirstDifference` names the first differing section (tests). `SaveMapMismatchException` carries the SV-30 text. |
| `SavePanelController`, `LoadPanelController` | `Assets/Game/UI/GameMenu/` | Load lists saves (greyed when unreadable; bin deletes via `ConfirmDialog`), checks the map, sets `PendingLoad`, reloads Game. |
| `AutosaveController`, `AutosaveTimer`, `SaveStore`, `PendingLoad` | `Assets/Game/Saving/` | Autosave slot every 30 real minutes (`Autosave.llsave`). `SaveStore` lists, writes and `Delete`s files in `persistentDataPath/Saves/`. |

## Format

Magic `LLSV`, int32 version (`SaveWriter.CurrentVersion`, now 5; every change bumps it, decision 0038), the header, then
sections, each as int32 id + int32 length. `SimSave.Read` requires the exact list order and throws on any other id. Homes is id 8 but written after Population (0034).

| Order | Id | Section | Holds |
|---|---|---|---|
| 1 | 1 | Core | Tick, pause, random state, tuning (incl. deconstruct shares), road and area catalogs. |
| 2 | 2 | Land | Trees as changes, road network, areas. |
| 2b | 10 | Bushes | Forage bushes (T-421, version 10): position, kind, baskets, emptied tick, reservation, id counter. Replaces the new-game scatter on load. Version 11 (T-423): forager tuning in Core, `BushId`/`CarrierId` per task. |
| 3 | 3 | Structures | Footprints, camp, operations, construction sites, deconstruction entries, pending operations. |
| 4 | 4 | Ledger | Locations, reservations, journals, id counters. Re-attaches the camp stockpile, runs `Audit`. |
| 5 | 5 | Population | Families, citizens, oxen. |
| 6 | 8 | Homes | Per house: footprint id, plot area id, family id (0 = Empty). Sets each family's `HomeHouseId`. |
| 7 | 6 | Movement | Move orders, follow links, debug sends, routine state, work-day plans. |
| 8 | 7 | Tasks | Task records (by def id) and their steps. |

## How data moves

- **Write:** sections in order. **Read:** header → refuse map mismatch → Core → `SimWorld.ForRestore(map, core)` (no start
  layout, no systems stepped) → each list section → full `Nav.Sync`. Registries restore records and id counters directly.
- **Unity load:** `LoadPanelController` → `PendingLoad` → Game scene reload → `SimHost.Start` builds the world from bytes.

## Rules for new state

Any task that adds Sim state adds it to a section in the same task, and extends `SimSaveRoundTripTests`:
`Write(Read(Write(w)))` equals `Write(w)` byte for byte. Not saved: `CommandLog`, nav cells (only `Nav.Version`), speed (decision 0006), step-runner runtime state.

## Extending

- A new section gets the next id and goes at the end unless it must load earlier. Record that in decision 0034.
- A format change bumps the version. Old versions are refused with a message; no migrations yet (SV-33). The Load list
  greys a refused save (`SaveEntry.UnreadableReason`, from `SaveStore.ReasonForVersion`); its bin still deletes it.
- A new task def id must be in `TaskDefCatalog`, or old saves with it fail to load.

## Gotchas

- Tuning in the save wins over current `SimConfig` (SV-14).
- Re-baking the map breaks old saves by hash (SV-30). Accepted in development (SV-32).
- Autosave runs while the Esc menu is closed. Speed is not saved. A save from another map is not greyed; it is refused at Load (SV-30).
- `SaveReader` says "Unknown save format version N"; the list uses the friendlier SV-34 text.

## Tests

`Sim.Tests/Save/` (`SimSaveRoundTripTests`, `SimSaveTests`, `SaveFormatTests`, one `*SectionTests` per section).Unity: `SaveStoreTests`, `AutosaveTimerTests` (EditMode); `AutosaveControllerTests`, `LoadPanelTests`, `GameMenuTests` (PlayMode).
