using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// A task type as data (decision 0005): id, ordered steps, who may take it, and any required resource.
    /// There is no ProcessDef type yet; steps are an explicit list.
    /// </summary>
    public sealed class TaskDef
    {
        private readonly TaskStep[] _steps;

        /// <summary>Stable id, e.g. "task.fell_tree".</summary>
        public string Id { get; }
        /// <summary>The steps in order. Never empty.</summary>
        public IReadOnlyList<TaskStep> Steps => _steps;
        /// <summary>Who may take the task.</summary>
        public EligibilityPolicy Eligibility { get; }
        /// <summary>Extra resource required.</summary>
        public RequiredResource Required { get; }

        /// <summary>Builds a definition. Throws on an empty id or no steps.</summary>
        public TaskDef(string id, IEnumerable<TaskStep> steps, EligibilityPolicy eligibility,
            RequiredResource required = RequiredResource.None)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Task id required", nameof(id));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps = new List<TaskStep>(steps).ToArray();
            if (_steps.Length == 0) throw new ArgumentException("A task needs at least one step", nameof(steps));
            Id = id; Eligibility = eligibility; Required = required;
        }
    }
}
