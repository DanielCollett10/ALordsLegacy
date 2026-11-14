using System.Collections.Generic;

namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// The one ordered list of save sections after Core (decision 0034): Land, Structures, Ledger, Population,
    /// Movement, Tasks. <see cref="SimSave"/> Write and Read both walk it in this order. A new section goes at the
    /// end unless it must come earlier, and that change is recorded in decision 0034.
    /// </summary>
    public static class SaveSections
    {
        /// <summary>Creates the section list in fixed order. A new list each call. Empty until sections are added.</summary>
        public static IReadOnlyList<ISaveSection> CreateDefault()
        {
            return new ISaveSection[]
            {
                // 1 Land (trees, roads, areas)
                // 2 Structures (footprints, operations, construction sites)
                // 3 Ledger
                // 4 Population (families, citizens, oxen)
                // 5 Movement (move orders, follow links, routine, work day, debug send)
                // 6 Tasks
            };
        }
    }
}
