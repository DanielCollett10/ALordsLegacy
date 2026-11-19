using System;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>
    /// What a thatcher's hut panel shows, read from the Sim in one go (T-576, pattern of ForagerHutReport). Immutable.
    /// Read only. Counts thatch sheaves (MAT-35): the hut store, stooks still curing (green, MAT-39c) and dry sheaves
    /// lying in the operation's piles waiting to be collected (MAT-39d).
    /// </summary>
    public sealed class ThatchingReport
    {
        private static readonly string[] MonthNames =
            { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        /// <summary>The thatcher's hut footprint id.</summary>
        public int FootprintId { get; private set; }
        /// <summary>True once the hut is built.</summary>
        public bool IsBuilt { get; private set; }
        /// <summary>True when a thatching operation uses this hut.</summary>
        public bool HasOperation { get; private set; }
        /// <summary>The thatching status (only meaningful when <see cref="HasOperation"/>).</summary>
        public OperationState State { get; private set; }
        /// <summary>Short status text, e.g. "Cutting (slow season)", "No reed beds", "Store full".</summary>
        public string StatusText { get; private set; }
        /// <summary>Thatch sheaves in the hut store.</summary>
        public int StoreCount { get; private set; }
        /// <summary>Thatch sheaves the hut store holds at most.</summary>
        public int StoreCapacity { get; private set; }
        /// <summary>Stooks still curing for this operation.</summary>
        public int StooksCuring { get; private set; }
        /// <summary>Green bundles in those stooks.</summary>
        public int BundlesCuring { get; private set; }
        /// <summary>Dry sheaves lying in the operation's piles, not yet collected.</summary>
        public int DrySheaves { get; private set; }
        /// <summary>When the next curing stook is ready, or null when none is curing.</summary>
        public GameTime? NextStookReadyAt { get; private set; }
        /// <summary>"Stooks curing: 2 (13 bundles)".</summary>
        public string StooksText => "Stooks curing: " + StooksCuring + " (" + BundlesCuring + " bundles)";
        /// <summary>"Dry sheaves: 5".</summary>
        public string DryText => "Dry sheaves: " + DrySheaves;
        /// <summary>"Store: 3 / 40".</summary>
        public string StoreText => "Store: " + StoreCount + " / " + StoreCapacity;
        /// <summary>"Next stook ready: 26 Nov", or "No stook curing".</summary>
        public string NextStookText => NextStookReadyAt == null ? "No stook curing"
            : "Next stook ready: " + NextStookReadyAt.Value.DayOfMonth + " " + MonthNames[NextStookReadyAt.Value.Month - 1];
        /// <summary>True when grown grain can be used (MAT-31). Always false for now.</summary>
        public bool GrainSourceAvailable { get; private set; }
        /// <summary>The grown grain label, "Needs farming" while unavailable (MAT-31).</summary>
        public string GrainSourceText { get; private set; }

        private ThatchingReport() { }

        /// <summary>The report for a thatcher's hut footprint, or null when the id is not a thatcher's hut.</summary>
        public static ThatchingReport Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null || fp.Type.Id != ModuleTypes.ThatcherHutId) return null;
            var r = new ThatchingReport
            {
                FootprintId = footprintId,
                IsBuilt = world.IsBuilt(footprintId),
                GrainSourceAvailable = GrownGrainSource.Available(world),
                GrainSourceText = GrownGrainSource.Text(world),
            };
            LedgerLocation store = world.Inventory.FindGoodsStore(footprintId);
            if (store != null)
            {
                r.StoreCount = store.CountOf(Items.Thatch.Id);
                r.StoreCapacity = store.ItemCapOf(Items.Thatch.Id);
            }
            Operation op = world.Operations.FindByModule(footprintId);
            r.HasOperation = op != null;
            if (op != null)
            {
                long? nextTick = null;
                foreach (StookRecord s in world.Curing.ForOperation(op.Id))
                {
                    r.StooksCuring++;
                    r.BundlesCuring += s.Count;
                    if (nextTick == null || s.ReadyTick < nextTick.Value) nextTick = s.ReadyTick;
                }
                if (nextTick.HasValue) r.NextStookReadyAt = world.TimeAt(nextTick.Value);
                r.DrySheaves = SheafCollecting.DryCount(world, op);
            }
            if (!r.IsBuilt) r.StatusText = "Under construction";
            else if (op == null) { r.State = OperationState.NoWorkers; r.StatusText = "No workers"; }
            else { r.State = ThatchingStatus.Of(world, op); r.StatusText = ThatchingStatus.TextOf(world, r.State); }
            return r;
        }
    }
}
