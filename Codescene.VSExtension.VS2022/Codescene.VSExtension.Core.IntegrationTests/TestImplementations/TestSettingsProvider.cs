// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Moq;

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
