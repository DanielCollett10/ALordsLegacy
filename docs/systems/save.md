# Save and load

**Purpose:** write the whole Sim to bytes and rebuild it exactly (decision 0034, `docs/design/saving.md`). Sim: `Assets/Sim/Save/`. Unity: `Assets/Game/Saving/`, `Assets/Game/UI/GameMenu/`.

## Key types and files

| Type | File | Role |
|---|---|---|
| `SimSave`, `SaveWriter` / `SaveReader` | `Save/` | `SimSave.Write(world, name, time, mapId, mapHash)` / `Read(bytes, map)`. Writer and reader hold primitives; `ReadHeaderOnly` feeds the Load list. |
| `SaveHeader`, `SaveSections` | `Save/` | Header: name, real time (int64), tick, map id and hash. `SaveSections.CreateDefault()` orders the sections. |
| `ISaveSection` | `Save/`, `Save/Sections/` | One section: `Write` and `Read`. |
| `SaveDiff`, exceptions | `Save/` | `SaveDiff.FirstDifference` names the first differing section (tests). `SaveMapMismatchException` carries the SV-30 text. |
| `SavePanelController`, `LoadPanelController` | `Assets/Game/UI/GameMenu/` | Save and Load lists (unreadable saves greyed), map check, `PendingLoad`, reload. |
| `AutosaveController`, `AutosaveTimer`, `SaveStore`, `PendingLoad` | `Assets/Game/Saving/` | Autosave slot every 30 real minutes (`Autosave.llsave`). `SaveStore` lists, writes and `Delete`s files in `persistentDataPath/Saves/`. |

## Format

Magic `LLSV`, int32 version (`SaveWriter.CurrentVersion`, now 11; every change bumps it, decision 0038), the header, then
sections, each as int32 id + length. `SimSave.Read` requires the exact list order. Homes (id 8) and Food (id 9) sit between Population and Movement (decision 0034).

| Order | Id | Section | Holds |
|---|---|---|---|
| 1 | 1 | Core | Tick, pause, random state, tuning (incl. deconstruct shares and forager tuning), road and area catalogs. |
| 2 | 2 | Land | Trees as changes, road network, areas. |
| 3 | 10 | Bushes | Forage bushes: position, kind, baskets, emptied tick, reservation, id counter. Replaces the new-game scatter on load (T-421). |
| 4 | 3 | Structures | Footprints, camp, operations, construction sites, deconstruction entries, pending operations. |
| 5 | 4 | Ledger | Locations, reservations, journals, id counters. Re-attaches the camp stockpile, runs `Audit`. |
| 6 | 5 | Population | Families, citizens, oxen. |
| 7 | 8 | Homes | Per house: footprint id, plot area id, family id (0 = Empty). Sets each family's `HomeHouseId`. |
| 8 | 9 | Food | Camp food store id. Per family: larder, carried rations, Hungry, trip carrier, allotment reservations. Per house: larder. |
| 9 | 6 | Movement | Move orders, follow links, debug sends, routine state, work-day plans. |
| 10 | 7 | Tasks | Task records (by def id) and their steps, with bush and carrier ids (T-423). |

Version history: 9 added Food (T-415..418), 10 added Bushes (T-421), 11 added forager tuning and task bush and carrier ids (T-423).

## How data moves

**Write:** sections in order. **Read:** header → refuse map mismatch → Core → `SimWorld.ForRestore` (no start layout, no
systems stepped) → each list section → full `Nav.Sync`. Unity: `LoadPanelController` → `PendingLoad` → scene reload → `SimHost.Start`.

## Rules for new state

Any task that adds Sim state adds it to a section in the same task, and extends `SimSaveRoundTripTests`:
`Write(Read(Write(w)))` equals `Write(w)` byte for byte. Not saved: `CommandLog`, nav cells (only `Nav.Version`), speed (decision 0006), step-runner runtime state.

## Extending

- A new section gets the next id and goes at the end unless it must load earlier (record in decision 0034). A new task def id must be in `TaskDefCatalog`.
- A format change bumps the version. Old versions are refused with a message; no migrations yet (SV-33). The Load list greys them.

## Gotchas

- Tuning in the save wins over current `SimConfig` (SV-14). Re-baking the map breaks old saves by hash (SV-30, SV-32).
- Autosave runs while the Esc menu is closed. Speed is not saved. A save from another map is refused at Load, not greyed (SV-30).
- **Gap:** `SimSaveRoundTripTests` has no food, bush or granary case (M14). There is no `FoodSectionTests`.

## Tests

`Sim.Tests/Save/` (`SimSaveRoundTripTests`, `SimSaveTests`, `SaveFormatTests`, `*SectionTests`, `LedgerFoodTests`).
Unity: `SaveStoreTests`, `AutosaveTimerTests` (EditMode); `AutosaveControllerTests`, `LoadPanelTests`, `GameMenuTests` (PlayMode).
