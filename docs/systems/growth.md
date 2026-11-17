# Growth (Assets/Sim/Growth)

Families leave in a famine and arrive in plenty (decision 0045, GR-xx rules).

## Systems (routine phase, in this order)
`EatingSystem` -> `DepartureSystem` -> `ArrivalSystem`, then allotment and food trips. Order is pinned by
`GrowthReportTests.DefaultSystems_RunEatingThenDepartureThenArrival`.

- **EatingSystem** counts `Family.HungryDays` and sets `World.LastHungryTick`.
- **DepartureSystem**: at midnight a family with `HungryDays >= HungryDaysToLeave` (30) starts leaving
  (`FamilyDeparture.Begin`: fails tasks, frees the house, `Passage = Leaving`). Each member walks to the nearest map
  exit (`MapExits.Nearest`), is removed on arrival; the family and its empty camp larder go with the last one.
- **ArrivalSystem**: at dawn, after cheap pre-checks, draws a family (`FamilyGenerator`), asks `ArrivalRules.Check`,
  reserves a house, spawns the members at `EntryPoint` with a month of food and walks them to the house.
  `MoveInSystem` finishes the move-in.
- **ArrivalRules** (pure) checks in order: free reachable working house, food for `ArrivalFoodDays`, no hunger in
  `ArrivalNoHungerDays`, gap since last arrival. Reasons are the string constants on the class.

## Read model: GrowthReport
Read only; never creates locations or draws from `world.Random`.
- `GrowthReport.StatusOf(world, familyId)`: `Hungry (n / 30 days)` when n >= 1, `Leaving (hungry)`, `Arriving`, else null.
- `GrowthReport.Of(world)`: `Families`, `Citizens`, `Arriving`, `Leaving`, `ArrivalReason`.
  The reason is judged for a typical couple (fixed probe plan, no random) and is null when a family would be let in.

## Rules worth knowing
- Hunger never slows work (GR-01): tested by `HungryWoodcutter_FellsAtTheSameRateAsAFedOne`.
- No state of its own is saved: passage, reservations, move orders and larders are in their own sections.
- Ids of left families and citizens are never reused.

## Tests
`Sim.Tests/Growth/`: ArrivalRules, ArrivalSystem, DepartureSystem, FamilyDeparture, GrowthReport (order, status text,
counts, end-to-end arrive then starve to population 0 with `Inventory.Audit()` clean).

## Gaps
- The end-to-end test builds its village by hand (road, plots, one family), not through `StartLayoutApplier`.
- No UI panel reads `GrowthReport` yet.
