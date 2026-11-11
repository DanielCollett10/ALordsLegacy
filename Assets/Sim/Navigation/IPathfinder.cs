namespace LordsLegacy.Sim.Navigation
{
    /// <summary>Finds the fastest walkable route between two ground points.</summary>
    public interface IPathfinder
    {
        /// <summary>Route from <paramref name="from"/> to <paramref name="to"/> (metres) over the nav grid, or NoRoute.</summary>
        PathResult Find(NavGrid nav, PathPoint from, PathPoint to);
    }
}
