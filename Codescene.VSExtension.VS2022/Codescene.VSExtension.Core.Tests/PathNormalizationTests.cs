// Copyright (c) CodeScene. All rights reserved.

using System.Runtime.InteropServices;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class PathNormalizationTests
    {
        [TestMethod]
        public void NormalizeWorkingDirectory_NullAndEmpty_ReturnOriginal()
        {
            Assert.IsNull(PathNormalization.NormalizeWorkingDirectory(null!));
            Assert.AreEqual(string.Empty, PathNormalization.NormalizeWorkingDirectory(string.Empty));
        }

        [TestMethod]
        public void NormalizeWorkingDirectory_RelativePathNoRoot_ReturnsTrimmed()
        {
            var path = Path.Combine("sub", "dir") + Path.DirectorySeparatorChar;
            var result = PathNormalization.NormalizeWorkingDirectory(path);
            Assert.AreEqual(Path.Combine("sub", "dir"), result);
        }

        [TestMethod]
        public void NormalizeWorkingDirectory_WindowsDriveRoot_ReturnsOriginalPath()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive();
                return;
            }

            var root = Path.GetPathRoot(Path.GetTempPath());
            Assert.IsFalse(string.IsNullOrEmpty(root));
            var pathWithExtraSeparators = root + new string(Path.DirectorySeparatorChar, 2);
            var result = PathNormalization.NormalizeWorkingDirectory(pathWithExtraSeparators);
            Assert.AreEqual(pathWithExtraSeparators, result);
        }
    }
}
