using System.Collections.Generic;

namespace LordsLegacy.Sim.Kit
{
    /// <summary>One building's open claim for kit (ST-21, ST-24): who, and the tick it first claimed.</summary>
    public sealed class KitClaim
    {
        /// <summary>The kit-using building (footprint id).</summary>
        public int BuildingId { get; }
        /// <summary>The tick the building first claimed; never changes while the claim stays open.</summary>
        public long FirstClaimTick { get; }

        /// <summary>Creates a claim.</summary>
        public KitClaim(int buildingId, long firstClaimTick) { BuildingId = buildingId; FirstClaimTick = firstClaimTick; }
    }

    /// <summary>
    /// The queue of buildings claiming kit (decision 0052, ST-24). <see cref="Ordered"/> lists oldest first,
    /// ties to the lower building id. Saved in the Structures section.
    /// </summary>
    public sealed class KitClaims
    {
        private readonly Dictionary<int, KitClaim> _byBuilding = new Dictionary<int, KitClaim>();

        /// <summary>Number of open claims.</summary>
        public int Count => _byBuilding.Count;

        /// <summary>The building's claim, or null.</summary>
        public KitClaim Get(int buildingId) => _byBuilding.TryGetValue(buildingId, out KitClaim c) ? c : null;

        /// <summary>Opens a claim at <paramref name="tick"/>. A building that already claims keeps its first tick.</summary>
        public KitClaim Claim(int buildingId, long tick)
        {
            if (_byBuilding.TryGetValue(buildingId, out KitClaim c)) return c;
            c = new KitClaim(buildingId, tick);
            _byBuilding[buildingId] = c;
            return c;
        }

        /// <summary>Closes the building's claim. False if there was none.</summary>
        public bool Release(int buildingId) => _byBuilding.Remove(buildingId);

        /// <summary>All claims, oldest first, ties to the lower building id (ST-24).</summary>
        public List<KitClaim> Ordered()
        {
            var list = new List<KitClaim>(_byBuilding.Values);
            list.Sort((a, b) => a.FirstClaimTick != b.FirstClaimTick ? a.FirstClaimTick.CompareTo(b.FirstClaimTick) : a.BuildingId.CompareTo(b.BuildingId));
            return list;
        }
    }
}
