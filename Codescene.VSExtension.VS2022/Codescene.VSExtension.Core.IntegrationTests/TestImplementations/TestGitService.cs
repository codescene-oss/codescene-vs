// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Git;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(IGitService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestGitService : IGitService
    {
        internal Mock<IGitService> Mock = new Mock<IGitService>();

        public bool IsFileIgnored(string filePath)
        {
            return Mock.Object.IsFileIgnored(filePath);
        }

        public void Dispose()
        {
        }
    }
}
