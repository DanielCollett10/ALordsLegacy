# Markets

**Purpose:** the town's one market (a drawn area) with stall slots, granary families that stock stalls, the camp handover,
and the Sunday allotment and trip from stalls. Design: `design/markets.md` (MK-*, MKT-*), `design/camp.md` (CMP-20..25).

## Key types and files

`Assets/Sim/Markets/` (`LordsLegacy.Sim.Markets`). Also `Geometry/StallGrid.cs` (slots), `Food/IFoodMarket.cs`
(interface), `Start/CampRemovalSystem.cs`, `Commands/SetGranarySupplies.cs`, `Commands/DeconstructStall.cs`.

| Type | Role |
|---|---|
| `MarketRegistry`, `Market` | One `Market` per `area.market` (`SimWorld.Markets`). Stall footprint ids, stall-to-family links, families with no room (not saved). |
| `MarketRules` | `CanPlaceMarket`: "Only one market per town" once one exists (MK-04). |
| `StallGrid` | Pure. Slots from the market polygon: 4 × 5 m cells, nearest the centre first, 2 m clear of every edge (MK-05). Nothing saved. |
| `StallSystem` | Operations phase. One stall per staffed family on the nearest free slot; unstaffed stalls keep food and are reused. `RemoveStalls` on market delete. |
| `StoreFamilySystem` | Operations phase. Plans `task.stock_stall` (granary to stall) and collect trips (hut or camp store to granary). Sets `FoodHandedOver`. Removes the empty camp store. |
| `StallMarket` | `IFoodMarket` for the market: stall stock is its stock, origin is the area centre, range `MarketRangeM`. |
| `MarketRange`, `HouseRangeResult` | Walking range from the market centre to each house's access point. `AccessText` feeds the house panel. |
| `SetGranarySupplies` | Command: a built granary supplies the market. Needs a market. Switching on sets `FoodHandedOver` (once). |
| `DeconstructStall` | Command: only for an unstaffed stall (MK-23b). Marks it; its food drops as a pile. |
| `MarketReport` | Read model for the market panel: stalls, stock, supplying granaries, last Sunday served and short. |
| `CampRemovalSystem` | Midnight: handed over and nobody Homeless, so the camp goes and leftovers become a ground pile. |

## Data flow (food moves one way)

1. Foragers pick into their own **hut store** only (decision 0044).
2. A granary family collects from the fullest hut store into the **granary** (10 items a load, 10 min each end).
   After the handover it also empties the **camp food store** into the granary, and the empty store is removed.
3. With a stall built and a granary stocked, the same family carries granary food to the **stall**.
4. Sunday dawn: `AllotmentSystem` reserves each household's week on stall stock (fullest first). The Sunday trip collects at each reserved stall and deposits in the **larder**. Eating at midnight sets `IsHungry` when short.
5. When every family is housed, `CampRemovalSystem` removes the camp at midnight.

## Rules

- Before `FoodHandedOver` households draw from the camp; after it only from stalls in range, never a mix (MK-31).
- So a switch-on late in the week gives a **short first Sunday**: no stall is built and stocked yet.
- Stall stock is the market's stock; granary stock never counts (MK-32). Store families never take allotted food (MK-27).
- Stall: 3 × 2 m, 1 log, 60 min, holds 30 food, does not block movement. Store carry: 10 items, 10 min per load or unload.
- Deleting the market drops stall food as a ground pile. Removing a granary clears its supply flag.

## How to extend

- **More stall families or types:** extend `StallSystem` and `StallGrid`; keep one stall per family.
- **Range and sizes:** `SimTuning` `MarketRangeM`, `MarketMin/MaxAreaM2`, `StallCellWidthM/DepthM`, `HandCarryItems`.

## Gotchas

- `StallGrid` is in `Geometry/`, not here. Its 2 m edge padding is not in the design (implementation note on MK-05).
- Houses in the end-to-end test are built with `Construction.Remove` after releasing the site's log reservations. A leftover
  reservation on the camp stockpile blocks camp removal for ever.
- The camp store must fit in the granary (100 items) or the handover never empties it.

## Tests

- Sim `Sim.Tests/Markets/`: `MarketEndToEndTests` (one-way food path checked each tick; save mid-run byte-identical),
  `GranarySupplyTests`, `StallSystemTests`, `ForagerCollectTests`, `FoodHandoverTests`, `MarketRangeTests`,
  `MarketReportTests`, `DeconstructStallTests`, `StoreFamilySystemTests`.
- Also `Areas/MarketAreaTests`, `Food/AllotmentHandoverTests`, `Geometry/PolygonCentroidTests`, `Start/CampRemovalTests`.
- Unity PlayMode: `MarketPanelTests`, `MarketToolTests`, `MarketVisualsTests`. Gap: range tint only when selected (MK-41).
