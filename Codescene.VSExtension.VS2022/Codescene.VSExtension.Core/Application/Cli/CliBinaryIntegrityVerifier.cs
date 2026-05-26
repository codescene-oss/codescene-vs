// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Security.Cryptography;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static class CliBinaryIntegrityVerifier
    {
        public static void Verify(string filePath, string expectedSha256Hex)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256Hex))
            {
                throw new ArgumentException("Expected SHA-256 hash is required.", nameof(expectedSha256Hex));
            }

            var actualSha256Hex = ComputeSha256Hex(filePath);
            if (!string.Equals(actualSha256Hex, expectedSha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "CodeScene CLI executable failed integrity verification. " +
                    "Please reinstall the extension or contact support if this issue persists.");
            }
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
