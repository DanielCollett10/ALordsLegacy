namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// One subsystem's part of a save (decision 0034). <see cref="SimSave"/> wraps <see cref="Write"/> in a section
    /// (id + byte length) and hands <see cref="Read"/> a reader over just that body.
    /// </summary>
    public interface ISaveSection
    {
        /// <summary>Unique section id in the save file.</summary>
        int Id { get; }

        /// <summary>Writes this section's body for <paramref name="world"/>.</summary>
        void Write(LordsLegacy.Sim.Core.SimWorld world, SaveWriter writer);

        /// <summary>Reads this section's body into the freshly restored <paramref name="world"/>.</summary>
        void Read(LordsLegacy.Sim.Core.SimWorld world, SaveReader reader);
    }
}
