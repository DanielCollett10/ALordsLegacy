# Kit

**Purpose:** a forager hut or granary keeps baskets in its own kit store. A carrier who borrows one carries twice as much.
Design: `design/materials.md` MAT-50..54, ST-20..25. Decision 0052 (kit is reserved stock).

## Key types and files

`Assets/Sim/Kit/` (`LordsLegacy.Sim.Kit`).

| Type | Role |
|---|---|
| `KitStores` | Kit store lookup. `OnBuilt` makes a goods store keyed by the building, with a per-item cap from `ForagerHutBasketSlots` (5) or `GranaryBasketSlots` (2). `BasketsHeld`. |
| `KitClaims` | Queue of buildings with free slots, oldest first, ties to the lower id (ST-24). Saved in the Structures section. |
| `KitFetchSystem` | Operations phase. Serves claims oldest first with a hand haul by the building's own assigned family, from the nearest unreserved basket: camp General store, working yard or weaver store. Reserves source and slots. |
| `KitLoans` | `TryBorrow` reserves one kit basket for a task (owner `-3_000_000 - taskId`). `Factor` = 2. `ReleaseFinished` frees it when the task ends. |

## Data flow

1. Built forager hut or granary: kit store and a claim. A full store closes the claim.
2. The weaver makes baskets into its store. The yard collects them (ST-42) and a kit fetch trip carries them to the hut.
3. A forage or granary trip plans with a free kit basket: load 2 x `HandCarryItems` (forage also capped at 20, bush and hut room). The basket stays in the ledger.
4. Removing the building drops its kit (T-564).

## Rules

- The building's own family fetches, so an unassigned hut never fills its kit.
- A kit store is never a construction, workshop or yard source. Village totals count it.

## Tests

`Sim.Tests/Kit/` (`KitStoreTests`, `KitFetchTests`, `KitLoanTests`), `Workshops/KitReadModelTests`, `Scenarios/BasketChainScenarioTests` (start layout to a 20 carry, audit every tick, mid-run save byte-identical).
