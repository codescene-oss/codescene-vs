namespace Codescene.VSExtension.Core.Models.Ace
{
    /// <summary>
    /// Represents the possible states of ACE.
    /// </summary>
    public enum AceState
    {
        /// <summary>
        /// ACE is initializing (e.g., preflight in progress).
        /// </summary>
        Loading,

        /// <summary>
        /// ACE is enabled and functioning normally.
        /// </summary>
        Enabled,

        /// <summary>
        /// ACE has been disabled by the user.
        /// </summary>
        Disabled,

        /// <summary>
        /// ACE encountered an error.
        /// </summary>
        Error,

        /// <summary>
        /// ACE is offline due to network unavailability.
        /// </summary>
        Offline
    }
}
