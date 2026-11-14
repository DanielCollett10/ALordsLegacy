using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>
    /// Where construction may take materials from (CN-11..13): the camp stockpile, every operation's BUILT log
    /// storage and unowned ground piles. Operation-owned piles, unbuilt storage, carriers and site stores are not sources.
    /// </summary>
    public static class ConstructionSources
    {
        /// <summary>Sum of available (total − reserved) stock of <paramref name="item"/> over all construction sources.</summary>
        public static int Available(SimWorld world, ItemDef item)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (item == null) throw new System.ArgumentNullException(nameof(item));
            long total = 0;
            foreach (var s in Sources(world)) total += s.Location.AvailableOf(item.Id);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// Reserves <paramref name="n"/> of <paramref name="item"/> from the sources nearest <paramref name="sitePoint"/>
        /// first, combining sources. All or nothing: returns null (nothing reserved) when fewer than n are available
        /// or n is not positive. Otherwise returns the reservation ids in the order taken.
        /// </summary>
        public static List<int> TryReserve(SimWorld world, ItemDef item, int n, PointMm sitePoint, int owner)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (item == null) throw new System.ArgumentNullException(nameof(item));
            if (n <= 0) return null;
            if (Available(world, item) < n) return null;

            var sources = Sources(world);
            sources.Sort((a, b) =>
            {
                int c = a.Position.DistanceTo(sitePoint).CompareTo(b.Position.DistanceTo(sitePoint));
                return c != 0 ? c : a.Location.Id.CompareTo(b.Location.Id);
            });

            var ids = new List<int>();
            int left = n;
            foreach (var s in sources)
            {
                if (left == 0) break;
                int take = System.Math.Min(left, s.Location.AvailableOf(item.Id));
                if (take <= 0) continue;
                int id = world.Inventory.ReserveStock(s.Location, item, take, owner);
                if (id == 0) { Rollback(world, ids); return null; }
                ids.Add(id);
                left -= take;
            }
            if (left > 0) { Rollback(world, ids); return null; }
            return ids;
        }

        private static void Rollback(SimWorld world, List<int> ids)
        {
            foreach (int id in ids) world.Inventory.Release(id);
        }

        private struct Source
        {
            public LedgerLocation Location;
            public PointMm Position;
        }

        private static List<Source> Sources(SimWorld world)
        {
            var builtStorage = new HashSet<int>();
            foreach (Operation op in world.Operations.All)
                foreach (Footprint f in OperationStatus.StorageModules(world, op))
                    if (world.IsBuilt(f.Id)) builtStorage.Add(f.Id);

            var list = new List<Source>();
            foreach (LedgerLocation l in world.Inventory.Locations)
            {
                switch (l.Kind)
                {
                    case LocationKind.Stockpile:
                        list.Add(new Source { Location = l, Position = l.Position });
                        break;
                    case LocationKind.GroundPile:
                        if (l.OwnerOperationId == null) list.Add(new Source { Location = l, Position = l.Position });
                        break;
                    case LocationKind.Storage:
                        if (builtStorage.Contains(l.ModuleId))
                        {
                            Footprint f = world.Footprints.Get(l.ModuleId);
                            list.Add(new Source { Location = l, Position = f.Centre });
                        }
                        break;
                }
            }
            return list;
        }
    }
}
