namespace LordsLegacy.Sim.People
{
    /// <summary>What a citizen or ox is doing right now, for the UI to show (PPL-14).</summary>
    public enum Activity
    {
        /// <summary>Asleep.</summary>
        Sleeping,
        /// <summary>Idle, wandering near its spot.</summary>
        MillingAbout,
        /// <summary>Moving from one place to another.</summary>
        Walking,
        /// <summary>Doing a task.</summary>
        Working,
        /// <summary>Standing by for something.</summary>
        Waiting
    }

    /// <summary>Biological sex of a citizen.</summary>
    public enum Sex
    {
        /// <summary>Female.</summary>
        Female,
        /// <summary>Male.</summary>
        Male
    }
}
