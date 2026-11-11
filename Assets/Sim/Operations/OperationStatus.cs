using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>Why an operation is or is not working. Listed in the order causes are checked.</summary>
    public enum OperationState
    {
        /// <summary>Nothing is stopping it.</summary>
        Working = 0,
        /// <summary>The player paused it.</summary>
        Paused = 1,
        /// <summary>No hut module (WC-05).</summary>
        MissingHut = 2,
        /// <summary>No log storage module (WC-05).</summary>
        MissingStorage = 3,
        /// <summary>No assigned family has a member aged 14 or more.</summary>
        NoWorkers = 4,
        /// <summary>No standing tree in the work region.</summary>
        NoTrees = 5,
        /// <summary>Logs held by the operation fill its storage (WC-25).</summary>
        StorageFull = 6,
    }

    /// <summary>Computes operation status from world state. Nothing here is stored (GDD section 5).</summary>
    public static class OperationStatus
    {
        /// <summary>Age in years at which a citizen can work.</summary>
        public const double WorkingAgeYears = 14;

        /// <summary>Working, or the first cause in order: Paused, MissingHut, MissingStorage, NoWorkers, NoTrees, StorageFull.</summary>
        public static OperationState Of(SimWorld world, Operation op)
        {
            if (op.Paused) return OperationState.Paused;
            if (Hut(world, op) == null) return OperationState.MissingHut;
            if (StorageModules(world, op).Count == 0) return OperationState.MissingStorage;
            if (!HasWorker(world, op)) return OperationState.NoWorkers;
            TreeRegion region = WorkRegion(world, op);
            if (region == null || !world.Trees.AnyStandingIn(region)) return OperationState.NoTrees;
            if (IsStorageFull(world, op)) return OperationState.StorageFull;
            return OperationState.Working;
        }

        /// <summary>Logs in ground piles produced by this operation plus logs in its storage modules (WC-25).</summary>
        public static int LogLoad(SimWorld world, Operation op)
        {
            var storageIds = new HashSet<int>();
            foreach (var f in StorageModules(world, op)) storageIds.Add(f.Id);
            int load = 0;
            foreach (var l in world.Inventory.Locations)
            {
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id) load += l.CountOf(Items.Log.Id);
                else if (l.Kind == LocationKind.Storage && storageIds.Contains(l.ModuleId)) load += l.CountOf(Items.Log.Id);
            }
            return load;
        }

        /// <summary>Total log capacity of the operation's storage modules.</summary>
        public static int LogCapacity(SimWorld world, Operation op)
        {
            int cap = 0;
            foreach (var f in StorageModules(world, op)) cap += f.Type.LogCapacity;
            return cap;
        }

        /// <summary>True when <see cref="LogLoad"/> has reached <see cref="LogCapacity"/> (and there is storage).</summary>
        public static bool IsStorageFull(SimWorld world, Operation op)
        {
            int cap = LogCapacity(world, op);
            return cap > 0 && LogLoad(world, op) >= cap;
        }

        /// <summary>The region trees are cut in: the work area polygon, or the default circle around the hut. Null if neither exists.</summary>
        public static TreeRegion WorkRegion(SimWorld world, Operation op)
        {
            if (op.WorkAreaId.HasValue)
            {
                AreaRecord work = world.Areas.Get(op.WorkAreaId.Value);
                if (work != null && work.Polygon.Count >= 3) return TreeRegion.Polygon(work.Polygon);
            }
            Footprint hut = Hut(world, op);
            if (hut == null) return null;
            OperationTypeDef type = OperationTypes.Find(op.TypeId);
            double radius = type != null ? type.DefaultWorkRadiusM : 0;
            return TreeRegion.Circle((float)hut.Centre.XMetres, (float)hut.Centre.ZMetres, (float)radius);
        }

        private static bool HasWorker(SimWorld world, Operation op)
        {
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c != null && c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= WorkingAgeYears) return true;
                }
            }
            return false;
        }

        private static Footprint Hut(SimWorld world, Operation op)
        {
            OperationTypeDef type = OperationTypes.Find(op.TypeId);
            if (type == null) return null;
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null && f.Type.Id == type.HutTypeId) return f;
            }
            return null;
        }

        private static List<Footprint> StorageModules(SimWorld world, Operation op)
        {
            var list = new List<Footprint>();
            OperationTypeDef type = OperationTypes.Find(op.TypeId);
            if (type == null) return list;
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null && f.Type.Id == type.LogStorageTypeId) list.Add(f);
            }
            return list;
        }
    }
}
