namespace Codescene.VSExtension.Core.Application.Services.Util
{
    /// <summary>
    /// Service for checking network connectivity.
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// Returns true if network connectivity is available.
        /// </summary>
        bool IsNetworkAvailable();
    }
}
