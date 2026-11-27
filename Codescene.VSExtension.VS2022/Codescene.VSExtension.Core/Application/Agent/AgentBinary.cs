// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal static class AgentBinary
    {
        public static void Verify(ICliSettingsProvider settings, ILogger logger)
        {
            var path = settings.AgentFileFullPath;

            if (!File.Exists(path))
            {
                var message = $"The Refactoring Agent is not available for windows-x64: {path} is missing.";
                logger.Warn(message);
                throw new AgentFailure(AgentFailureKind.Generic, message);
            }

            var expectedSha = settings.RequiredAgentBinarySha256;
            var actualSha = ComputeSha256Hex(path);

            if (!string.Equals(actualSha, expectedSha, System.StringComparison.OrdinalIgnoreCase))
            {
                var message = "The Refactoring Agent binary failed integrity verification. Reinstall the extension.";
                logger.Warn(message);
                throw new AgentFailure(AgentFailureKind.Generic, message);
            }
        }

        private static string ComputeSha256Hex(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(stream);
            return System.BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
