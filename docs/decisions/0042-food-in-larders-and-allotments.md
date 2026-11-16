# 0042: Food is ledger stock in larders, and a week's allotment is a reservation

**Context.** Food has to be counted and moved like logs, and each family needs its own food whether it is homeless or
housed. Markets (M15) will later supply the same allotments, so the claim and the move must be separate steps.

**Decision.**
- Food is an `ItemCategory.Food` item in the ledger, held in `FoodStore` locations that refuse non-food. The camp food
  store is separate from the log stockpile.
- Every family and house has a larder location, created on first need (`Larders`). Moving in and out moves the food.
- At Sunday dawn each household's week (7 days of need) is reserved on its market's stock. The owner is
  `-1000 - familyId`. Nothing moves until the trip collects it.
- The Sunday trip is a `task.fetch_food` owned by the family, not an operation. Collect and deposit are ledger transfers.
- Eating is a ledger `Consume` at midnight. The part of the last item is carried in the family (`CarriedRations`).

**Consequences.** Food is never created or lost outside the ledger, and `Audit` checks it. Reservations, carriers and larder
ids are saved (Food and Ledger sections). A second market needs only `IFoodMarket`, though the trip target must change too.
