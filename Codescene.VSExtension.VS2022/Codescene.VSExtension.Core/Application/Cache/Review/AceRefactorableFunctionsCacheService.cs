using Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class AceRefactorableFunctionsCacheService : CacheService<
        AceRefactorableFunctionsQuery, 
        AceRefactorableFunctionsEntry, 
        AceRefactorableFunctionsItem, 
        IList<FnToRefactorModel>>
    {
        public override IList<FnToRefactorModel> Get(AceRefactorableFunctionsQuery query)
        {
            string filePath = query.FilePath;
            string fileContents = query.FileContents;
            string contentHash = Hash(fileContents);

            if (Cache.TryGetValue(filePath, out var cachedItem))
            {
                if (cachedItem.FileContentsHash == contentHash)
                {
                    return cachedItem.Result;
                }
            }

            return new List<FnToRefactorModel>();
        }

        public override void Put(AceRefactorableFunctionsEntry entry)
        {
            string filePath = entry.FilePath;
            string fileContents = entry.FileContents;
            string contentHash = Hash(fileContents);

            Cache[filePath] = new AceRefactorableFunctionsItem(contentHash, entry.Result);
        }
    }
}
