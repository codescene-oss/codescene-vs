using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Models.Cache.Delta
{
    public class DeltaCacheItem
    {
        public string HeadHash { get; }

        public string CurrentHash { get; }

        public DeltaResponseModel Delta { get; }

        public DeltaCacheItem(string headHash, string currentHash, DeltaResponseModel delta)
        {
            HeadHash = headHash;
            CurrentHash = currentHash;
            Delta = delta;
        }
    }
}
