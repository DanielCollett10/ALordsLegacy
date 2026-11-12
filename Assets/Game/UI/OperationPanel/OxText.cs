using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Game
{
    /// <summary>Text for oxen shown in the operation panel and the citizen/ox panel. Reads the Sim only.</summary>
    public static class OxText
    {
        /// <summary>The ox's state: Free, Reserved for a task, Led by a citizen, or Resting.</summary>
        public static string State(SimWorld world, Ox ox)
        {
            OxState s = ox.State;
            switch (s.Kind)
            {
                case OxStateKind.Reserved: return "Reserved for a haul";
                case OxStateKind.Led:
                    Citizen c = world.People.FindCitizen(s.HandlerId);
                    return "Led by " + (c != null ? c.Name : "a citizen");
                case OxStateKind.Resting: return "Resting";
                default: return "Free";
            }
        }

        /// <summary>The ox's owner building, e.g. "Woodcutter 1", or "Shared pool" when it belongs to none.</summary>
        public static string Owner(SimWorld world, Ox ox)
        {
            Operation op = world.Operations.FindOxOwner(ox.Id);
            return op == null ? "Shared pool" : "Woodcutter " + op.Id;
        }

        /// <summary>What the ox carries, e.g. "4 logs" or "nothing".</summary>
        public static string Carrying(SimWorld world, Ox ox)
        {
            LedgerLocation carrier = ox.CarrierId != 0 ? world.Inventory.Get(ox.CarrierId) : null;
            int logs = carrier == null ? 0 : carrier.CountOf(Items.Log.Id);
            return logs == 0 ? "nothing" : logs + (logs == 1 ? " log" : " logs");
        }
    }
}
