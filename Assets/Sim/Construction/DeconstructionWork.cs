using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>Work total and completion of a deconstruction (T-394, CN-53..55).</summary>
    public static class DeconstructionWork
    {
        /// <summary>
        /// Owner id of the deconstruct tasks of a house. Houses have no operation (their plot is the area they stand on), so
        /// their tasks carry this id. It only matters for assigned-family eligibility, and these tasks are helpers-only.
        /// </summary>
        public const int HouseOwnerId = -1;

        /// <summary>Worker-minutes the deconstruction takes: recipe minutes × <c>DeconstructTimeShare</c>, rounded down (hut 480).</summary>
        public static int TotalMinutes(SimWorld world, Footprint fp) =>
            (int)Math.Floor(fp.Type.ConstructionRecipe.BuildWorkerMinutes * world.Tuning.DeconstructTimeShare + 1e-9);

        /// <summary>Logs returned: recipe logs × <c>DeconstructRefundShare</c>, rounded down (hut 4, oxen stand 0).</summary>
        public static int RefundLogs(SimWorld world, Footprint fp) =>
            (int)Math.Floor(fp.Type.ConstructionRecipe.Logs * world.Tuning.DeconstructRefundShare + 1e-9);

        /// <summary>Items returned: each recipe item count × <c>DeconstructRefundShare</c>, rounded down; zero counts are left out.</summary>
        public static SortedDictionary<string, int> Refunds(SimWorld world, Footprint fp)
        {
            var r = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> kv in fp.Type.ConstructionRecipe.Items)
            {
                int n = (int)Math.Floor(kv.Value * world.Tuning.DeconstructRefundShare + 1e-9);
                if (n > 0) r[kv.Key] = n;
            }
            return r;
        }

        /// <summary>Worker-minutes still to do, never negative.</summary>
        public static int Remaining(SimWorld world, DeconstructionEntry e, Footprint fp) =>
            Math.Max(0, TotalMinutes(world, fp) - e.ProgressMinutes);

        /// <summary>
        /// Takes the module down: its refund logs and any storage contents become one unowned ground pile at the site
        /// (CN-54, CN-55), live deconstruct tasks of it are cancelled, and the footprint and entry are removed.
        /// </summary>
        public static void Finish(SimWorld world, int footprintId)
        {
            Footprint fp = world.Footprints.Get(footprintId);
            DeconstructionEntry e = world.Deconstruction.Get(footprintId);
            if (fp == null || e == null) return;
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
                if (!t.IsFinished && t.Def.Id == ConstructionTaskDefs.DeconstructId && t.SiteFootprintId == footprintId)
                    world.Tasks.Cancel(t.Id);

            Ledger inv = world.Inventory;
            SortedDictionary<string, int> refunds = Refunds(world, fp);
            LedgerLocation storage = inv.FindStorage(footprintId);
            if (storage == null) storage = inv.FindFoodStore(footprintId); // a granary's food store (CN-55)
            var sources = new List<LedgerLocation>();
            if (storage != null) sources.Add(storage);
            List<LedgerLocation> wStocks = inv.WorkshopStocks(footprintId); // a workshop's input stocks and output store (HW-01, PL-01)
            LedgerLocation wStore = inv.FindGoodsStore(footprintId);
            sources.AddRange(wStocks);
            if (wStore != null) sources.Add(wStore);
            bool hasContents = false;
            foreach (LedgerLocation s in sources) if (s.Total > 0) hasContents = true;
            if (refunds.Count > 0 || hasContents)
            {
                LedgerLocation pile = inv.GetOrAddGroundPile(fp.Type.Id == ModuleTypes.StorageYardId ? fp.AccessPoint : fp.Centre, null);
                foreach (LedgerLocation src in sources)
                    foreach (KeyValuePair<string, int> kv in new List<KeyValuePair<string, int>>(src.Counts))
                    {
                        int n = src.AvailableOf(kv.Key);
                        if (n > 0) inv.Transfer(src, pile, Items.Find(kv.Key), n);
                    }
                foreach (KeyValuePair<string, int> kv in refunds) inv.Produce(pile, Items.Find(kv.Key), kv.Value);
            }
            foreach (LedgerLocation s in wStocks) inv.RemoveEmptyLocation(s.Id);
            if (wStore != null) inv.RemoveEmptyLocation(wStore.Id);

            var owners = new List<int>();
            foreach (Operation op in new List<Operation>(world.Operations.All))
                foreach (int id in new List<int>(op.ModuleIds))
                    if (id == footprintId) { world.Operations.RemoveModule(op.Id, footprintId); owners.Add(op.Id); }
            world.KitClaims.Release(footprintId); // decision 0052: the building's kit claim goes with it
            world.Footprints.Remove(footprintId);
            HouseRemoval.RemoveWithPlot(world, footprintId); // HM-14b: a house takes its plot with it (no-op otherwise)
            world.Deconstruction.Remove(footprintId);
            foreach (int opId in owners) CompletePendingOperation(world, opId);
            RemoveGranaryOperation(world, owners);
        }

        /// <summary>
        /// Frees the families of each listed area-less granary operation that has no modules left (WorkplaceId null,
        /// tasks cancelled like UnassignFamily) and removes the operation (T-447).
        /// </summary>
        public static void RemoveGranaryOperation(SimWorld world, IEnumerable<int> operationIds)
        {
            foreach (int opId in operationIds)
            {
                Operation op = world.Operations.Get(opId);
                if (op == null || (op.TypeId != OperationTypes.GranaryId && op.TypeId != OperationTypes.StorageYardOpId && OperationTypes.WorkshopOf(op.TypeId, world.Tuning) == null) || op.ModuleIds.Count > 0) continue;
                foreach (int fid in new List<int>(op.FamilyIds)) new Commands.UnassignFamily(op.Id, fid).Apply(world);
                world.Operations.Remove(op.Id);
            }
        }

        /// <summary>
        /// If the operation is marked for removal and has no modules left, removes its area and the operation (CN-58).
        /// Returns true when it did.
        /// </summary>
        public static bool CompletePendingOperation(SimWorld world, int operationId)
        {
            if (!world.Deconstruction.IsOperationPending(operationId)) return false;
            Operation op = world.Operations.Get(operationId);
            if (op == null) { world.Deconstruction.RemovePendingOperation(operationId); return false; }
            if (op.ModuleIds.Count > 0) return false;
            foreach (Operation linked in new List<Operation>(world.Operations.All))
                if (linked.WorkAreaId == op.AreaId) world.Operations.SetWorkArea(linked.Id, null);
            world.Areas.Remove(op.AreaId);
            world.Operations.Remove(op.Id);
            world.Deconstruction.RemovePendingOperation(operationId);
            return true;
        }
    }
}
