using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>One store-to-store hand trip a yard job source asks for (ST-43): move <see cref="Count"/> of
    /// <see cref="Item"/> from <see cref="Source"/> to <see cref="Destination"/>. The planner trims the count to the hand
    /// load, the source's free stock and the destination's room.</summary>
    public sealed class YardTrip
    {
        /// <summary>Where the goods are picked up.</summary>
        public LedgerLocation Source { get; }
        /// <summary>Where the goods go.</summary>
        public LedgerLocation Destination { get; }
        /// <summary>The item moved.</summary>
        public ItemDef Item { get; }
        /// <summary>Items wanted; the planner may carry fewer.</summary>
        public int Count { get; }

        /// <summary>Creates a trip request.</summary>
        public YardTrip(LedgerLocation source, LedgerLocation destination, ItemDef item, int count)
        {
            Source = source; Destination = destination; Item = item; Count = count;
        }
    }

    /// <summary>A source of yard work (ST-43): names the next trip a yard's idle family member should make, or null.
    /// Holds the job rule; <see cref="YardFamilySystem"/> holds none.</summary>
    public interface IYardJobSource
    {
        /// <summary>The next trip for <paramref name="yard"/>, or null when this source has nothing to move.</summary>
        YardTrip NextTrip(SimWorld world, Operation yard);
    }

    /// <summary>The ordered list of yard job sources (first source with a usable trip wins). Starts empty.</summary>
    public sealed class YardJobs
    {
        private readonly List<IYardJobSource> _sources = new List<IYardJobSource>();

        /// <summary>The sources, in the order they are asked.</summary>
        public IReadOnlyList<IYardJobSource> Sources => _sources;

        /// <summary>
        /// The job sources the game runs, in order: the camp General store first (ST-40), then unowned General
        /// ground piles (ST-41). Both carry by hand through <c>task.haul_by_hand</c>.
        /// </summary>
        public static YardJobs Standard()
        {
            var jobs = new YardJobs();
            jobs.Add(new YardCampStoreSource());
            jobs.Add(new YardPileSource());
            jobs.Add(new YardWorkshopSource());
            jobs.Add(new YardBeamSource()); // beams by pool ox (ST-43); makes its own ox task
            return jobs;
        }

        /// <summary>Appends a source at the end of the order.</summary>
        public void Add(IYardJobSource source)
        {
            if (source != null) _sources.Add(source);
        }
    }
}
