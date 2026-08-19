// Copyright (c) CodeScene. All rights reserved.

using System.Text;
using Codescene.VSExtension.Core.Application.Cli.Rpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitBlobShaTests
    {
        [TestMethod]
        public void FromUtf8_MatchesGitBlobSha1()
        {
            var content = "hello\n";
            var expected = GitBlobSha.FromBytes(Encoding.UTF8.GetBytes(content));

            Assert.AreEqual(expected, GitBlobSha.FromUtf8(content));
            Assert.AreEqual(40, expected.Length);
        }
    }
}
