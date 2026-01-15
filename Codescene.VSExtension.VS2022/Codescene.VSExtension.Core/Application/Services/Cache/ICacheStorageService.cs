using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cache
{
    /// <summary>
    /// Cache storage used by the CLI. Cache locations are sent as parameters
    /// </summary>
    public interface ICacheStorageService
    {
        /// <summary>
        /// Initializes the cache storage, validates and creates necessary directories.
        /// </summary>
        /// <returns></returns>
        Task InitializeAsync();

        /// <summary>
        /// Returns the folder to store the review cache that should be scoped to the current solution.
        /// </summary>
        /// <returns></returns>
        string GetSolutionReviewCacheLocation();

        /// <summary>
        /// Removes old cache entries. Default 30 days
        /// </summary>
        /// <param name="nrOfDays"></param>
        /// <returns></returns>
        void RemoveOldReviewCacheEntries(int nrOfDays = 30);
    }
}
