using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>Why a foraging camp is or is not working (FD-22, FD-23b). Computed, never stored.</summary>
    public static class ForagingStatus
    {
        /// <summary>Working, or the first cause: Paused, MissingHut, NoWorkers, NoBushes, StorageFull.</summary>
        public static OperationState Of(SimWorld world, Operation op)
        {
            if (op.Paused) return OperationState.Paused;
            if (OperationStatus.Hut(world, op) == null) return OperationState.MissingHut;
            if (!HasForager(world, op)) return OperationState.NoWorkers;
            var region = OperationStatus.WorkRegion(world, op);
            if (region == null || !world.Bushes.AnyWithBasketsIn(region, BushKind.Berries, BushKind.Mushrooms)) return OperationState.NoBushes;
            if (ForagingStores.AllFull(world, op)) return OperationState.StorageFull;
            return OperationState.Working;
        }

        private static bool HasForager(SimWorld world, Operation op)
        {
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c != null && c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= world.Tuning.ForagerMinAge) return true;
                }
            }
            return false;
        }
    }
}
