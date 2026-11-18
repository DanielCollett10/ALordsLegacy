# Workshops

**Purpose:** the hewing station (log to beam) and the sawpit (beam to planks): placement, stock and store, the shared system that fetches and works, and the read models panels use. Design: `design/planks.md` (HW-01..06, PL-01..05). Decisions 0046, 0047.

## Key types and files

`Assets/Sim/Operations/Workshops/` (`LordsLegacy.Sim.Operations.Workshops`), plus `Inventory/GoodsStats.cs`.

| Type | Role |
|---|---|
| `WorkshopDef` | Data: input, output, outputs per input, inputs per day per family, stock and store capacity, ox load. `Hewing(t)` and `Sawing(t)` read every number from `SimTuning`. |
| `WorkshopBuildings` | `IsWorkshop`; `OnBuilt` makes one `WorkshopStock` per recipe input (own capacity, `RecipeInput.StockCapacity`) and the `GoodsStore` (output) when the site finishes. |
| Multi-input (T-558) | A work task starts only when every input has one batch available and the store has room for one batch; all reserved at start, all consumed and outputs produced on finish. Fetch runs the single-input path per input in recipe order (choice: cw-5c). Save v35. Tests inject a def via `OperationTypes.TestDefs`. |
| `WorkshopSystem` | Operations phase, after construction. Opens `task.fetch_input` (ox, nearest free source) and one `task.workshop_work` per working family. |
| `InputSources` | Where an ox may fetch from: logs share `ConstructionSources` plus woodcutting piles; beams come from workshop goods stores. Never a `WorkshopStock`. |
| `WorkshopStatus` | Computed status: Paused, MissingHut, NoWorkers, Working, WorkshopStoreFull, WaitingForInput (Reported: WaitingForOx). |
| `WorkshopReport` | Read model (T-525): `Of(world, footprintId)` gives `Name`, `StatusText`, `InputCount/InputCapacity`, `OutputCount/OutputCapacity`, `InputText`, `OutputText`, `FamilyId`, `FamilyName`, `IsBuilt`. `ConfirmText(world, typeId)` is the placement dialog text. Null for a non-workshop id. |
| `GoodsStats` | Top bar totals (T-525): `Logs` (camp stockpile, built log storage, unowned piles, as `ConstructionSources` counts them), `Beams` and `Planks` (every `GoodsStore`). |

## How a day runs

- Fetch: a free ox takes one input load from the nearest source (`InputSources`) into the workshop's `WorkshopStock`
  (`OxLoadForInput`). Both ends are reserved by the fetch task, so no load is double-booked.
- Work: one `task.workshop_work` per working family, one input at a time. Minutes per input = `ShiftMinutes` ÷
  `InputsPerDayPerFamily`, so a family works through 3 inputs a working day (3 logs hewn, or 3 beams sawn). Done = one input
  consumed, outputs produced into the `GoodsStore`. Progress is kept overnight.
- Gate: no fetch or work while unbuilt, paused or without a working-age member. A fetch needs room in the input stock; a work task needs room in the store.

## Rules for the totals

- Reserved stock still counts (decision 0046): a log reserved for a site is still in the village.
- A log on an ox, in a `WorkshopStock`, on a site or in an operation's own pile does not count (decision 0047). Beams waiting in a sawpit's stock are not village beams.
- Status text: "Working", "Paused", "Missing hut", "No workers", "Waiting for logs/beams", "Waiting: N / R logs in storage" (reserve), "Waiting for ox", "Store full", "Under construction".

## Tests

`Sim.Tests/Workshops/`: `WorkshopReportTests` (report, statuses, confirm text, `GoodsStats` rows), `WorkshopWorkTests`, `WorkshopSystemTests`, `InputSourcesTests`, `WorkshopDefTests`, `InputReserveTests`, `WorkshopReserveSettingTests`. `Operations/WorkshopPlacementTests` covers placement.

`TimberChainEndToEndTests` is the pipeline test: start layout with logs, a woodcutter, hewing station, sawpit, market and three granaries with six store families, all by real commands and ticks. The 10 starting planks go into five stalls, sawn planks are carried to the sixth, and it is built. Audit runs every 5 ticks.

## Fetch choice and zero inputs (T-559)

Each fetch round picks the ox input with the lowest share of its stock (held plus incoming, ties in recipe order), skipping inputs with no room, no source, or blocked by their own reserve. A def with no inputs never fetches and opens work whenever a family is assigned and the store has room for a batch; it shows only Working, No workers or Store full. "Waiting for X" names the first missing input; the reserve status names that item too. Hand inputs are not fetched yet.

## Input reserve (T-537, PL-30..34)

`InputReserve`: a fetch opens only if storage total (logs in Storage/Stockpile, beams in workshop GoodsStores; never piles, workshop stock, carried) minus stock reserved by open fetches minus the load is at least the reserve (tuning `WorkshopDefaultReserve` = 5, 0 = none). Own input stock is committed and uncounted; fetched work is always processed. Stocks: hewing store 10, input stocks 10.
The player sets it per workshop and input (`SetWorkshopReserve`, 0..9999): stored on the operation (`Operation.Reserves`, Structures section, save v31); an unset input uses the tuning default. Yard and coppice-hut stores also count (see `storage.md`). Decision 0055.

## Gaps and follow-ups

- Unity side (T-527, T-550): `WorkshopPanelView` (picked via `AgentPicker.SelectWorkshop`) shows `WorkshopReport` with Assign/Unassign/Deconstruct and the "Keep in storage" reserve field (text input, focus only on click, T-602); the granary panel shows "Waiting for planks"; the top bar has Logs/Beams/Planks labels from `GoodsStats`. PlayMode tests: `WorkshopPanelTests`, `GranaryPanelTests`, `TopBarTests`.
- A site that loses a carried load at sunset drops it on a pile the site owns, and may not reserve it again if no spare logs exist (seen in the end-to-end test; not fixed here).
- Stall builds need an odd-jobber family to build (stall planks are carried by the granary family); with every family assigned, build tasks stay open.
