# Workshops

**Purpose:** the hewing station (log to beam), the sawpit (beam to planks), the weaver (willow rods to baskets) and the wattle
yard (hazel and willow rods to panels): placement, stock and store, the shared system that fetches and works, and the read
models panels use. Design: `design/planks.md` (HW, PL), `design/materials.md` (MAT-20..24). Decisions 0046, 0047, 0051, 0055.

## Key types and files

`Assets/Sim/Operations/Workshops/` (`LordsLegacy.Sim.Operations.Workshops`), plus `Inventory/GoodsStats.cs`.

| Type | Role |
|---|---|
| `WorkshopDef` | Data: a recipe (inputs with count and carry class, outputs), batches per family-day, stock and store capacity, min work age. `Hewing(t)`, `Sawing(t)`, `Weaving(t)`, `Wattling(t)`. Hewing and sawing read `SimTuning`; weaving and wattling hard-code theirs (see gaps). |
| `WorkshopBuildings` | `IsWorkshop`; `OnBuilt` makes one `WorkshopStock` per recipe input and the output `GoodsStore`. |
| `WorkshopSystem` | Operations phase, after construction. Opens `task.fetch_input` (ox) and `task.haul_by_hand` (hand) fetch trips, and one `task.workshop_work` per working family. |
| `InputSources` | Where each input may come from. Logs: `ConstructionSources` plus woodcutting piles. Beams: workshop goods stores. Rods: coppice hut and working yard stores. Baskets and panels: working yards only (T-562). Never a `WorkshopStock`. |
| `InputReserve` | Village reserve per input (PL-31): a fetch opens only while storage total minus open fetches minus the load is at least the reserve. |
| `WorkshopStatus` | Computed status: Paused, MissingHut, NoWorkers, Working, WorkshopStoreFull, WaitingForInput (Reported: WaitingForOx). |
| `WorkshopReport` | Read model: `Name`, `StatusText`, `Inputs` (one line per input: stock, cap, reserve, `InStorage`), output counts, `RecipeText`, family. `ConfirmText` for placement. Null for a non-workshop id. |
| `GoodsStats` | Totals: `Logs`, `Beams`, `Planks` (top bar); `Rods`, `Baskets`, `Panels` and `GeneralByItem` count every goods store, kit and coppice stores too. The top bar lists General goods in the Beams and Planks tooltips. |

## How a day runs

- Fetch: a free ox takes one input load (`OxLoadForInput`). A hand fetch carries up to the item's hand load (`HandLoads`).
  The fetch task reserves both ends, so no load is double-booked.
- Choice: each round takes the input with the lowest share of its stock (held plus incoming; ties in recipe order), skipping an
  input with no room, no source or a blocked reserve. A def with no inputs never fetches.
- Work: one `task.workshop_work` per working family. A batch starts only when every input has one batch in stock and the output
  store has room for one batch. Inputs are consumed and outputs produced on finish. Progress is kept overnight.
- Gate: no fetch or work while unbuilt, paused or without a working-age member.

## Hand inputs (T-560, decision 0051)

A `CarryClass.Hand` input is fetched by the workshop's own family through the shared `task.haul_by_hand` (`HandHaulTaskDefs`),
one open trip per working family, up to the item's hand load. The reserve counts a trip's items as already taken. Rods come from
the coppice hut or yard store nearest first (`RodFinder`). Decision 0051 named a separate task id; the code shares the haul task.

## Rules for the totals

- Reserved stock still counts (decision 0046). A log on an ox, in a `WorkshopStock`, on a site or in an operation pile does not
  count (decision 0047).
- Status text: "Working", "Paused", "Missing hut", "No workers", "Waiting for logs/beams", "Waiting: N / R logs in storage"
  (reserve), "Waiting for ox", "Store full", "Under construction".

## Gaps and follow-ups

- Weaver, wattle and daub numbers come from `SimTuning` (`Weaver*`, `Wattle*`, `DaubPerDay`, `DaubPitStore`, `HandCarryDaub`); T-583 wired them.
- Unity: `WorkshopPanelView` (via `AgentPicker.SelectWorkshop`) shows the report, Assign/Unassign/Deconstruct and the reserve field
  (focus only on click, T-602). The granary panel shows "Waiting for planks".
- **Daub pit** (T-583): `WorkshopDef.Daubing`, no input, 10 a day, store 20, hand load 2 (all tuning). Never fetches; status is No workers or Store full only. Sites take daub from its store; yards ignore it (ST-42b). `DaubPitTests`. Build-bar entry is hl-9.
- A site that loses a carried load at sunset drops it on a pile it owns (open from M18). Stall builds need an odd-jobber family; with every family assigned, build tasks stay open.

## Tests

`Sim.Tests/Workshops/`: `WorkshopRecipeTests`, `MultiInputWorkshopTests`, `WorkshopFetchChoiceTests`, `WorkshopHandFetchTests`,
`WeaverWattleYardTests`, `WorkshopEquivalenceTests`, `InputReserveTests`, `WorkshopReserveSettingTests`, `KitReadModelTests`,
`InputSourcesTests`, `WorkshopReportTests`. `Operations/WorkshopPlacementTests`. Pipeline: `TimberChainEndToEndTests`.
Unity PlayMode: `WorkshopPanelTests`, `GranaryPanelTests`, `TopBarTests`.
