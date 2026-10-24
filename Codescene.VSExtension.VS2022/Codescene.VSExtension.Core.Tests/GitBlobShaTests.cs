// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitBlobShaTests
    {
        [TestMethod]
        public void FromUtf8_EmptyContent_MatchesGitBlobHash()
        {
            Assert.AreEqual("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", GitBlobSha.FromUtf8(string.Empty));
        }

        [TestMethod]
        public void FromUtf8_KnownContent_MatchesGitBlobHash()
        {
            Assert.AreEqual("ce013625030ba8dba906f756967f9e9ca394464a", GitBlobSha.FromUtf8("hello\n"));
        }

        [TestMethod]
        public void FromUtf8_NullContent_MatchesEmptyBlob()
        {
            Assert.AreEqual(GitBlobSha.FromUtf8(string.Empty), GitBlobSha.FromUtf8(null));
        }
    }
}
