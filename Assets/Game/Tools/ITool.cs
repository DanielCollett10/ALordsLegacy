namespace LordsLegacy.Game.Tools
{
    /// <summary>A mouse tool picked from the build bar. Only one is active at a time (ui.md UIB-03).</summary>
    public interface ITool
    {
        /// <summary>Id matching a build bar tool entry.</summary>
        string Id { get; }

        /// <summary>True while the tool holds an unconfirmed draft.</summary>
        bool HasDraft { get; }

        /// <summary>Called when the tool becomes the active tool.</summary>
        void Activate();

        /// <summary>Called when the tool stops being the active tool.</summary>
        void Deactivate();

        /// <summary>Discards any unconfirmed draft.</summary>
        void Cancel();

        /// <summary>Offers Escape to the tool. Returns true if it consumed the key (e.g. cancelled a draft).</summary>
        bool HandleEscape();
    }
}
