using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>Why a thatcher's hut is or is not working (MAT-39e). Computed, never stored.</summary>
    public static class ThatchingStatus
    {
        /// <summary>Working, or the first cause: Paused, MissingHut, NoWorkers, NoBushes (no reed bed), StorageFull.</summary>
        public static OperationState Of(SimWorld world, Operation op)
        {
            if (op.Paused) return OperationState.Paused;
            if (OperationStatus.Hut(world, op) == null) return OperationState.MissingHut;
            if (!OperationStatus.HasWorker(world, op)) return OperationState.NoWorkers;
            var region = OperationStatus.WorkRegion(world, op);
            if (region == null || !world.Bushes.AnyWithBasketsIn(region, BushKind.ReedBed)) return OperationState.NoBushes;
            LedgerLocation store = HutStore(world, op);
            if (store != null && store.FreeSpaceFor(Items.Thatch.Id) <= 0) return OperationState.StorageFull;
            return OperationState.Working;
        }

        /// <summary>The operation's built hut store, or null.</summary>
        public static LedgerLocation HutStore(SimWorld world, Operation op)
        {
            Footprints.Footprint hut = OperationStatus.Hut(world, op);
            return hut == null ? null : world.Inventory.FindGoodsStore(hut.Id);
        }

        /// <summary>True in December, January and February: the full cutting rate (MAT-34).</summary>
        public static bool IsWinter(SimWorld world) => world.Now.Season == Calendar.Season.Winter;

        /// <summary>
        /// The panel text for a thatcher's status (MAT-39e): "Cutting" in winter, "Cutting (slow season)" otherwise,
        /// "No reed beds", "Store full"; every other state uses <see cref="OperationText"/>.
        /// </summary>
        public static string TextOf(SimWorld world, OperationState s)
        {
            switch (s)
            {
                case OperationState.Working: return IsWinter(world) ? "Cutting" : "Cutting (slow season)";
                case OperationState.NoBushes: return "No reed beds";
                case OperationState.StorageFull: return "Store full";
                default: return OperationText.Of(s);
            }
        }
    }
}
