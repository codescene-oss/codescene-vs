// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.Text;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal sealed class NativeIdeServerProcessFactory : IIdeServerProcessFactory
    {
        public IIdeServerProcess Start(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName ?? throw new ArgumentNullException(nameof(fileName)),
                Arguments = arguments ?? string.Empty,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start the CodeScene CLI server process.");
            }

            return new NativeIdeServerProcess(process);
        }
    }
}
