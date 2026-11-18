using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Coppice
{
    /// <summary>Why a coppice hut is or is not working (MAT-18). Computed, never stored.</summary>
    public static class CoppiceStatus
    {
        /// <summary>Working, or the first cause: Paused, MissingHut, NoWorkers, NoStools, StorageFull.</summary>
        public static OperationState Of(SimWorld world, Operation op)
        {
            if (op.Paused) return OperationState.Paused;
            if (OperationStatus.Hut(world, op) == null) return OperationState.MissingHut;
            if (!HasCutter(world, op)) return OperationState.NoWorkers;
            var region = OperationStatus.WorkRegion(world, op);
            if (region == null) return OperationState.NoStools;
            bool any = false, room = false;
            Inventory.LedgerLocation store = CoppiceStores.HutStore(world, op);
            foreach (BushKind k in CoppiceStores.StoolKinds)
            {
                if (!world.Bushes.AnyWithBasketsIn(region, k)) continue;
                any = true;
                // One kind full only stops that kind (MAT-18): the hut is full only when every kind with stools is.
                if (store != null && store.FreeSpaceFor(CoppiceStores.RodOf(k).Id) > 0) room = true;
            }
            if (!any) return OperationState.NoStools;
            return room ? OperationState.Working : OperationState.StorageFull;
        }

        private static bool HasCutter(SimWorld world, Operation op)
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
