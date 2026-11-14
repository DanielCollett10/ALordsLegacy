using LordsLegacy.Sim;

namespace LordsLegacy.Sim.Construction
{
    public class ConstructionRegistry
    {
        private Dictionary<string, ConstructionSite> _sites = new Dictionary<string, ConstructionSite>();

        public void AddSite(string footprintId, ConstructionSite site)
        {
            _sites[footprintId] = site;
        }

        public ConstructionSite GetSite(string footprintId)
        {
            return _sites.ContainsKey(footprintId) ? _sites[footprintId] : null;
        }
    }

    public class ConstructionSite
    {
        public string FootprintId { get; }
        public ConstructionState State { get; set; }
        public string ReservationId { get; set; }
        public string LedgerLocationId { get; set; }
        public int ProgressMinutes { get; set; }
        public ConstructionPriority Priority { get; set; }

        public ConstructionSite(string footprintId, ConstructionState state, string reservationId, string ledgerLocationId, int progressMinutes, ConstructionPriority priority)
        {
            FootprintId = footprintId;
            State = state;
            ReservationId = reservationId;
            LedgerLocationId = ledgerLocationId;
            ProgressMinutes = progressMinutes;
            Priority = priority;
        }
    }

    public enum ConstructionState
    {
        Delivering,
        Building
    }

    public enum ConstructionPriority
    {
        Normal,
        High
    }
}
