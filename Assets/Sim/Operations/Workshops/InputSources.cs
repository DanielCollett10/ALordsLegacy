using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>One place an input item can be fetched from, and where it lies.</summary>
    public readonly struct InputSource
    {
        /// <summary>The ledger location holding the stock.</summary>
        public readonly LedgerLocation Location;
        /// <summary>Where the stock lies (for the nearest search).</summary>
        public readonly PointMm Position;

        /// <summary>Creates a source.</summary>
        public InputSource(LedgerLocation location, PointMm position) { Location = location; Position = position; }
    }

    /// <summary>Finds the candidate sources of one item. Register one per item with <see cref="InputSources.Register"/>.</summary>
    public delegate IEnumerable<InputSource> InputSourceFinder(SimWorld world, ItemDef item);

    /// <summary>
    /// Where an ox may fetch a workshop input item from (T-522, decision 0047, PL-04): an extendable list of finders per
    /// item. <see cref="Nearest(SimWorld, ItemDef, PointMm)"/> returns the nearest candidate with unreserved stock, and
    /// never a <see cref="LocationKind.WorkshopStock"/> or stock reserved for a site. Logs share
    /// <see cref="ConstructionSources"/> and add woodcutting operation piles; beams come from built workshop and storage-yard goods stores (PL-04, ST-07).
    /// </summary>
    public sealed class InputSources
    {
        private readonly Dictionary<string, List<InputSourceFinder>> _finders =
            new Dictionary<string, List<InputSourceFinder>>(StringComparer.Ordinal);

        /// <summary>A set holding the built-in finders (logs, beams).</summary>
        public static InputSources CreateDefault()
        {
            var s = new InputSources();
            s.Register(Items.Log, LogFinder);
            s.Register(Items.Beam, BeamFinder);
            return s;
        }

        /// <summary>Nearest source of <paramref name="item"/> using the built-in finders, or null.</summary>
        public static LedgerLocation Nearest(SimWorld world, ItemDef item, PointMm from) =>
            CreateDefault().Find(world, item, from);

        /// <summary>Adds a finder for an item; the search code needs no change.</summary>
        public void Register(ItemDef item, InputSourceFinder finder)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (finder == null) throw new ArgumentNullException(nameof(finder));
            if (!_finders.TryGetValue(item.Id, out List<InputSourceFinder> list))
                _finders[item.Id] = list = new List<InputSourceFinder>();
            list.Add(finder);
        }

        /// <summary>Nearest registered source of <paramref name="item"/> with unreserved stock, or null. Ties go to the lower location id.</summary>
        public LedgerLocation Find(SimWorld world, ItemDef item, PointMm from)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_finders.TryGetValue(item.Id, out List<InputSourceFinder> list)) return null;
            LedgerLocation best = null;
            double bestD = double.MaxValue;
            foreach (InputSourceFinder finder in list)
            {
                IEnumerable<InputSource> found = finder(world, item);
                if (found == null) continue;
                foreach (InputSource s in found)
                {
                    LedgerLocation l = s.Location;
                    if (l == null || l.Kind == LocationKind.WorkshopStock || l.AvailableOf(item.Id) < 1) continue;
                    double d = s.Position.DistanceTo(from);
                    if (d < bestD || (d == bestD && best != null && l.Id < best.Id)) { best = l; bestD = d; }
                }
            }
            return best;
        }

        private static IEnumerable<InputSource> LogFinder(SimWorld world, ItemDef item)
        {
            foreach (ConstructionSources.Source s in ConstructionSources.Sources(world, item))
                yield return new InputSource(s.Location, s.Position);
            foreach (LedgerLocation l in world.Inventory.Locations)
            {
                if (l.Kind != LocationKind.GroundPile || l.OwnerOperationId == null) continue;
                Operation op = world.Operations.Get(l.OwnerOperationId.Value);
                if (op != null && op.TypeId == OperationTypes.WoodcuttingId) yield return new InputSource(l, l.Position);
            }
        }

        private static IEnumerable<InputSource> BeamFinder(SimWorld world, ItemDef item)
        {
            foreach (LedgerLocation l in world.Inventory.Locations)
            {
                if (l.Kind != LocationKind.GoodsStore) continue;
                Footprint f = world.Footprints.Get(l.ModuleId);
                if (f == null || !WorkshopBuildings.HoldsGoods(f.Type) || !world.IsWorking(f.Id)) continue;
                yield return new InputSource(l, f.Centre);
            }
        }
    }
}
