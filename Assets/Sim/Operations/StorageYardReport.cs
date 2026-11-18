using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// The yard panel status (ST-45), computed each time and never stored. The first cause that applies wins, in this order:
    /// UnderConstruction, NoWorkers, WaitingForOx, MovingCampGoods, Collecting, YardFull, Idle.
    /// </summary>
    public enum StorageYardStatus { UnderConstruction = 0, NoWorkers = 1, WaitingForOx = 2, MovingCampGoods = 3, Collecting = 4, YardFull = 5, Idle = 6 }

    /// <summary>Everything the storage yard panel shows, read from the Sim in one go (T-547, pattern of GranaryReport). Immutable.</summary>
    public sealed class StorageYardReport
    {
        /// <summary>The yard footprint id.</summary>
        public int FootprintId { get; private set; }
        /// <summary>The yard operation id, or 0 when the yard has none.</summary>
        public int OperationId { get; private set; }
        /// <summary>True once the yard is built.</summary>
        public bool IsBuilt { get; private set; }
        /// <summary>The computed status (ST-45).</summary>
        public StorageYardStatus Status { get; private set; }
        /// <summary>The status as panel text, e.g. "Moving camp goods".</summary>
        public string StatusText { get; private set; }
        /// <summary>Every General item in catalogue order with the count in this yard (zeros included).</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Stock { get; private set; }
        /// <summary>General items in the yard, reserved ones included.</summary>
        public int Total { get; private set; }
        /// <summary>Yard capacity (one total across all items, ST-08).</summary>
        public int Capacity { get; private set; }
        /// <summary>Ids of the families assigned to the yard, in assignment order.</summary>
        public IReadOnlyList<int> FamilyIds { get; private set; }
        /// <summary>Surnames of the assigned families joined with ", ", or "None".</summary>
        public string FamilyText { get; private set; }
        /// <summary>
        /// True when the yard supplies the market. Always false for now: no household buys General goods yet (ST-10,
        /// Level 1 households need only food). Derived from demand, never stored.
        /// </summary>
        public bool SuppliesMarketAvailable { get; private set; }
        /// <summary>"No demand yet" while <see cref="SuppliesMarketAvailable"/> is false, else "Supplies market".</summary>
        public string SuppliesMarketText => SuppliesMarketAvailable ? "Supplies market" : "No demand yet";
        /// <summary>Every General item in the village, in catalogue order (the T-525 totals, <see cref="GoodsStats.GeneralByItem"/>).</summary>
        public IReadOnlyList<KeyValuePair<string, int>> VillageStock { get; private set; }

        private StorageYardReport() { }

        /// <summary>The report for a storage yard footprint, or null when the id is not a storage yard.</summary>
        public static StorageYardReport Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null || fp.Type.Id != ModuleTypes.StorageYardId) return null;

            var r = new StorageYardReport { FootprintId = footprintId, IsBuilt = world.IsBuilt(footprintId) };
            LedgerLocation store = world.Inventory.FindGoodsStore(footprintId);
            var stock = new List<KeyValuePair<string, int>>();
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.General)
                    stock.Add(new KeyValuePair<string, int>(item.Id, store == null ? 0 : store.CountOf(item.Id)));
            r.Stock = stock;
            if (store != null) { r.Total = store.Total; r.Capacity = store.Capacity; }
            r.VillageStock = GoodsStats.GeneralByItem(world);
            r.SuppliesMarketAvailable = false;

            Operation op = world.Operations.FindByModule(footprintId);
            var families = new List<int>();
            var names = new List<string>();
            if (op != null)
            {
                r.OperationId = op.Id;
                foreach (int fid in op.FamilyIds)
                {
                    families.Add(fid);
                    Family f = world.People.FindFamily(fid);
                    names.Add(f != null ? f.Surname : "Family " + fid);
                }
            }
            r.FamilyIds = families;
            r.FamilyText = names.Count == 0 ? "None" : string.Join(", ", names);
            r.Status = StatusOf(world, footprintId, op, store);
            r.StatusText = TextOf(r.Status);
            return r;
        }

        /// <summary>Panel text for a status.</summary>
        public static string TextOf(StorageYardStatus s)
        {
            switch (s)
            {
                case StorageYardStatus.UnderConstruction: return "Under construction";
                case StorageYardStatus.NoWorkers: return "No workers";
                case StorageYardStatus.WaitingForOx: return "Waiting for ox";
                case StorageYardStatus.MovingCampGoods: return "Moving camp goods";
                case StorageYardStatus.Collecting: return "Collecting";
                case StorageYardStatus.YardFull: return "Yard full";
                default: return "Idle";
            }
        }

        private static StorageYardStatus StatusOf(SimWorld world, int footprintId, Operation op, LedgerLocation store)
        {
            if (!world.IsBuilt(footprintId)) return StorageYardStatus.UnderConstruction;
            if (op == null || !OperationStatus.HasWorker(world, op)) return StorageYardStatus.NoWorkers;
            if (OperationStatus.IsWaitingForOx(world, op) || YardBeamSource.WaitingForOx(world, op)) return StorageYardStatus.WaitingForOx;
            LedgerLocation camp = world.Camp?.GeneralStoreLocation;
            bool campTrip = false, otherTrip = false;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
            {
                if (t.IsFinished || (t.Def.Id != HandHaulTaskDefs.HaulId && t.Def.Id != WorkshopSystem.FetchTaskId)) continue;
                int source = SourceLocationOf(t);
                if (camp != null && source == camp.Id) campTrip = true; else otherTrip = true;
            }
            if (campTrip) return StorageYardStatus.MovingCampGoods;
            if (otherTrip) return StorageYardStatus.Collecting;
            if (store != null && store.FreeSpace <= 0) return StorageYardStatus.YardFull;
            return StorageYardStatus.Idle;
        }

        // The location a haul or ox fetch takes goods from: the first collect step (hand) or load step (ox).
        private static int SourceLocationOf(TaskRecord t)
        {
            foreach (TaskStep s in t.Def.Steps)
                if (s.Kind == StepKind.CollectItems || s.Kind == StepKind.LoadOx) return s.LocationId;
            return 0;
        }

        /// <summary>Count of one General item in <see cref="Stock"/> (0 when not listed).</summary>
        public int StockOf(string itemId)
        {
            foreach (KeyValuePair<string, int> kv in Stock) if (kv.Key == itemId) return kv.Value;
            return 0;
        }
    }
}
