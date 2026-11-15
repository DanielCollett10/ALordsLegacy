using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>Work total and completion of a deconstruction (T-394, CN-53..55).</summary>
    public static class DeconstructionWork
    {
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

            foreach (Operation op in new List<Operation>(world.Operations.All))
                foreach (int id in new List<int>(op.ModuleIds))
                    if (id == footprintId) world.Operations.RemoveModule(op.Id, footprintId);
            world.Footprints.Remove(footprintId);
            world.Deconstruction.Remove(footprintId);
        }
    }
}
