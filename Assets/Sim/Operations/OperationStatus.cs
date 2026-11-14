using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

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
        /// <summary>Logs wait for an ox and none is free (only from <see cref="OperationStatus.Reported"/>; felling continues).</summary>
        WaitingForOx = 7,
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
            if (Hut(world, op) == null) return OperationState.MissingHut; if (!op.HutBuilt) return OperationState.MissingHut;
            if (StorageModules(world, op).Count == 0) return OperationState.MissingStorage;
            if (!HasWorker(world, op)) return OperationState.NoWorkers;
            TreeRegion region = WorkRegion(world, op);
            if (region == null || !world.Trees.AnyStandingIn(region)) return OperationState.NoTrees;
            if (IsStorageFull(world, op)) return OperationState.StorageFull;
            return OperationState.Working;
        }

        /// <summary>
        /// The status to show: <see cref="Of"/>, except that a Working operation with a Haul task still waiting for an
        /// ox reports <see cref="OperationState.WaitingForOx"/> (WCO-06, TSK-09). Felling carries on either way, so
        /// the woodcutting system gates on <see cref="Of"/>, not on this.
        /// </summary>
        public static OperationState Reported(SimWorld world, Operation op)
        {
            OperationState s = Of(world, op);
            if (s != OperationState.Working && s != OperationState.StorageFull) return s;
            return IsWaitingForOx(world, op) ? OperationState.WaitingForOx : s;
        }

        /// <summary>True when any unfinished task of the operation needs an ox and has none.</summary>
        public static bool IsWaitingForOx(SimWorld world, Operation op)
        {
            foreach (var t in world.Tasks.ByOwner(op.Id))
                if (t.IsWaitingForOx) return true;
            return false;
        }

        /// <summary>
        /// The logs this operation is responsible for (WC-25, WC-37): logs in its ground piles, in its storage modules
        /// and on the carriers of its oxen, plus room promised to logs that are not yet anywhere physical. Room reserved
        /// for a haul whose logs are already in a pile or on a carrier is not counted again: the reserved incoming room
        /// is counted only where it exceeds the logs held in those places (<c>incoming − reserved pile stock − carried</c>).
        /// This is the one place the log sum lives; felling and <see cref="IsStorageFull"/> use it.
        /// </summary>
        public static int LogLoad(SimWorld world, Operation op)
        {
            var storageIds = new HashSet<int>();
            foreach (var f in StorageModules(world, op)) storageIds.Add(f.Id);
            int piles = 0, pileReserved = 0, stored = 0, incoming = 0;
            foreach (var l in world.Inventory.Locations)
            {
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id)
                {
                    piles += l.CountOf(Items.Log.Id);
                    pileReserved += l.ReservedOf(Items.Log.Id);
                }
                else if (l.Kind == LocationKind.Storage && storageIds.Contains(l.ModuleId))
                {
                    stored += l.CountOf(Items.Log.Id);
                    incoming += l.IncomingOf(Items.Log.Id);
                }
            }
            int carried = 0;
            foreach (var c in CarrierLocations(world, op)) carried += c.CountOf(Items.Log.Id);
            int unbacked = System.Math.Max(0, incoming - pileReserved - carried);
            return piles + stored + carried + unbacked;
        }

        /// <summary>
        /// The carrier locations of the oxen this operation uses: its assigned oxen and any ox held by one of its
        /// unfinished or recent tasks. Oxen without a carrier yet are skipped.
        /// </summary>
        internal static List<LedgerLocation> CarrierLocations(SimWorld world, Operation op)
        {
            var oxIds = new HashSet<int>(op.OxIds);
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.OxId.HasValue) oxIds.Add(t.OxId.Value);
            var list = new List<LedgerLocation>();
            foreach (int oxId in oxIds)
            {
                Ox ox = world.People.FindOx(oxId);
                LedgerLocation c = ox != null && ox.CarrierId != 0 ? world.Inventory.Get(ox.CarrierId) : null;
                if (c != null) list.Add(c);
            }
            return list;
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

        /// <summary>The operation's hut module, or null.</summary>
        public static Footprint Hut(SimWorld world, Operation op)
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

        internal static List<Footprint> StorageModules(SimWorld world, Operation op)
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
