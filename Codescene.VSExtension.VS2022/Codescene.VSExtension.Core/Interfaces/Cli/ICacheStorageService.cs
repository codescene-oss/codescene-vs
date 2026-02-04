// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    /// <summary>
    /// Cache storage used by the CLI. Cache locations are sent as parameters.
    /// </summary>
    public interface ICacheStorageService
    {
        /// <summary>
        /// Initializes the cache storage, validates and creates necessary directories.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Returns the folder to store the review cache that should be scoped to the current solution.
        /// </summary>
        /// <returns>location of cache.</returns>
        string GetSolutionReviewCacheLocation();

        /// <summary>
        /// Removes old cache entries. Default 30 days.
        /// </summary>
        /// <param name="nrOfDays">How many days old the cache files should be before deleting.</param>
        void RemoveOldReviewCacheEntries(int nrOfDays = 30);
    }
}
