using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Coppice
{
    /// <summary>
    /// Puts the rods of a coppice task that ended mid-carry on the ground (MAT-17): an operation pile at the stool while
    /// cutting, at the hut once the cutter is walking home.
    /// </summary>
    public static class CoppiceDrop
    {
        /// <summary>Drops everything the task's carrier holds. <paramref name="stand"/> is unused when the stool or hut is known.</summary>
        public static void DropCarried(SimWorld world, TaskRecord t, PointMm? stand)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null || carrier.Total == 0) return;
            PointMm at = stand ?? default;
            bool home = t.CurrentStep > 0 && t.CurrentStep < t.Def.Steps.Count && t.Def.Steps[t.CurrentStep].Kind != StepKind.Work;
            Footprint hut = null;
            Operation op = world.Operations.Get(t.OwnerOperationId);
            if (home && op != null) hut = OperationStatus.Hut(world, op);
            if (hut != null) at = hut.Centre;
            else if (t.BushId.HasValue && world.Bushes.TryGet(t.BushId.Value, out BushRecord b)) at = PointMm.FromMetres(b.X, b.Z);
            LedgerLocation pile = world.Inventory.GetOrAddGroundPile(at, t.OwnerOperationId);
            foreach (ItemDef item in Items.All)
            {
                int n = carrier.CountOf(item.Id);
                if (n > 0) world.Inventory.Transfer(carrier, pile, item, n);
            }
        }
    }
}
