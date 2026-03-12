// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Application.Cache.Review
{
    public class AceRefactorableFunctionsCacheService : CacheService<
        AceRefactorableFunctionsQuery,
        AceRefactorableFunctionsEntry,
        AceRefactorableFunctionsItem,
        IList<FnToRefactorModel>>
    {
        public AceRefactorableFunctionsCacheService()
            : base()
        {
        }

        public AceRefactorableFunctionsCacheService(ConcurrentDictionary<string, AceRefactorableFunctionsItem> store)
            : base(store)
        {
        }

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

        public override void Put(AceRefactorableFunctionsEntry entry, long? operationGeneration = null)
        {
            if (!IsStillCurrentGeneration(operationGeneration))
            {
                return;
            }

            string filePath = entry.FilePath;
            string fileContents = entry.FileContents;
            string contentHash = Hash(fileContents);

            Cache[filePath] = new AceRefactorableFunctionsItem(contentHash, entry.Result);
        }
    }
}
