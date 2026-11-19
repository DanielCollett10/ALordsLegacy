using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>Writes construction site state changes to <see cref="SimLog"/> (T-629). Writes only; never changes state.</summary>
    public static class ConstructionLog
    {
        /// <summary>A site was created and its materials reserved.</summary>
        public static void Reserved(SimWorld world, int footprintId) => Write(world, footprintId, "reserved");

        /// <summary>A site moved from Delivering to Building.</summary>
        public static void Building(SimWorld world, int footprintId) => Write(world, footprintId, "building");

        /// <summary>A site finished and the footprint is Built.</summary>
        public static void Done(SimWorld world, int footprintId) => Write(world, footprintId, "done");

        /// <summary>A site was removed before it finished.</summary>
        public static void Cancelled(SimWorld world, int footprintId) => Write(world, footprintId, "cancelled");

        private static void Write(SimWorld world, int footprintId, string what) =>
            world.Log.Write(LogCategory.Construction, "site " + footprintId + " " + what);
    }
}
