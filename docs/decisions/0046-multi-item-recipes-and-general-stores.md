# 0046: Multi-item recipes, general stores and hand delivery

**Context.** M17 adds planks (`design/planks.md`); M18–M20 add thatch, panels, rods and baskets. Today
`ConstructionRecipe` already keys items by id, but every placement path (`PlaceModule`, `PlaceStructure`,
`DesignatePlot`, `StallSystem.BuildStall`, `ConstructionSystem` top-up) reserves only `Items.Log`, deliveries are ox-only,
and `ConstructionSite.LogsConsumed` is log-specific. The ledger has log `Storage` and food-only `FoodStore`, but nothing
for General items (INV-A1).

**Decision.**
- **General store:** a new `LocationKind.GoodsStore` that holds General items only (mirrors `FoodStore`, refuses logs and
  food). Used by the camp's General goods store, the sawpit's plank store, later the storage yard and hut stores.
- **One reservation path:** `SiteMaterials.TryReserveAll(world, recipe, point, owner)` reserves **every** recipe item,
  nearest source first, all or nothing. All placement paths call it. Refusal text names the first short item
  ("Not enough planks (have 4, need 6)"). `BuildCost.CanAfford` takes a recipe.
- **Delivery by carry class:** logs keep `task.deliver_materials` by ox. Other items use a new `task.carry_materials`
  done by hand, `HandCarry` items per trip from tuning. Who carries depends on the site: odd-jobbers by default (same
  eligibility as deliveries); a market stall's items are carried by its granary family (`MarketRegistry.FamilyOf`).
- **Progress over all items:** build cap = recipe minutes × delivered units ÷ needed units over all items.
  `LogsConsumed` becomes consumed counts per item (saved). Deconstruction refunds each item by `DeconstructRefundShare`.

- **Reservation semantics:** a General item is reserved on the nearest `GoodsStore` with available stock, like logs.
  Reserved stock still counts in village totals (it is still in the store) but not in `AvailableOf`.
- **Save versions:** every task that changes the save format bumps `SaveWriter.CurrentVersion` by exactly one. Every
  such task lists `Assets/Sim/Save/SaveWriter.cs` and `Sim.Tests/Save/SimSaveRoundTripTests.cs` in its touches, so save
  tasks are **serialized**: never two on parallel branches. A task that does not change the format does not bump.

**Consequences.** New materials are data: an `ItemDef` plus recipe counts. Thatch and panels in M20 need no new
construction code. Old saves are refused (version bump, decision 0038).
