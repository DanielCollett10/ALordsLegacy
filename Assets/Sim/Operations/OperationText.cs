namespace LordsLegacy.Sim.Operations
{
    /// <summary>Short panel status text for an <see cref="OperationState"/>, shared by the workshop, coppice and forager hut reports (T-566).</summary>
    public static class OperationText
    {
        /// <summary>Status text for a state; <paramref name="input"/> names what is missing for WaitingForInput.</summary>
        public static string Of(OperationState s, string input = "")
        {
            switch (s)
            {
                case OperationState.Working: return "Working";
                case OperationState.Paused: return "Paused";
                case OperationState.MissingHut: return "Missing hut";
                case OperationState.NoWorkers: return "No workers";
                case OperationState.NoBushes: return "No bushes";
                case OperationState.NoStools: return "No stools";
                case OperationState.StorageFull: return "Storage full";
                case OperationState.WaitingForInput: return "Waiting for " + input + "s";
                case OperationState.WaitingForOx: return "Waiting for ox";
                case OperationState.WorkshopStoreFull: return "Store full";
                default: return s.ToString();
            }
        }
    }
}
