using System;

namespace LordsLegacy.Sim.Save
{
    public class SaveHeader
    {
        public string Name { get; set; }
        public long CreatedTime { get; set; }
        public long GameTick { get; set; }
        public long MapId { get; set; }
        public long ContentHash { get; set; }
    }
}