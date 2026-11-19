# 0061: Household level is stored; its needs are derived on read; only a command raises it

**Context.** M21 adds household levels 1 and 2 (`design/household-levels.md` HL-20..26). The owner wants the player to
press Upgrade, nothing automatic (HL-22). Needs depend on inputs that change daily: the last Sunday allotment's food
types, hunger, and the fence state. Storing a "needs met" flag would go stale, and a daily job would make the panel lag.

**Decision.**
- The level is stored on the family: `Family.Level`, starting at 1. It is saved in the Population section and it never
  drops. Moving house keeps it.
- Needs are not stored. `HouseholdNeedsReport.Of` derives each need and `Eligible` on every call. The inputs are stored
  (`Family.AllotmentFoodTypes`, `IsHungry`, the fence link).
- The level table is data (`HouseholdLevels`, `NeedDef`). The DiverseFood threshold is `SimTuning.DiverseFoodTypes`.
- `UpgradeHousehold` is the only thing that raises a level. It re-runs the same check, refuses with the first reason, and
  logs the change to the settlement log. Its cost is `HouseholdLevelDef.UpgradeCost` (empty in M21).
- Read models (`HouseReport`, `HouseholdLevelCounts`) never change the world.

**Consequences.** Eligibility is always current, and no daily tick or save field is needed for it. The panel and the
command cannot disagree, because both call the same check. A future rule that needs a timer (for example "met for three
days") must add stored progress, and that is a new decision. Level 2 gives nothing yet (HL-25), so the level is only
shown and counted.
