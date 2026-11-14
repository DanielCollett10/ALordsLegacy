using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// The settlement-wide ox pool (WC-30..32, CON-11). Oxen are shared unless assigned to an operation through
    /// <see cref="OperationRegistry.AssignOx"/>. Finds oxen for work, tracks oxen-stand occupancy and picks rest spots.
    /// </summary>
    public sealed class OxPool
    {
        private readonly SimWorld _world;

        /// <summary>Creates a view over a world's oxen, operations and footprints.</summary>
        public OxPool(SimWorld world)
        {
            _world = world ?? throw new System.ArgumentNullException(nameof(world));
        }

        /// <summary>True if the ox can take new work: it is Free or Resting.</summary>
        public static bool IsAvailable(Ox ox) =>
            ox.State.Kind == OxStateKind.Free || ox.State.Kind == OxStateKind.Resting;

        /// <summary>
        /// Finds an ox for an operation: assigned oxen first, then unassigned pool oxen; the nearest to
        /// <paramref name="from"/> in each group (ties: lowest id). Oxen assigned to another operation are never
        /// returned. Null if none is available.
        /// </summary>
        public Ox FindFor(int operationId, PointMm from)
        {
            Ox assigned = null, pool = null;
            double assignedD = double.MaxValue, poolD = double.MaxValue;
            foreach (Ox ox in _world.People.Oxen)
            {
                if (!IsAvailable(ox)) continue;
                Operation owner = _world.Operations.FindOxOwner(ox.Id);
                double d = Dist2(ox, from);
                if (owner == null)
                {
                    if (d < poolD) { poolD = d; pool = ox; }
                }
                else if (owner.Id == operationId && d < assignedD)
                {
                    assignedD = d; assigned = ox;
                }
            }
            return assigned ?? pool;
        }

        /// <summary>Number of oxen resting at this oxen stand module.</summary>
        public int Occupancy(int standFootprintId)
        {
            int n = 0;
            foreach (Ox ox in _world.People.Oxen)
                if (ox.State.Kind == OxStateKind.Resting && ox.State.StandId == standFootprintId) n++;
            return n;
        }

        /// <summary>True if the stand module has room (the camp stand is not a module and has no limit).</summary>
        public bool HasRoom(Footprint stand, Ox excluding = null)
        {
            int n = Occupancy(stand.Id);
            if (excluding != null && excluding.State.Kind == OxStateKind.Resting && excluding.State.StandId == stand.Id) n--;
            return n < stand.Type.OxCapacity;
        }

        /// <summary>
        /// Where this ox should rest (WC-31, WC-32). Assigned: its operation's oxen stand if it has room, else beside
        /// the operation's log storage. Pool: the nearest free oxen stand of any operation or the camp stand, which
        /// has no limit. Returns the spot and the stand footprint id taken (0 for none).
        /// </summary>
        public PointMm RestSpot(Ox ox, out int standId)
        {
            standId = OxState.NoStand;
            Operation owner = _world.Operations.FindOxOwner(ox.Id);
            if (owner != null)
            {
                foreach (int id in owner.ModuleIds)
                {
                    Footprint f = _world.Footprints.Get(id);
                    if (f != null && _world.IsBuilt(id) && f.Type.OxCapacity > 0 && HasRoom(f, ox)) { standId = f.Id; return f.Centre; }
                }
                foreach (int id in owner.ModuleIds)
                {
                    Footprint f = _world.Footprints.Get(id);
                    if (f != null && _world.IsBuilt(id) && f.Type.LogCapacity > 0) return f.AccessPoint;
                }
                return CampStand(ox);
            }

            PointMm here = PointMm.FromMetres(ox.X, ox.Z);
            bool found = false;
            PointMm best = CampStand(ox);
            double bestD = _world.Camp != null ? D2(best, here) : double.MaxValue;
            foreach (Operation op in _world.Operations.All)
            {
                foreach (int id in op.ModuleIds)
                {
                    Footprint f = _world.Footprints.Get(id);
                    if (f == null || !_world.IsBuilt(id) || f.Type.OxCapacity <= 0 || !HasRoom(f, ox)) continue;
                    double d = D2(f.Centre, here);
                    if (d < bestD) { bestD = d; best = f.Centre; standId = f.Id; found = true; }
                }
            }
            if (!found) standId = OxState.NoStand;
            return best;
        }

        /// <summary>Sends an ox to its rest spot: sets state to Resting there and returns the spot.</summary>
        public PointMm Rest(Ox ox)
        {
            PointMm spot = RestSpot(ox, out int standId);
            ox.State = OxState.Resting(spot.X, spot.Z, standId);
            return spot;
        }

        private PointMm CampStand(Ox ox) =>
            _world.Camp != null ? _world.Camp.OxStand : PointMm.FromMetres(ox.HomeX, ox.HomeZ);

        private static double Dist2(Ox ox, PointMm p)
        {
            double dx = ox.X * 1000.0 - p.X, dz = ox.Z * 1000.0 - p.Z;
            return dx * dx + dz * dz;
        }

        private static double D2(PointMm a, PointMm b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }
    }
}
