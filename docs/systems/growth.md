# Growth

**Purpose:** the village grows and shrinks. A family with 30 hungry days in a row walks out to a map exit. A family walks in when a house is free and food covers a month. Decision 0045; rules GR-* in `design/growth.md`.

## Key types and files

`Assets/Sim/Growth/`, `Assets/Sim/Arrivals/` (family plans), `Assets/Sim/Roads/MapExits.cs`.

| Type | Role |
|---|---|
| `DepartureSystem` | Routine phase, after eating. At midnight starts leaving for `HungryDays >= HungryDaysToLeave`. Walks Leaving members to `MapExits.Nearest`; removes each on arrival, and the family (and its empty larder) with the last one. |
| `FamilyDeparture.Begin` | Unassigns, fails open tasks, drops carried loads, releases allotments, evicts the house (larder food stays), sets `Passage = Leaving`, logs. |
| `ArrivalSystem` | Routine phase, after departure. Walks Arriving families to their house and moves them in on the first arrival (or makes them Homeless). At dawn, after cheap pre-checks, tries one new family. |
| `ArrivalRules` | Pure. `Check` returns Allowed with a house id, or a reason. Order: free reachable working house with no Homeless family waiting; food for `ArrivalFoodDays`; no hunger for `ArrivalNoHungerDays`; gap `ArrivalGapDays`. |
| `GrowthReport` | Read only. `Of` gives counts and `ArrivalReason` (a probe couple). `StatusOf` gives "Hungry (n / 30 days)", "Leaving (hungry)", "Arriving" or null. |
| `FamilyGenerator`, `FamilyPlan`, `ArrivalCatalog` | Pure. Make-up, surname, names and ages from seeded `SimRandom` and catalog data (defaults in code). |
| `MapExits` | Pure. Edge = terrain bounds within `MapEdgeToleranceM`. `Nearest` is a road node on the edge, else the nearest walkable edge point. `ComputeEntry` is the trunk road's end. Derived, never saved. |
| `FamilyPassage` | `None`, `Arriving`, `Leaving` on `Family.Passage`. Readers call `Family.IsInPassage`. |

## How data moves

- **Routine order** (pinned by `GrowthReportTests.DefaultSystems_RunEatingThenDepartureThenArrival`): `RoutineSystem`, `EatingSystem`, `DepartureSystem`, `ArrivalSystem`, `AllotmentSystem`, `FoodTripSystem`. Eating raises `HungryDays` when short, else resets it, and sets `LastHungryTick`.
- **Leaving:** midnight starts `FamilyDeparture.Begin`. Members walk to the exit; a failed walk retries hourly.
- **Arriving:** dawn runs `MayTry`, a generated plan, then `ArrivalRules.Check`. On success it reserves the house, spawns members at `EntryPoint` with `Passage = Arriving`, walks them to the house, and produces a month of food (meat, berries, mushrooms) into the larder.

## Readers that skip passage families

`WorkDay`, `RoutineSystem`, `DispatchSystem`, `AllotmentSystem`, `FoodTripSystem`, `StallSystem`, `StoreFamilySystem`, `MoveInSystem`, and `ArrivalRules` (Homeless families waiting) check `IsInPassage`. `AssignFamily` refuses both passage states. A new family reader must check it.

## What the player sees

- Settlement log: "The X family has arrived" and "The X family is leaving: hungry for N days". The message line under the top bar shows the last 4 and fades at 30 s.
- Citizen and house panels: `GrowthReport.StatusOf`. Top bar: "Citizens: N", tooltip with families, arriving, leaving and the next family's reason.

## Saved

Passage (Population), `ReservedForFamilyId` (Homes), `LastArrivalTick`, `EntryPoint` and the settlement log (Core), `LastHungryTick` and `HungryDays` (Food). `MapExits` and `GrowthReport` are derived. Save version: `SaveWriter.CurrentVersion` (27).

## Extending

- New refusal reason: a constant in `ArrivalRules`, a check in order, and a test. `Check` stays pure.
- Tuning: `SimTuning` fields (`HungryDaysToLeave`, `ArrivalFoodDays`, `ArrivalNoHungerDays`, `ArrivalGapDays`, `MapEdgeToleranceM`), set in the `SimConfig` "Growth" group.

## Gotchas

- Citizens vanish at the exit; the family goes with its last member. Views must cope with ids disappearing (`PeopleRenderer` drops phase state). Ids are never reused.
- Hunger never slows work (GR-01; `HungryWoodcutter_FellsAtTheSameRateAsAFedOne`).
- Arrival food is new supply put in the larder at spawn, so `Inventory.Audit()` passes.
- The entry point is saved, so later road edits do not move it. The generator extends the trunk road to the edge; the nearest-walkable-edge fallback is for maps without one.
- A house lost on the way makes the family Homeless at a rough-sleeping spot (`RoughSleep`).

## Tests

`Sim.Tests/Growth/`: ArrivalRules, ArrivalSystem, DepartureSystem, FamilyDeparture, GrowthReport (order, status, counts, `EndToEnd_FamilyMovesIn_ThenNoFood_EveryoneLeaves_PopulationZero`), PassageFoodHomeMarketReaders. Also `Arrivals/FamilyGeneratorTests`, `People/FamilyPassageTests`, `People/PassageWorkReadersTests`, `Core/SettlementLogTests`, `Start/MapExitsTests`, `Save/CoreSectionTests`. Unity PlayMode: `CitizenPanelTests`, `HousePanelTests`, `MessageLogTests`, `TopBarTests`, `PeopleRendererTests`, `GameSceneTests`.

## Gaps

- The end-to-end test builds its village by hand, not through `StartLayoutApplier`.
- Names and age ranges are code defaults, not start-layout data (GR-23). The top bar shows citizens only (GR-31).
- Not built: POP-A6 (every arrival is a new family, GR-25), homeless departure (GR-13b), deaths, births, player alerts.
