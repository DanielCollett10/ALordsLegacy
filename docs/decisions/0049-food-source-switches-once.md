# 0049: The Sunday food source switches once, at the handover

**Context.** Households fetch their week on Sunday. Before any stall has stock, the camp supplies them. Once a built
granary supplies the market, stalls take over. If both sources served the same household's week, the range and
removal rules would have to handle a mix of both.

**Decision.**
- `SimWorld.FoodHandedOver` is set once, by `StoreFamilySystem`, when a granary supplies a built market
  (the switch is `SetGranarySupplies`). It is saved in the Food section and never cleared, even if the granary is switched off or lost (CMP-22).
- Before the flag is set, `AllotmentSystem` draws only from the camp. After it, only from stalls in range. A household
  draws from one source, never a mix (MK-31).

**Consequences.** A switch-on late in the week gives a short first Sunday. After the switch the camp store is emptied
into the granary (CMP-21) and the camp is removed once everyone is housed. Any future food source, such as a storage
yard, must follow the same one-way rule.
