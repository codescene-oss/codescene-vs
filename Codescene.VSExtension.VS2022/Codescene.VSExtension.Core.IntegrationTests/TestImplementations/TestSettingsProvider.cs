using Codescene.VSExtension.Core.Application.Services.Settings;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ISettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestSettingsProvider : ISettingsProvider
    {
        internal Mock<ISettingsProvider> Mock = new();
        public bool ShowDebugLogs => Mock.Object.ShowDebugLogs;
        public string AuthToken => Mock.Object.AuthToken;
    }
}
