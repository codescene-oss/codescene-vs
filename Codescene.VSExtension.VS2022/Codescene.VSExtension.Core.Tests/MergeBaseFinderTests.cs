// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class MergeBaseFinderTests : GitChangeDetectorTestBase
    {
        private MergeBaseFinder _finder;

        [TestInitialize]
        public void SetupFinder()
        {
            _finder = new MergeBaseFinder(_fakeLogger);
        }

        [TestMethod]
        public void GetMergeBaseCommit_WithEmptyRepo_ReturnsNull()
        {
            var emptyRepoPath = Path.Combine(Path.GetTempPath(), $"empty-repo-{Guid.NewGuid()}");
            Directory.CreateDirectory(emptyRepoPath);
            Repository.Init(emptyRepoPath);

            Commit result = null;
            try
            {
                using (var repo = new Repository(emptyRepoPath))
                {
                    result = _finder.GetMergeBaseCommit(repo);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(emptyRepoPath, true);
                }
                catch
                {
                }
            }

            Assert.IsNull(result, "Should return null for empty repository with no commits");
        }

        [TestMethod]
        public void GetMergeBaseCommit_WithCorruptedRepo_HandlesGracefullyAndReturnsNull()
        {
            CorruptGitObjects();

            using (var repo = new Repository(_testRepoPath))
            {
                var result = _finder.GetMergeBaseCommit(repo);

                Assert.IsNull(result, "Should return null when git operations fail");
            }
        }

        [TestMethod]
        public void GetMergeBaseCommit_OnMainBranch_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var result = _finder.GetMergeBaseCommit(repo);

                Assert.IsNull(result, "Should return null when on main branch");
            }
        }

        [TestMethod]
        public void GetMergeBaseCommit_OnFeatureBranchWithMergeBase_ReturnsCommit()
        {
            Commit expectedMergeBase;
            using (var repo = new Repository(_testRepoPath))
            {
                expectedMergeBase = repo.Head.Tip;
            }

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "feature content", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = _finder.GetMergeBaseCommit(repo);

                Assert.AreEqual(expectedMergeBase.Sha, result.Sha, "Should return the initial commit as merge base");
                Assert.IsTrue(_fakeLogger.DebugMessages.Exists(m => m.Contains("Found merge base using branch")), "Should log merge base found");
            }
        }

        [TestMethod]
        public void IsMainBranch_WithMainBranches_ReturnsTrue()
        {
            Assert.IsTrue(_finder.IsMainBranch("main"), "Should recognize 'main'");
            Assert.IsTrue(_finder.IsMainBranch("master"), "Should recognize 'master'");
            Assert.IsTrue(_finder.IsMainBranch("develop"), "Should recognize 'develop'");
            Assert.IsTrue(_finder.IsMainBranch("trunk"), "Should recognize 'trunk'");
            Assert.IsTrue(_finder.IsMainBranch("dev"), "Should recognize 'dev'");
        }

        [TestMethod]
        public void IsMainBranch_WithDifferentCasing_ReturnsTrue()
        {
            Assert.IsTrue(_finder.IsMainBranch("Main"), "Should be case-insensitive for 'Main'");
            Assert.IsTrue(_finder.IsMainBranch("MASTER"), "Should be case-insensitive for 'MASTER'");
            Assert.IsTrue(_finder.IsMainBranch("Develop"), "Should be case-insensitive for 'Develop'");
        }

        [TestMethod]
        public void IsMainBranch_WithNonMainBranch_ReturnsFalse()
        {
            Assert.IsFalse(_finder.IsMainBranch("feature"), "Should not recognize 'feature'");
            Assert.IsFalse(_finder.IsMainBranch("bugfix"), "Should not recognize 'bugfix'");
            Assert.IsFalse(_finder.IsMainBranch("release"), "Should not recognize 'release'");
        }

        [TestMethod]
        public void IsMainBranch_WithNullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(_finder.IsMainBranch(null), "Should return false for null");
            Assert.IsFalse(_finder.IsMainBranch(string.Empty), "Should return false for empty string");
        }

        [TestMethod]
        public void GetMergeBaseCommit_WithPartiallyCorruptedRepo_HandlesExceptionInFindMergeBase()
        {
            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "feature content", "Add feature");

            CorruptGitObjects();

            using (var repo = new Repository(_testRepoPath))
            {
                var result = _finder.GetMergeBaseCommit(repo);

                Assert.IsNotEmpty(_fakeLogger.DebugMessages, "Should log debug messages");
            }
        }
    }
}
