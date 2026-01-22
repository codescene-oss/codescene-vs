using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ICacheStorageService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestCacheStorageService : ICacheStorageService
    {
        internal Mock<ICacheStorageService> Mock = new();

        public string GetSolutionReviewCacheLocation() => Mock.Object.GetSolutionReviewCacheLocation();
        public Task InitializeAsync() => Mock.Object.InitializeAsync();
        public void RemoveOldReviewCacheEntries(int nrOfDays = 30) => Mock.Object.RemoveOldReviewCacheEntries(nrOfDays);
    }
}
