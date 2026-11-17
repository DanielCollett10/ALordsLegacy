# Workshops

**Purpose:** the hewing station (log to beam) and the sawpit (beam to planks): placement, stock and store, the shared system that fetches and works, and the read models panels use. Design: `design/planks.md` (HW-01..06, PL-01..05). Decisions 0046, 0047.

## Key types and files

`Assets/Sim/Operations/Workshops/` (`LordsLegacy.Sim.Operations.Workshops`), plus `Inventory/GoodsStats.cs`.

| Type | Role |
|---|---|
| `WorkshopDef` | Data: input, output, outputs per input, inputs per day per family, stock and store capacity, ox load. `Hewing(t)` and `Sawing(t)` read every number from `SimTuning`. |
| `WorkshopBuildings` | `IsWorkshop`; `OnBuilt` makes the `WorkshopStock` (input) and `GoodsStore` (output) when the site finishes. |
| `WorkshopSystem` | Operations phase, after construction. Opens `task.fetch_input` (ox, nearest free source) and one `task.workshop_work` per working family. |
| `InputSources` | Where an ox may fetch from: logs share `ConstructionSources` plus woodcutting piles; beams come from workshop goods stores. Never a `WorkshopStock`. |
| `WorkshopStatus` | Computed status: Paused, MissingHut, NoWorkers, Working, WorkshopStoreFull, WaitingForInput (Reported: WaitingForOx). |
| `WorkshopReport` | Read model (T-525): `Of(world, footprintId)` gives `Name`, `StatusText`, `InputCount/InputCapacity`, `OutputCount/OutputCapacity`, `InputText`, `OutputText`, `FamilyId`, `FamilyName`, `IsBuilt`. `ConfirmText(world, typeId)` is the placement dialog text. Null for a non-workshop id. |
| `GoodsStats` | Top bar totals (T-525): `Logs` (camp stockpile, built log storage, unowned piles, as `ConstructionSources` counts them), `Beams` and `Planks` (every `GoodsStore`). |

## Rules for the totals

- Reserved stock still counts (decision 0046): a log reserved for a site is still in the village.
- A log on an ox, in a `WorkshopStock`, on a site or in an operation's own pile does not count (decision 0047). Beams waiting in a sawpit's stock are not village beams.
- Status text: "Working", "Paused", "Missing hut", "No workers", "Waiting for logs/beams", "Waiting for ox", "Store full", "Under construction".

## Tests

`Sim.Tests/Workshops/`: `WorkshopReportTests` (report, statuses, confirm text, `GoodsStats` rows), `WorkshopWorkTests`, `WorkshopSystemTests`, `InputSourcesTests`, `WorkshopDefTests`. `Operations/WorkshopPlacementTests` covers placement.

`TimberChainEndToEndTests` is the pipeline test: start layout with logs, a woodcutter, hewing station, sawpit, market and three granaries with six store families, all by real commands and ticks. The 10 starting planks go into five stalls, sawn planks are carried to the sixth, and it is built. Audit runs every 5 ticks.

## Gaps and follow-ups

- No Unity panel or top bar readout yet; read `WorkshopReport` and `GoodsStats` there.
- A site that loses a carried load at sunset drops it on a pile the site owns, and may not reserve it again if no spare logs exist (seen in the end-to-end test; not fixed here).
- Stall builds need an odd-jobber family (one with no workplace); with every family assigned, build tasks stay open.
