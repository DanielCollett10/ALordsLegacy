# 0044: Only store families move goods from production buildings into stores

**Context.** Owner, 16/11/2026: "only storage related families move items from a production / gathering building into a
store house". Decision 0043 let foragers carry baskets straight to the nearest granary with room.

**Decision.**
- Producers deliver only to their **own** building's store (foragers: their forager hut store). They never carry to a
  granary or storage yard. 0043's candidate list shrinks to the operation's own hut store; the rest of 0043 stands
  (reservations, room, carrier items, drop on failure, never the camp).
- Store families (families assigned to a granary, later a storage yard) collect from production stores into their
  store, and carry from their store to their market stall (markets.md MK-24).
- A full hut store shows *Storage full* and stops gathering until a store family empties it.

**Consequences.** `ForagingStores.NearestWithRoom` returns only the hut store. A granary with no families stays empty.
Granary families have work even before a market exists. The same rule applies to later production (bakery, INV-A8).
