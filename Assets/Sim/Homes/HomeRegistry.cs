using System;
using System.Collections.Generic;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>One house with its plot and the family living in it (HM-10). Family id 0 means Empty.</summary>
    public sealed class HomeRecord
    {
        /// <summary>Footprint id of the house. Unique key.</summary>
        public int HouseFootprintId { get; }
        /// <summary>Area id of the plot the house stands on.</summary>
        public int PlotAreaId { get; }
        /// <summary>Id of the family living here, or 0 while the house is Empty.</summary>
        public int FamilyId { get; internal set; }

        /// <summary>Ledger id of the house's larder (FD-10), or 0 for none yet. Saved by the Food section.</summary>
        public int LarderLocationId { get; internal set; }

        internal HomeRecord(int houseFootprintId, int plotAreaId, int familyId)
        {
            HouseFootprintId = houseFootprintId; PlotAreaId = plotAreaId; FamilyId = familyId;
        }
    }

    /// <summary>
    /// Links each house to its plot and its family (HM-10, HM-13). Records iterate by ascending house footprint id.
    /// Moving a family in sets <see cref="Family.HomeHouseId"/>, so it is no longer Homeless. Saved by the Homes section.
    /// </summary>
    public sealed class HomeRegistry
    {
        private readonly SortedDictionary<int, HomeRecord> _homes = new SortedDictionary<int, HomeRecord>();

        /// <summary>Increases on every add and every family change.</summary>
        public int Version { get; private set; }

        /// <summary>Number of houses.</summary>
        public int Count => _homes.Count;

        /// <summary>All homes by ascending house id.</summary>
        public IEnumerable<HomeRecord> All => _homes.Values;

        /// <summary>The home of a house, or null.</summary>
        public HomeRecord Get(int houseFootprintId) => _homes.TryGetValue(houseFootprintId, out var h) ? h : null;

        /// <summary>The home a family lives in, or null while it is Homeless.</summary>
        public HomeRecord FindByFamily(int familyId)
        {
            foreach (HomeRecord h in _homes.Values) if (h.FamilyId == familyId) return h;
            return null;
        }

        /// <summary>The home whose plot is this area, or null (HM-14b: a plot with a house cannot be deleted).</summary>
        public HomeRecord FindByPlot(int plotAreaId)
        {
            foreach (HomeRecord h in _homes.Values) if (h.PlotAreaId == plotAreaId) return h;
            return null;
        }

        /// <summary>Adds an Empty home. Throws if the house already has one.</summary>
        public HomeRecord Add(int houseFootprintId, int plotAreaId)
        {
            if (_homes.ContainsKey(houseFootprintId)) throw new ArgumentException("Duplicate house " + houseFootprintId);
            var h = new HomeRecord(houseFootprintId, plotAreaId, 0);
            _homes.Add(houseFootprintId, h);
            Version++;
            return h;
        }

        /// <summary>Links an Empty house to a Homeless family. Returns false (nothing changed) if either is not free.</summary>
        public bool MoveIn(HomeRecord home, Family family)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (home.FamilyId != 0 || !family.IsHomeless) return false;
            home.FamilyId = family.Id;
            family.HomeHouseId = home.HouseFootprintId;
            Version++;
            return true;
        }

        /// <summary>Makes a home Empty (its family link is cleared). The caller clears the family's side (<see cref="HouseRemoval"/>).</summary>
        public void Vacate(HomeRecord home)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (home.FamilyId == 0) return;
            home.FamilyId = 0;
            Version++;
        }

        /// <summary>Removes a home record. False if the house has none.</summary>
        public bool Remove(int houseFootprintId)
        {
            if (!_homes.Remove(houseFootprintId)) return false;
            Version++;
            return true;
        }

        /// <summary>Restore: adds a record with its family; the family's link is set by <see cref="CheckLinks"/>' caller.</summary>
        internal void Restore(int houseFootprintId, int plotAreaId, int familyId)
        {
            if (_homes.ContainsKey(houseFootprintId)) throw new ArgumentException("Duplicate house " + houseFootprintId);
            _homes.Add(houseFootprintId, new HomeRecord(houseFootprintId, plotAreaId, familyId));
            Version++;
        }
    }
}
