using System.Collections.Generic;

namespace LordsLegacy.Sim.Processes
{
    /// <summary>
    /// The process definitions, built in code until the definition pipeline exists (decision 0019).
    /// Values from woodcutting.md "Timber chain timing" and tuning.md (TMB-01). Placeholders until tuned.
    /// </summary>
    public static class ProcessDefs
    {
        /// <summary>Id of the timber chain.</summary>
        public const string TimberId = "process.timber";
        /// <summary>Id of the fell stage.</summary>
        public const string FellStageId = "stage.fell";
        /// <summary>Id of the delimb stage (disabled by default).</summary>
        public const string DelimbStageId = "stage.delimb";
        /// <summary>Id of the buck (cut to length) stage, per log.</summary>
        public const string BuckStageId = "stage.buck";

        /// <summary>
        /// Timber: fell 90 min, delimb 30 min (off, so its time goes to buck), buck 15 min per log.
        /// Heavy: fell and buck. Delimb is not on the POP-A3 heavy list yet, so it is marked light.
        /// </summary>
        public static readonly ProcessDef Timber = new ProcessDef(TimberId, new[]
        {
            new StageDef(FellStageId, 90, false, true, true, "fell"),
            new StageDef(DelimbStageId, 30, false, false, false, "delimb"),
            new StageDef(BuckStageId, 15, true, true, true, "buck"),
        });

        /// <summary>All process definitions.</summary>
        public static readonly IReadOnlyList<ProcessDef> All = new[] { Timber };

        /// <summary>Finds a process by id, or null if unknown.</summary>
        public static ProcessDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
