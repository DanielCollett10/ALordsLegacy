namespace LordsLegacy.Game
{
    /// <summary>
    /// Hand-over of a chosen save to the next Game scene (decision 0034, load by scene reload). The Load panel sets it just
    /// before reloading the scene; <see cref="SimHost"/> takes it in Start and clears it at once, so a later reload starts a
    /// new game. A Game-layer static on purpose: it must survive the scene reload.
    /// </summary>
    public static class PendingLoad
    {
        /// <summary>The save bytes waiting to be loaded, or null.</summary>
        public static byte[] Bytes { get; private set; }

        /// <summary>True when a save is waiting.</summary>
        public static bool HasPending => Bytes != null;

        /// <summary>Stores save bytes for the next scene load.</summary>
        public static void Set(byte[] bytes) { Bytes = bytes; }

        /// <summary>Returns the waiting bytes (or null) and clears the slot.</summary>
        public static byte[] Take()
        {
            var b = Bytes;
            Bytes = null;
            return b;
        }

        /// <summary>Clears the slot.</summary>
        public static void Clear() { Bytes = null; }
    }
}
