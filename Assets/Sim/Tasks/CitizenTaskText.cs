using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Plain-text description of what a citizen does for work, for the citizen panel (PPL-14, T-283). Read-only: it
    /// never changes state. Examples: "Felling (Buck 2/4)", "Walking to work", "Paused for the night", "Milling about".
    /// </summary>
    public static class CitizenTaskText
    {
        /// <summary>Task verb for the woodcutting fell task.</summary>
        private const string FellVerb = "Felling";

        /// <summary>
        /// What the citizen is doing now. A worker with an unfinished task shows the task: its verb, and the stage
        /// progress when the stage runs more than once (e.g. "Felling (Buck 2/4)"). Otherwise the activity is shown.
        /// </summary>
        public static string Task(SimWorld world, Citizen c)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (c == null) return "";
            List<TaskRecord> live = world.Tasks.ByAssignee(c.Id);
            if (live.Count == 0) return ActivityText(c.Activity);
            TaskRecord t = live[0];
            if (t.Def.Id == Food.FoodTripSystem.FetchFoodTaskId) return "Fetching food";

            if (t.Def.Id == Markets.StoreFamilySystem.StockStallTaskId) return StockTripText(world, t);

            if (!world.WorkDay.OnShift(world, c))
                return world.WorkDay.IsWorkingDaylight(world) ? "Walking to work" : "Paused for the night";
            if (t.State != TaskState.Active || c.Activity == Activity.Walking) return "Walking to work";

            TaskStep step = t.Def.Steps[t.CurrentStep];
            if (step.Kind == StepKind.MoveTo) return "Walking to work";

            string verb = VerbOf(t);
            int total = CountStage(t.Def, step.StageId);
            if (step.StageId == null || total <= 1) return verb;
            int done = CountStageBefore(t.Def, t.CurrentStep, step.StageId);
            return verb + " (" + StageLabel(step.StageId) + " " + (done + 1) + "/" + total + ")";
        }

        /// <summary>The citizen's workplace, e.g. "Woodcutter 1", or "none" when the family has no operation.</summary>
        public static string Workplace(SimWorld world, Citizen c)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (c == null) return "none";
            Family f = world.People.FindFamily(c.FamilyId);
            if (f == null || !f.WorkplaceId.HasValue) return "none";
            Operation op = world.Operations.Get(f.WorkplaceId.Value);
            if (op == null) return "none";
            return OperationName(op.TypeId) + " " + op.Id;
        }

        /// <summary>Text for an activity with no task behind it.</summary>
        public static string ActivityText(Activity a)
        {
            switch (a)
            {
                case Activity.Sleeping: return "Asleep";
                case Activity.MillingAbout: return "Milling about";
                case Activity.Walking: return "Walking";
                case Activity.Working: return "Working";
                case Activity.Waiting: return "Waiting";
                default: return a.ToString();
            }
        }

        // MK-42: a trip that ends on a stall carries food to it; one that ends in a granary is collecting food for it.
        private static string StockTripText(SimWorld world, TaskRecord t)
        {
            foreach (TaskStep s in t.Def.Steps)
            {
                if (s.Kind != StepKind.DepositItems) continue;
                Inventory.LedgerLocation to = world.Inventory.Get(s.LocationId);
                Footprints.Footprint fp = to == null ? null : world.Footprints.Get(to.ModuleId);
                return fp != null && fp.Type.Id == Footprints.ModuleTypes.MarketStallId ? "Carrying food to stall" : "Collecting food";
            }
            return "Collecting food";
        }

        private static string VerbOf(TaskRecord t)
        {
            if (t.Def.Id == WoodcuttingSystem.FellTaskId) return FellVerb;
            if (t.Def.Id == Foraging.ForagingSystem.ForageTaskId) return "Foraging";
            if (t.Def.Id == Coppice.CoppiceSystem.CoppiceTaskId) return "Cutting rods";
            if (t.Def.Id == Thatch.ThatchingSystem.CutReedTaskId) return "Cutting reed";
            if (t.Def.Id == Thatch.SheafCollecting.CollectTaskId) return "Collecting thatch";
            if (t.Def.Id == HandHaulTaskDefs.HaulId) return "Hauling goods";
            return ConstructionTaskDefs.VerbFor(t.Def.Id, Footprints.ModuleTypes.DisplayName(t.Subject)) ?? "Working";
        }

        private static string OperationName(string typeId)
        {
            if (typeId == OperationTypes.ForagingId) return "Foraging camp";
            if (typeId == OperationTypes.CoppiceId) return "Coppice hut";
            if (typeId == OperationTypes.ThatchingId) return "Thatcher's hut";
            if (typeId == OperationTypes.GranaryId) return "Granary";
            return typeId == OperationTypes.WoodcuttingId ? "Woodcutter" : "Operation";
        }

        /// <summary>"stage.buck" becomes "Buck".</summary>
        private static string StageLabel(string stageId)
        {
            string s = stageId.StartsWith("stage.") ? stageId.Substring("stage.".Length) : stageId;
            return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static int CountStage(TaskDef def, string stageId)
        {
            if (stageId == null) return 0;
            int n = 0;
            for (int i = 0; i < def.Steps.Count; i++) if (def.Steps[i].StageId == stageId) n++;
            return n;
        }

        private static int CountStageBefore(TaskDef def, int index, string stageId)
        {
            int n = 0;
            for (int i = 0; i < index && i < def.Steps.Count; i++) if (def.Steps[i].StageId == stageId) n++;
            return n;
        }
    }
}
