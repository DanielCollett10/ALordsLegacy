# Food

**Purpose:** food is ledger stock like logs. Households keep it in larders, fetch a week from the camp each Sunday, eat daily,
and show Hungry when short. Design: `design/food.md` (FD-*). Foraging supply: `foraging.md`.

## Key types and files

`Assets/Sim/Food/` (`LordsLegacy.Sim.Food`). Items: `Assets/Sim/Inventory/Items.cs`. Save: `Save/Sections/FoodSection.cs`.

| Type | Role |
|---|---|
| `Items.Meat`, `Berries`, `Mushrooms` | Food items: 3, 1 and 1 rations (`RationsPerItem`). |
| `Larders` | Food-only location per family and house, made on first need. `OnMoveIn` and `OnEvict` move food. |
| `HouseholdLarder` | Read only: counts, rations, days left. Never creates a location. |
| `IFoodMarket`, `CampFoodMarket` | Where the allotment is reserved: stock, origin, range (630 m). `Markets/MarketRange` gives houses in walking range of a centre, same measure. |
| `AllotmentSystem` | Routine, Sunday dawn. Tops each household up to 7 days, nearest first. |
| `FoodTripSystem` | Routine, Sunday morning. Opens `task.fetch_food` for each household with a reservation. |
| `EatingSystem` | Routine, midnight. Eats from the larder and sets `IsHungry`. `DailyNeed` is pure. |
| `FoodStats`, `FoodStock` | Village totals for the top bar. |
| `GranaryReport` | Read model for the granary panel, with `ConfirmText`. |

Family fields: `LarderLocationId`, `CarriedRations`, `IsHungry`, `TripCarrierId`, `AllotmentReservationIds`.
Ledger kind `LocationKind.FoodStore` refuses non-food.

## Data flow

- **Stock:** the camp food store (`CampInfo.FoodStoreLocation`) starts with 400 meat (`StartingMeat`). Start families get larders.
- **Sunday allotment:** reserves each household's deficit on market stock (owner `-1000 - familyId`). Nothing moves yet.
- **Sunday trip:** `FoodTripSystem` opens `task.fetch_food` over the 240 minutes after dawn, spread by household rank.
  `DispatchSystem.RunFamilyTrips` gives it to the first awake, idle adult. Steps: `WalkTo` camp, `CollectFood` (ledger
  transfer that consumes the reservations), `WalkTo` home, `DepositFood`. A failed trip drops what it carries.
- **Eating:** at midnight. Whole items first; the rest stays in `CarriedRations`. Short = `IsHungry` that day (only the UI reads it).

## Rules and granary

- Only the camp market sells in M14. Granaries and forager stores never feed the camp (FD-12). A household outside the range gets nothing.
- Hungry changes no work (FD-16).
- `PlaceStructure(module.granary)` creates the standalone 8 × 6 m footprint (5 m yard, 8 logs, 600 min), a food store (100),
  the site and `operation.granary` (no area, 2 slots; decision 0041). Deconstruction drops its food. No granary work yet (MKT-03b).

## How to extend

- **New food item:** an `ItemDef` in `Items` with category Food. Add it to `GranaryReport.FoodItems` to show it on the granary panel.
- **New market (M15):** implement `IFoodMarket`, return it from `AllotmentSystem`'s source, and make `OpenTrip` walk to its origin.
- **Balancing:** `SimTuning` `StartingMeat`, `AdultRations`, `ChildRations`, `MarketRangeM` (mirrored in `SimConfig`).

## Gotchas

- `FoodTripSystem.OpenTrip` always walks to the camp, not to `IFoodMarket.Origin`.
- Allotment and trip owners are `-1000 - familyId`. Keep that range clear of footprint and task ids (`FamilyOf` decodes it).
- `DaysLeft` = rations ÷ need (children 0.5). The top bar shows "Food: –" when nobody eats.
- Ration totals include `CarriedRations`. Item counts do not.

## Tests

- Sim: `Sim.Tests/Food/` (Allotment, Eating, FoodTrip, FoodStore, FoodStock, HouseholdLarder, GranaryReport),
  `Save/LedgerFoodTests`, `Placement/GranaryPlacementTests`, `Operations/GranaryOperationTests`, `Markets/MarketRangeTests`.
- Unity PlayMode: `TopBarTests`, `CitizenPanelTests`, `HousePanelTests`, `GranaryPanelTests`, `StructureToolTests`.
- Gaps: no `FoodSectionTests`. The save round trip has no food case (decision 0034).
