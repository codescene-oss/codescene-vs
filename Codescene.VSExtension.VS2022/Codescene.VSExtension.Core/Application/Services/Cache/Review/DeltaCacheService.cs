using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review
{
    public class DeltaCacheService : CacheService<DeltaCacheQuery, DeltaCacheEntry, DeltaCacheItem, (bool, DeltaResponseModel)>
    {
        /// <summary>
        /// Retrieves a cached delta response for the given query.
        ///
        /// This method distinguishes between:
        /// - A cache <b>hit</b>, where the file path and content hashes match
        ///   a stored entry. It returns the cached <see cref="DeltaResponseModel"/>,
        ///   which may be <c>null</c> if the API originally returned <c>null</c>.
        /// - A cache <b>miss</b> or <b>stale</b> entry, where no match is found
        ///   or content has changed. This is indicated by <c>false</c> in the tuple.
        /// </summary>
        public override (bool, DeltaResponseModel) Get(DeltaCacheQuery query)
        {
            var oldHash = Hash(query.BaselineContent);
            var newHash = Hash(query.CurrentContent);

            if (!Cache.TryGetValue(query.FilePath, out var entry))
            {
                return (false, null);
            }

            var contentsMatch = entry.HeadHash == oldHash && entry.CurrentHash == newHash;
            var isCacheHitOrNotStale = contentsMatch;

            return (isCacheHitOrNotStale, entry.Delta);
        }

        /// <summary>
        /// Adds or updates the delta cache for the given entry.
        ///
        /// The key is the file path, and the cache stores hashes of both the
        /// head and current content. These hashes are later used to check staleness.
        /// </summary>
        public override void Put(DeltaCacheEntry entry)
        {
            var headHash = Hash(entry.BaselineContent);
            var currentContentHash = Hash(entry.CurrentFileContent);

            Cache[entry.FilePath] = new DeltaCacheItem(headHash, currentContentHash, entry.Delta);
        }
    }
}