using Codescene.VSExtension.Core.Interfaces.Cli;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(IGitService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestGitService : IGitService
    {
        internal Mock<IGitService> Mock = new();

        public string GetFileContentForCommit(string path)
        {
            return Mock.Object.GetFileContentForCommit(path);
        }

        public bool IsFileIgnored(string filePath)
        {
            return Mock.Object.IsFileIgnored(filePath);
        }
    }
}
