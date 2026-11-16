# Markets

**Purpose:** the town market (a drawn area) with stall slots, granary families that stock stalls, the camp handover, and the
Sunday allotment and trip from stalls. Design: `design/markets.md` (MK-*, MKT-*). Decision 0038.

## Key types and files

`Assets/Sim/Markets/` (`LordsLegacy.Sim.Markets`). Related: `Start/CampRemovalSystem.cs`, `Food/StallMarket.cs`.

| Type | Role |
|---|---|
| `MarketRegistry`, `Market` | The one market per area: stall footprint ids, stall to family links, families with no room. Saved. |
| `StallGrid`, `StallSystem` | Slot grid inside the area; one stall (3 × 2 m, 1 log, 60 min, food store 30) per staffed family, nearest slot first. Unstaffed stalls keep their food and are reused. |
| `StoreFamilySystem` | Plans family trips for a granary operation: hut or camp store to granary, granary to stall (`task.stock_stall`). |
| `MarketRange`, `MarketRules` | Walking range (630 m) from the market centre to the house; switch-on rules. |
| `SetGranarySupplies` | Command: a built granary supplies the market. Needs a market. Switching on sets `FoodHandedOver`. |
| `MarketReport` | Read model for the panel. |
| `CampRemovalSystem` | Midnight: handed over and nobody Homeless, so the camp goes and leftovers become a ground pile. |

## Data flow (food moves one way)

1. Foragers pick into their own **hut store** (never anywhere else, decision 0044).
2. A granary family collects from the fullest hut store into the **granary** (10 items a load, 10 min each end).
   After the handover it also empties the **camp food store** into the granary, and the empty store is removed.
3. With a stall built and a granary stocked, the same family carries granary food to the **stall**.
4. Sunday dawn: `AllotmentSystem` reserves each household's week on stall stock (fullest first). The Sunday trip walks to
   each reserved stall, collects, and deposits in the **larder**. Eating at midnight sets `IsHungry` when short.
5. When every family is housed, `CampRemovalSystem` removes the camp at midnight.

## Rules

- Before `FoodHandedOver` households draw from the camp, after it only from stalls in range, never a mix (MK-31).
- So a switch-on late in the week gives a **short first Sunday**: no stall is built and stocked yet.
- Stall stock is the market's stock; granary stock never counts (MK-32).
- Deleting the market drops stall food as a ground pile. Removing a granary clears its flag.

## How to extend

- **More stall families or types:** extend `StallSystem` and `StallGrid`; keep one stall per family.
- **Range:** `SimTuning.MarketRangeM`.

## Gotchas

- Houses in the end-to-end test are built with `Construction.Remove` after releasing the site's log reservations. A leftover
  reservation on the camp stockpile blocks camp removal for ever.
- The camp store must fit in the granary (100 items) or the handover never empties it.

## Tests

- `Sim.Tests/Markets/`: `MarketEndToEndTests` (the whole loop: Case A switch on Monday, Case B on Saturday night, one-way food
  path checked each tick, save mid-run and 2 days later byte-identical), `GranarySupplyTests`, `StallSystemTests`,
  `ForagerCollectTests`, `FoodHandoverTests`, `MarketRangeTests`, `MarketReportTests`, `DeconstructStallTests`.
- Also `Food/AllotmentHandoverTests`, `Start/CampRemovalTests`.
