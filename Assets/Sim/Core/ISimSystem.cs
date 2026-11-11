using LordsLegacy.Sim.Commands;

namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// A unit of simulation logic that <see cref="SimWorld"/> runs in the fixed order defined by
    /// <see cref="SimSystems"/>. Systems hold no static state: anything they keep lives on the world
    /// (in a <c>SimWorld.&lt;Area&gt;.cs</c> partial) or on the system instance owned by one world.
    /// </summary>
    public interface ISimSystem
    {
        /// <summary>
        /// Runs once per tick, after <see cref="SimWorld.Tick"/> has been advanced to the tick being simulated.
        /// Never called while the world is paused.
        /// </summary>
        /// <param name="world">The world being simulated. Never null.</param>
        void Step(SimWorld world);

        /// <summary>
        /// Called after a command was accepted and applied by <see cref="SimWorld.Execute"/>, in system order.
        /// Not called for rejected commands. Runs even while paused, because commands work while paused.
        /// </summary>
        /// <param name="world">The world the command was applied to. Never null.</param>
        /// <param name="result">The accepted result of the command. Never null.</param>
        void OnCommandApplied(SimWorld world, CommandResult result);
    }
}
