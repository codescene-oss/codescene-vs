// Copyright (c) CodeScene. All rights reserved.

using System.Security.Cryptography;
using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliBinaryIntegrityVerifierTests
    {
        private string _tempFilePath;

        [TestInitialize]
        public void Setup()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            File.WriteAllText(_tempFilePath, "integrity test content");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public void Verify_WhenHashMatches_DoesNotThrow()
        {
            CliBinaryIntegrityVerifier.Verify(_tempFilePath, ComputeSha256Hex(_tempFilePath));
        }

        [TestMethod]
        public void Verify_WhenHashMismatch_ThrowsInvalidOperationException()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                CliBinaryIntegrityVerifier.Verify(_tempFilePath, new string('a', 64)));

            Assert.Contains("integrity verification", exception.Message);
        }

        private static string ComputeSha256Hex(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
