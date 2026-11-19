using System.Collections.Generic;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>The construction sites of a world, keyed by footprint id (decision 0033).</summary>
    public sealed class ConstructionRegistry
    {
        private readonly SortedDictionary<int, ConstructionSite> _sites = new SortedDictionary<int, ConstructionSite>();

        /// <summary>All sites in footprint id order.</summary>
        public IEnumerable<ConstructionSite> Sites => _sites.Values;

        /// <summary>Increases whenever a site is added or removed (a footprint becomes Built). Not saved; used by nav sync.</summary>
        public int Version { get; private set; }

        /// <summary>Number of unbuilt footprints.</summary>
        public int Count => _sites.Count;

        /// <summary>Adds a site. Throws if the footprint already has one.</summary>
        public ConstructionSite Add(ConstructionSite site)
        {
            if (site == null) throw new System.ArgumentNullException(nameof(site));
            if (_sites.ContainsKey(site.FootprintId))
                throw new System.InvalidOperationException("Footprint " + site.FootprintId + " already has a site.");
            _sites.Add(site.FootprintId, site);
            Version++;
            return site;
        }

        /// <summary>The site of a footprint, or null if it is built (or unknown).</summary>
        public ConstructionSite Get(int footprintId) => _sites.TryGetValue(footprintId, out var s) ? s : null;

        /// <summary>Removes a site (the footprint becomes Built). Returns false if there was none.</summary>
        public bool Remove(int footprintId)
        {
            if (!_sites.Remove(footprintId)) return false;
            Version++;
            return true;
        }
    }
}
