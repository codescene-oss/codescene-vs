using Codescene.VSExtension.Core.Application.Services.Util;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(IExtensionMetadataProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestExtensionMetadataProvider : IExtensionMetadataProvider
    {
        internal Mock<IExtensionMetadataProvider> Mock = new();

        public string GetDescription() => Mock.Object.GetDescription();
        public string GetDisplayName() => Mock.Object.GetDisplayName();
        public string GetPublisher() => Mock.Object.GetPublisher();
        public string GetVersion() => Mock.Object.GetVersion();
    }
}
