// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
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

        public string GetBaselineCommit(string repoRootPath)
        {
            return Mock.Object.GetBaselineCommit(repoRootPath);
        }

        public string GetFileContentForCommit(string path)
        {
            return Mock.Object.GetFileContentForCommit(path);
        }

        public string GetFileContentForCommit(string path, string baselineCommit)
        {
            return Mock.Object.GetFileContentForCommit(path, baselineCommit);
        }

        public bool IsFileIgnored(string filePath)
        {
            return Mock.Object.IsFileIgnored(filePath);
        }

        public HashSet<string> FilterIgnoredFiles(IEnumerable<string> absolutePaths)
        {
            return new HashSet<string>(absolutePaths, StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
        }
    }
}
