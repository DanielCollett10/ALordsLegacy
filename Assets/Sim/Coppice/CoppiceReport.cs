using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Coppice
{
    /// <summary>One rod kind on the coppice panel: stock out of its cap, and the stools of that kind in the work area.</summary>
    public sealed class CoppiceKindLine
    {
        /// <summary>The stool kind (HazelStool or WillowStool).</summary>
        public BushKind Kind { get; internal set; }
        /// <summary>Item id of the rod, e.g. "item.hazel_rod".</summary>
        public string ItemId { get; internal set; }
        /// <summary>Short name, "hazel" or "willow".</summary>
        public string Name { get; internal set; }
        /// <summary>Rods of this kind in the hut store.</summary>
        public int Stock { get; internal set; }
        /// <summary>Per-kind cap (40), or 0 when the hut has no store yet.</summary>
        public int Cap { get; internal set; }
        /// <summary>Stools of this kind in the work area with rods left.</summary>
        public int StoolsAvailable { get; internal set; }
        /// <summary>Stools of this kind in the work area that are emptied and waiting to regrow.</summary>
        public int StoolsRegrowing { get; internal set; }
        /// <summary>"hazel: 12 / 40".</summary>
        public string StockText => Name + ": " + Stock + " / " + Cap;
        /// <summary>"3 available, 2 regrowing".</summary>
        public string StoolsText => StoolsAvailable + " available, " + StoolsRegrowing + " regrowing";
    }

    /// <summary>Everything the coppice hut panel shows, read from the Sim in one go (T-566, pattern of GranaryReport). Immutable. Read only.</summary>
    public sealed class CoppiceReport
    {
        /// <summary>The coppice hut footprint id.</summary>
        public int FootprintId { get; private set; }
        /// <summary>True once the hut is built.</summary>
        public bool IsBuilt { get; private set; }
        /// <summary>True when a coppice operation uses this hut.</summary>
        public bool HasOperation { get; private set; }
        /// <summary>The coppice status (only meaningful when <see cref="HasOperation"/>).</summary>
        public OperationState State { get; private set; }
        /// <summary>Short status text, e.g. "Working", "No stools", "Storage full".</summary>
        public string StatusText { get; private set; }
        /// <summary>One line per rod kind, hazel first.</summary>
        public IReadOnlyList<CoppiceKindLine> Kinds { get; private set; }

        private CoppiceReport() { }

        /// <summary>The line for a stool kind, or null.</summary>
        public CoppiceKindLine Line(BushKind kind)
        {
            foreach (CoppiceKindLine l in Kinds) if (l.Kind == kind) return l;
            return null;
        }

        /// <summary>The report for a coppice hut footprint, or null when the id is not a coppice hut.</summary>
        public static CoppiceReport Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null || fp.Type.Id != ModuleTypes.CoppiceHutId) return null;
            var r = new CoppiceReport { FootprintId = footprintId, IsBuilt = world.IsBuilt(footprintId) };
            Operation op = world.Operations.FindByModule(footprintId);
            r.HasOperation = op != null;
            LedgerLocation store = world.Inventory.FindGoodsStore(footprintId);
            TreeRegion region = op == null ? null : OperationStatus.WorkRegion(world, op);
            var lines = new List<CoppiceKindLine>();
            foreach (BushKind k in CoppiceStores.StoolKinds)
            {
                ItemDef rod = CoppiceStores.RodOf(k);
                var line = new CoppiceKindLine
                {
                    Kind = k,
                    ItemId = rod.Id,
                    Name = k == BushKind.WillowStool ? "willow" : "hazel",
                    Stock = store == null ? 0 : store.CountOf(rod.Id),
                    Cap = store == null ? 0 : store.ItemCapOf(rod.Id),
                };
                if (region != null)
                    foreach (BushRecord b in world.Bushes.All)
                    {
                        if (b.Kind != k || !region.Contains(b.X, b.Z)) continue;
                        if (b.Baskets > 0) line.StoolsAvailable++; else line.StoolsRegrowing++;
                    }
                lines.Add(line);
            }
            r.Kinds = lines;
            if (!r.IsBuilt) r.StatusText = "Under construction";
            else if (op == null) { r.State = OperationState.NoWorkers; r.StatusText = "No workers"; }
            else { r.State = CoppiceStatus.Of(world, op); r.StatusText = OperationText.Of(r.State); }
            return r;
        }
    }
}
