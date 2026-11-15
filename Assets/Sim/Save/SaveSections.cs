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
                new Sections.LandSection(), // 1 Land (trees, roads; areas follow in part 2)
                new Sections.StructuresSection(), // 2 Structures (footprints, camp, operations, construction sites)
                new Sections.LedgerSection(), // 3 Ledger (locations, reservations, journal, id counters)
                new Sections.PopulationSection(), // 4 Population (families, citizens, oxen)
                new Sections.HomesSection(), // 4b Homes (houses, plots, family links; needs footprints, areas, families)
                new Sections.FoodSection(), // 4c Food (camp food store, larder ids; needs ledger, families, homes)
                new Sections.MovementSection(), // 5 Movement (move orders, follow links, routine, work day, debug send)
                new Sections.TasksSection(), // 6 Tasks (task board: records, steps, progress, id counter)
            };
        }
    }
}
