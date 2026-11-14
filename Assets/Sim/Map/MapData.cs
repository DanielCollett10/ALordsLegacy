using System;

namespace LordsLegacy.Sim.Map
{
    using System;

namespace LordsLegacy.Sim.Map
{
    public class MapData
    {
        public long ContentHash { get; set; }
        public List<MapTree> Trees { get; set; }
        public List<int> Heights { get; set; }
    }
}
}