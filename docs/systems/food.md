# Food

**Purpose:** food is ledger stock like logs. Households keep it in larders, fetch a week each Sunday from a market (the
camp until the handover, then the town's stalls), eat daily, and show Hungry when short. Design: `design/food.md` (FD-*),
`design/markets.md` (MK-*). Markets: `markets.md`. Foraging supply: `foraging.md`.

## Key types and files

`Assets/Sim/Food/` (`LordsLegacy.Sim.Food`). Items: `Assets/Sim/Inventory/Items.cs`. Save: `Save/Sections/FoodSection.cs`.

| Type | Role |
|---|---|
| `Items.Meat`, `Berries`, `Mushrooms` | Food items: 3, 1 and 1 rations (`RationsPerItem`). |
| `Larders` | Food-only location per family and house, made on first need. `OnMoveIn` and `OnEvict` move food. |
| `HouseholdLarder` | Read only: counts, rations, days left. Never creates a location. |
| `IFoodMarket`, `CampFoodMarket` | Allotment source: stock locations, origin, range. `StallMarket` (in `Markets/`) is the stall version. |
| `AllotmentSystem` | Routine, Sunday dawn. Tops each household up to 7 days from the active market, nearest household first. |
| `FoodTripSystem` | Routine, Sunday morning. Opens `task.fetch_food` for each household with a reservation. |
| `EatingSystem` | Routine, midnight. Eats from the larder and sets `IsHungry`. `DailyNeed` is pure. |
| `FoodStats`, `FoodStock` | Village totals for the top bar. |
| `GranaryReport` | Read model for the granary panel: state, stock, supplies toggle, `ConfirmText`. |

Family fields: `LarderLocationId`, `CarriedRations`, `IsHungry`, `TripCarrierId`, `AllotmentReservationIds`. Ledger kind `FoodStore` refuses non-food.

## Data flow

- **Stock:** the camp food store (`CampInfo.FoodStoreLocation`) starts with 400 meat (`StartingMeat`). Start families get larders.
- **Sunday allotment:** reserves each household's deficit on the active market's stock (owner `-1000 - familyId`). Nothing moves yet.
- **Sunday trip:** `FoodTripSystem` opens `task.fetch_food` over the 240 minutes after dawn, spread by household rank.
  `DispatchSystem.RunFamilyTrips` gives it to the first awake, idle adult. Steps: a `WalkTo` and `CollectFood` for each
  reserved stop (camp or stall, nearest first), `WalkTo` home, `DepositFood`. A failed trip drops what it carries.
- **Eating:** at midnight. Whole items first; the rest stays in `CarriedRations`. Short = `IsHungry` that day. `HungryDays` counts hungry midnights in a row (0 when fed or no need); `world.LastHungryTick` = latest hungry midnight (null at start). Both saved in Food (T-496, GR-02/21).
- **Handover:** `SimWorld.FoodHandedOver` is set once a built granary supplies the market and is never cleared (`markets.md`).

## Rules

- Before the handover only the camp sells; after it only stalls in range. Never a mix (MK-31). A household out of range gets nothing.
- Granaries and forager stores never feed households directly. Store families carry their food to stalls (`markets.md`).
- Hungry changes no work (FD-16). A family in passage (Arriving or Leaving) gets no allotment and no food trip (M16, `growth.md`).
- `PlaceStructure(module.granary)` creates the 8 × 6 m footprint with a 5 m yard, a food store (100), the site and
  `operation.granary` (no area, 2 slots; decision 0041). Deconstruction drops its food. Granary family work is `StoreFamilySystem`.

## How to extend

- **New food item:** an `ItemDef` in `Items` with category Food. Add it to `GranaryReport.FoodItems` to show it on the granary panel.
- **New allotment source:** implement `IFoodMarket` and return it from `AllotmentSystem`'s source choice. Each stop becomes a trip step.
- **Balancing:** `SimTuning` `StartingMeat`, `AdultRations`, `ChildRations`, `MarketRangeM` (mirrored in `SimConfig`).

## Gotchas

- Allotment and trip owners are `-1000 - familyId`. Keep that range clear of footprint and task ids (`FamilyOf` decodes it).
- Stall trips (T-466): a stall whose footprint is gone is skipped and its reservations released. No save change.
- `DaysLeft` = rations ÷ need (children 0.5). The top bar shows "Food: –" when nobody eats.
- Ration totals include `CarriedRations`. Item counts do not.

## Tests

- Sim: `Sim.Tests/Food/` (AllotmentHandover, Allotment, Eating, FoodTrip, StallTrip, FoodStore, FoodStock, HouseholdLarder, GranaryReport), `Markets/FoodHandoverTests`, `Save/LedgerFoodTests`, `Placement/GranaryPlacementTests`, `Operations/GranaryOperationTests`.
- Unity PlayMode: `TopBarTests`, `CitizenPanelTests`, `HousePanelTests`, `GranaryPanelTests`, `StructureToolTests`.
- Gap: no `FoodSectionTests`. `MarketEndToEndTests` saves mid-run and compares bytes (decision 0034).
