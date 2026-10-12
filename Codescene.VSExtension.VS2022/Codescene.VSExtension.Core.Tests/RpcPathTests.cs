// Copyright (c) CodeScene. All rights reserved.

using System.Runtime.InteropServices;
using System.Text;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RpcPathTests
    {
        [TestMethod]
        public void CanonicalRepoRoot_NonExistentPath_FallsBackToNormalizeFsPath()
        {
            var missing = Path.Combine(Path.GetTempPath(), "rpc-path-missing-" + Guid.NewGuid().ToString("N"));
            var canonical = RpcPath.CanonicalRepoRoot(missing);
            Assert.AreEqual(RpcPath.NormalizeFsPath(missing), canonical);
        }

        [TestMethod]
        public void ToLocalRepoRoot_RoundTripsRememberedRoot()
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), "rpc-path-roundtrip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);

            try
            {
                RpcPath.RememberLocalRepoRoot(repoRoot);
                var canonical = RpcPath.CanonicalRepoRoot(repoRoot);
                Assert.AreEqual(repoRoot, RpcPath.ToLocalRepoRoot(canonical));
            }
            finally
            {
                Directory.Delete(repoRoot, true);
            }
        }

        [TestMethod]
        public void PathsEqual_WindowsShortPathMatchesLongPath()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive();
                return;
            }

            var longPath = Path.Combine(Path.GetTempPath(), "rpc-path-long-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(longPath);

            try
            {
                var shortPath = GetShortPath(longPath);
                Assert.IsTrue(RpcPath.PathsEqual(longPath, shortPath));
                Assert.AreEqual(RpcPath.CanonicalRepoRoot(longPath), RpcPath.CanonicalRepoRoot(shortPath));
            }
            finally
            {
                Directory.Delete(longPath, true);
            }
        }

        private static string GetShortPath(string longPath)
        {
            var buffer = new StringBuilder(512);
            var length = GetShortPathName(longPath, buffer, (uint)buffer.Capacity);
            if (length == 0 || length >= buffer.Capacity)
            {
                return longPath;
            }

            return buffer.ToString();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);
    }
}
