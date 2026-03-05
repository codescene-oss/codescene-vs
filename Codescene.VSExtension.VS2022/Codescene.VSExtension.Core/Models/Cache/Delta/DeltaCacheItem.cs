// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Models.Cache.Delta
{
    public class DeltaCacheItem
    {
        public DeltaCacheItem(string filePath, string headHash, string currentHash, DeltaResponseModel delta)
        {
            FilePath = filePath;
            HeadHash = headHash;
            CurrentHash = currentHash;
            Delta = delta;
        }

        public string FilePath { get; set; }

        public string HeadHash { get; }

        public string CurrentHash { get; }

        public DeltaResponseModel Delta { get; }
    }
}
