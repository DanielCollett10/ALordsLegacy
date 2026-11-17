namespace LordsLegacy.Sim.People
{
    /// <summary>
    /// Whether a family is passing through the settlement (decision 0045, GR-13). <c>None</c> is a settled family.
    /// Saved in the Population section.
    /// </summary>
    public enum FamilyPassage
    {
        /// <summary>A settled family: the normal case.</summary>
        None = 0,

        /// <summary>A new family walking in from the map edge.</summary>
        Arriving = 1,

        /// <summary>A family walking out, after a famine or a departure.</summary>
        Leaving = 2,
    }
}
