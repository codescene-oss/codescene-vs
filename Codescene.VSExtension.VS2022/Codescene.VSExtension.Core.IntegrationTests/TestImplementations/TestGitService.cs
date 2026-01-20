using Codescene.VSExtension.Core.Application.Services.Git;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(IGitService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestGitService : IGitService
    {
        internal Mock<IGitService> Mock = new();
        public string GetFileContentForCommit(string path) => Mock.Object.GetFileContentForCommit(path);
    }
}
