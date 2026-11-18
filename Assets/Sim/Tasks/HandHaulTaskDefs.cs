using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// The one store-to-store hand-carry task (ST-43, ST-44): walk to the source, load, walk to the destination, unload.
    /// Every yard, workshop and market hand trip reuses it; <c>task.carry_materials</c> stays the site delivery.
    /// </summary>
    public static class HandHaulTaskDefs
    {
        /// <summary>Id of the hand-haul task definition.</summary>
        public const string HaulId = "task.haul_by_hand";
        /// <summary>Stage id of the load step.</summary>
        public const string LoadStageId = "stage.haul_load";
        /// <summary>Stage id of the unload step.</summary>
        public const string UnloadStageId = "stage.haul_unload";

        /// <summary>Ledger reservation owner of a haul task: negative, so it never clashes with other owners.</summary>
        public static int OwnerFor(int taskId) => -2_000_000 - taskId;

        /// <summary>Builds the def: walk, load, collect, walk, unload, deposit. Load and unload take
        /// <see cref="StoreFamilySystem.LoadMinutes"/> minutes each.</summary>
        public static TaskDef Build(SimWorld world, LedgerLocation source, LedgerLocation destination, ItemDef item, int count)
        {
            var steps = new List<TaskStep>
            {
                TaskStep.WalkTo(PointOf(world, source)),
                TaskStep.Work(StoreFamilySystem.LoadMinutes, false, LoadStageId),
                TaskStep.CollectItems(source.Id, item.Id, count),
                TaskStep.WalkTo(PointOf(world, destination)),
                TaskStep.Work(StoreFamilySystem.LoadMinutes, false, UnloadStageId),
                TaskStep.DepositItems(destination.Id, item.Id, count),
            };
            return new TaskDef(HaulId, steps, EligibilityPolicy.AssignedFamilyOnly);
        }

        // A building is reached at its access point; a pile or camp store at its own spot.
        private static PathPoint PointOf(SimWorld world, LedgerLocation l)
        {
            Footprint f = l.ModuleId != 0 ? world.Footprints.Get(l.ModuleId) : null;
            PointMm p = f != null ? f.AccessPoint : l.Position;
            return new PathPoint(p.XMetres, p.ZMetres);
        }
    }
}
