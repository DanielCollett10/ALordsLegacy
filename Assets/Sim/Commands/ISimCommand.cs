using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// A player action expressed as plain data. <see cref="SimWorld.Execute"/> calls
    /// <see cref="Validate"/> first and <see cref="Apply"/> only if validation accepted.
    /// Commands hold ids and integer-mm values only, never references to views.
    /// </summary>
    public interface ISimCommand
    {
        /// <summary>
        /// Checks the command against the current world without changing anything.
        /// Returns <see cref="CommandResult.Rejected"/> with a reason, or an accepted result.
        /// Must not mutate <paramref name="world"/>.
        /// </summary>
        CommandResult Validate(SimWorld world);

        /// <summary>
        /// Changes the world. Only called after <see cref="Validate"/> accepted the same command
        /// against the same world state. Returns the accepted result with created and removed ids.
        /// </summary>
        CommandResult Apply(SimWorld world);
    }
}
