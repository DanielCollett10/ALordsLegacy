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

    /// <summary>How urgently the dispatcher hands a task out (CN-30). Set at creation; High is taken before Normal.</summary>
    public enum TaskPriority
    {
        /// <summary>The default for every task.</summary>
        Normal = 0,
        /// <summary>Taken before any Normal task, whatever the distance.</summary>
        High = 1
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
        Work,
        /// <summary>Walk to the task's reserved ox and take it by the halter (ox becomes Led). T-289.</summary>
        FetchOx,
        /// <summary>Walk to a point, leading the ox, which follows (MOV-16).</summary>
        LeadTo,
        /// <summary>Work for load minutes, then move logs from a pile or store onto the ox's carrier.</summary>
        LoadOx,
        /// <summary>Work for unload minutes, then move logs from the ox's carrier to a ledger location.</summary>
        UnloadOx,
        /// <summary>Lead the ox to its rest spot and let go.</summary>
        ReturnOx
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
