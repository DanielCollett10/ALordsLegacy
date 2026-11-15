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
            int refund = RefundLogs(world, fp);
            LedgerLocation storage = inv.FindStorage(footprintId);
            bool hasContents = storage != null && storage.Total > 0;
            if (refund > 0 || hasContents)
            {
                LedgerLocation pile = inv.GetOrAddGroundPile(fp.Centre, null);
                if (hasContents)
                    foreach (KeyValuePair<string, int> kv in new List<KeyValuePair<string, int>>(storage.Counts))
                    {
                        int n = storage.AvailableOf(kv.Key);
                        if (n > 0) inv.Transfer(storage, pile, Items.Find(kv.Key), n);
                    }
                if (refund > 0) inv.Produce(pile, Items.Log, refund);
            }

            var owners = new List<int>();
            foreach (Operation op in new List<Operation>(world.Operations.All))
                foreach (int id in new List<int>(op.ModuleIds))
                    if (id == footprintId) { world.Operations.RemoveModule(op.Id, footprintId); owners.Add(op.Id); }
            world.Footprints.Remove(footprintId);
            HouseRemoval.RemoveWithPlot(world, footprintId); // HM-14b: a house takes its plot with it (no-op otherwise)
            world.Deconstruction.Remove(footprintId);
            foreach (int opId in owners) CompletePendingOperation(world, opId);
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
