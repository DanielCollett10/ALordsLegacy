using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>
    /// Where a forager may deliver (FD-23b, FD-24): the camp's own hut store and every built granary store, never the
    /// camp food store (FD-12). Pure queries; nothing is stored. Room is the store's free space minus what unfinished
    /// forage tasks have already promised it.
    /// </summary>
    public static class ForagingStores
    {
        /// <summary>The food stores a forager of the operation may deliver to: its own built hut, then built granaries.</summary>
        public static List<LedgerLocation> Candidates(SimWorld world, Operation op)
        {
            var list = new List<LedgerLocation>();
            Footprint hut = OperationStatus.Hut(world, op);
            if (hut != null)
            {
                LedgerLocation own = world.Inventory.FindFoodStore(hut.Id);
                if (own != null) list.Add(own);
            }
            foreach (Footprint f in world.Footprints.All)
            {
                if (!f.Type.IsStandalone || f.Type.FoodCapacity <= 0 || !world.IsWorking(f.Id)) continue;
                LedgerLocation s = world.Inventory.FindFoodStore(f.Id);
                if (s != null) list.Add(s);
            }
            return list;
        }

        /// <summary>Items that unfinished forage tasks have promised to a store but not yet put in it.</summary>
        public static int Pending(SimWorld world, int storeLocationId)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != ForagingSystem.ForageTaskId) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositForage && s.LocationId == storeLocationId) n += s.Count;
            }
            return n;
        }

        /// <summary>True when every candidate store is physically full (FD-23b: Storage full). False when there is no store at all.</summary>
        public static bool AllFull(SimWorld world, Operation op)
        {
            List<LedgerLocation> stores = Candidates(world, op);
            if (stores.Count == 0) return false;
            foreach (LedgerLocation s in stores) if (s.FreeSpace > 0) return false;
            return true;
        }

        /// <summary>
        /// The nearest candidate store with room left after pending deliveries, measured from (x, z) to the building
        /// centre; ties go to the lower location id. Null when none has room. <paramref name="room"/> is its room.
        /// </summary>
        public static LedgerLocation NearestWithRoom(SimWorld world, Operation op, double x, double z, out int room)
        {
            LedgerLocation best = null;
            double bestD = double.MaxValue;
            room = 0;
            foreach (LedgerLocation s in Candidates(world, op))
            {
                Footprint f = world.Footprints.Get(s.ModuleId);
                if (f == null) continue;
                int r = s.FreeSpace - Pending(world, s.Id);
                if (r <= 0) continue;
                double dx = f.Centre.XMetres - x, dz = f.Centre.ZMetres - z, d = dx * dx + dz * dz;
                if (d < bestD - 1e-9 || (d <= bestD + 1e-9 && best != null && s.Id < best.Id)) { best = s; bestD = d; room = r; }
            }
            return best;
        }
    }
}
