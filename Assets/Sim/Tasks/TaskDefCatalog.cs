using System.Collections.Generic;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Looks up the fixed part of a task definition by its id (decision 0034): who may take it and what extra
    /// resource it needs. A save stores a task's def id and steps (their numbers depend on the pile, the load and the
    /// tree), and the load rebuilds the <see cref="TaskDef"/> from this catalog, so the policy is never saved.
    /// </summary>
    public static class TaskDefCatalog
    {
        private static readonly Dictionary<string, (EligibilityPolicy, RequiredResource)> Known =
            new Dictionary<string, (EligibilityPolicy, RequiredResource)>
            {
                { WoodcuttingSystem.FellTaskId, (EligibilityPolicy.AssignedFamilyOnly, RequiredResource.None) },
                { HaulSystem.HaulTaskId, (EligibilityPolicy.AssignedFamilyOnly, RequiredResource.Ox) },
                { ConstructionTaskDefs.DeliverId, (EligibilityPolicy.HelpersOnly, RequiredResource.Ox) },
                { ConstructionTaskDefs.BuildId, (EligibilityPolicy.HelpersOnly, RequiredResource.None) },
            };

        /// <summary>The policy and resource of a known task def id; false for an unknown id.</summary>
        public static bool TryGet(string defId, out EligibilityPolicy eligibility, out RequiredResource required)
        {
            if (defId != null && Known.TryGetValue(defId, out var v)) { eligibility = v.Item1; required = v.Item2; return true; }
            eligibility = EligibilityPolicy.AssignedFamilyOnly; required = RequiredResource.None;
            return false;
        }

        /// <summary>Builds a def from its id and steps. Null when the id is unknown.</summary>
        public static TaskDef Build(string defId, IEnumerable<TaskStep> steps) =>
            TryGet(defId, out var e, out var r) ? new TaskDef(defId, steps, e, r) : null;
    }
}
