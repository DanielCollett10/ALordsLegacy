using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// The two odd-job task types construction needs (CN-20, CN-21, CN-24; decision 0005). Both are helpers-only:
    /// working-age members of families with no workplace take them, never assigned families. Both are heavy, so a
    /// child under 14 cannot take them (PPL-44). Deliver needs an ox; Build needs none.
    /// </summary>
    public static class ConstructionTaskDefs
    {
        /// <summary>Id of the deliver-materials task definition: an ox carries logs from a pile to a site.</summary>
        public const string DeliverId = "task.deliver_materials";

        /// <summary>Id of the build task definition: a worker builds a site.</summary>
        public const string BuildId = "task.build";

        /// <summary>
        /// Deliver up to <paramref name="count"/> logs: fetch the ox, lead it to the pile, load, lead it to the site,
        /// unload and return the ox. The load and unload work is heavy.
        /// </summary>
        /// <param name="pile">Where the logs are picked up.</param>
        /// <param name="pileLocationId">Ledger location of the pile.</param>
        /// <param name="site">Where the logs are dropped (the site's access point).</param>
        /// <param name="siteLocationId">Ledger location of the site's log store.</param>
        /// <param name="count">Logs carried; at least 1.</param>
        /// <param name="loadMinutes">Load work in minutes; at least 1.</param>
        /// <param name="unloadMinutes">Unload work in minutes; at least 1.</param>
        public static TaskDef Deliver(PathPoint pile, int pileLocationId, PathPoint site, int siteLocationId,
            int count, int loadMinutes, int unloadMinutes)
        {
            return new TaskDef(DeliverId, new[]
            {
                TaskStep.FetchOx(),
                TaskStep.LeadTo(pile),
                TaskStep.LoadOx(pileLocationId, count, loadMinutes, heavy: true),
                TaskStep.LeadTo(site),
                TaskStep.UnloadOx(siteLocationId, count, unloadMinutes, heavy: true),
                TaskStep.ReturnOx(),
            }, EligibilityPolicy.HelpersOnly, RequiredResource.Ox);
        }

        /// <summary>Build for <paramref name="minutes"/> minutes of heavy work (CN-24). The caller sizes the work
        /// (the recipe minutes); progress caps (CN-23) are applied by the construction system, not here.
        /// The definition has no walk step yet: <c>MoveTo</c> needs a tree target, and a site has none. The walk to
        /// the site comes with the construction system (a point-walk step).</summary>
        /// <param name="minutes">Work duration in minutes; at least 1.</param>
        public static TaskDef Build(int minutes)
        {
            return new TaskDef(BuildId, new[] { TaskStep.Work(minutes, heavy: true) }, EligibilityPolicy.HelpersOnly);
        }

        /// <summary>The citizen-panel verb for a construction task, given the module it serves. Null when the
        /// definition is not a construction task.</summary>
        public static string VerbFor(string defId, string moduleName)
        {
            bool named = !string.IsNullOrEmpty(moduleName);
            if (defId == DeliverId) return named ? "Delivering logs to " + moduleName : "Delivering logs";
            if (defId == BuildId) return named ? "Building " + moduleName : "Building";
            return null;
        }
    }
}
