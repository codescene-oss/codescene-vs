// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitPathDiscoveryTests
    {
        [TestMethod]
        public void ResolveRepositoryRoots_ReturnsWorkingDirectoriesAndNestedRepos()
        {
            var parent = Path.Combine(Path.GetTempPath(), "codescene-reporoots-" + Guid.NewGuid().ToString("N"));
            var repo1 = Path.Combine(parent, "repo1");
            var repo2 = Path.Combine(parent, "repo2");
            var nested = Path.Combine(parent, "folder", "nested");
            var nodeModules = Path.Combine(parent, "folder", "node_modules");

            Directory.CreateDirectory(repo1);
            Directory.CreateDirectory(repo2);
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(nodeModules);

            LibGit2Sharp.Repository.Init(repo1);
            LibGit2Sharp.Repository.Init(repo2);
            LibGit2Sharp.Repository.Init(nested);
            LibGit2Sharp.Repository.Init(nodeModules);

            try
            {
                var directories = new[] { repo1, parent + Path.DirectorySeparatorChar + "folder" };
                var roots = GitPathDiscovery.ResolveRepositoryRoots(directories);

                Assert.AreEqual(2, roots.Count);
                Assert.IsTrue(roots.Any(r => RpcPath.PathsEqual(r, repo1)));
                Assert.IsTrue(roots.Any(r => RpcPath.PathsEqual(r, nested)));
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }

        [TestMethod]
        public void ResolveRepositoryRoots_WithNestedAndWorkingDirectories_DeduplicatesByNormalization()
        {
            var parent = Path.Combine(Path.GetTempPath(), "codescene-dedup-" + Guid.NewGuid().ToString("N"));
            var repo = Path.Combine(parent, "repo");

            Directory.CreateDirectory(repo);
            LibGit2Sharp.Repository.Init(repo);

            try
            {
                var directories = new[]
                {
                    repo,
                    repo.ToUpperInvariant(),
                    repo + Path.DirectorySeparatorChar,
                };

                var roots = GitPathDiscovery.ResolveRepositoryRoots(directories);

                Assert.AreEqual(1, roots.Count);
                Assert.IsTrue(RpcPath.PathsEqual(roots[0], repo));
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }

        [TestMethod]
        public void ResolveRepositoryRoots_WithNonGitDirectories_ReturnsOnlyGitRoots()
        {
            var parent = Path.Combine(Path.GetTempPath(), "codescene-nongit-" + Guid.NewGuid().ToString("N"));
            var repo = Path.Combine(parent, "repo");
            var nonGit = Path.Combine(parent, "nongit");

            Directory.CreateDirectory(repo);
            Directory.CreateDirectory(nonGit);
            LibGit2Sharp.Repository.Init(repo);

            try
            {
                var directories = new[] { repo, nonGit };
                var roots = GitPathDiscovery.ResolveRepositoryRoots(directories);

                Assert.AreEqual(1, roots.Count);
                Assert.IsTrue(RpcPath.PathsEqual(roots[0], repo));
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }
    }
}
