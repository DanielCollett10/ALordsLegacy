namespace LordsLegacy.Sim.Core
{
    /// <summary>System lookup for the save sections (T-366).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The first system of type <typeparamref name="T"/> in this world's list, or null (a custom list may omit it).</summary>
        internal T FindSystem<T>() where T : class
        {
            for (int i = 0; i < _systems.Length; i++)
                if (_systems[i] is T t) return t;
            return null;
        }
    }
}
