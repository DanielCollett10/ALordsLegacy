namespace LordsLegacy.Sim.Tasks
{
    /// <summary>Who may take a task (decision 0005). The dispatcher reads this; it never special-cases task types.</summary>
    public enum EligibilityPolicy
    {
        /// <summary>Only the owner operation's assigned family.</summary>
        AssignedFamilyOnly,
        /// <summary>The assigned family first, then helpers when the family cannot.</summary>
        AssignedThenHelpers,
        /// <summary>Helpers only.</summary>
        HelpersOnly
    }

    /// <summary>A resource a task needs besides the worker.</summary>
    public enum RequiredResource
    {
        /// <summary>Nothing extra.</summary>
        None,
        /// <summary>An ox.</summary>
        Ox
    }

    /// <summary>The kind of a task step. A small fixed set; more arrive in M9.</summary>
    public enum StepKind
    {
        /// <summary>Walk to the task target.</summary>
        MoveTo,
        /// <summary>Work in place for a number of ticks.</summary>
        Work
    }

    /// <summary>Lifecycle of a task: Open, Assigned, Active, Done, or Cancelled from any live state.</summary>
    public enum TaskState
    {
        /// <summary>Waiting for a worker.</summary>
        Open,
        /// <summary>A worker has been chosen but has not started.</summary>
        Assigned,
        /// <summary>The worker is running the steps.</summary>
        Active,
        /// <summary>All steps finished.</summary>
        Done,
        /// <summary>Abandoned; reservations released.</summary>
        Cancelled
    }
}
