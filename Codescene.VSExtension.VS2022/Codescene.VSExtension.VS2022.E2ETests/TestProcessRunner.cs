// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;

namespace Codescene.VSExtension.VS2022.E2ETests;

internal static class TestProcessRunner
{
    internal static Process StartProcess(string fileName, string arguments)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
        });

        Assert.IsNotNull(process, $"Could not start '{fileName}'.");
        return process;
    }

    internal static int RunExternalProcess(string fileName, string arguments, bool allowFailure, TimeSpan? timeout = null)
    {
        var wait = timeout ?? E2ETestEnvironment.ProcessTimeout;
        using var process = StartProcess(fileName, arguments);
        if (!process.WaitForExit((int)wait.TotalMilliseconds))
        {
            process.Kill();
            throw new TimeoutException($"Timed out waiting for '{fileName} {arguments}'.");
        }

        if (!allowFailure && process.ExitCode > 0)
        {
            throw new InvalidOperationException($"Process '{fileName} {arguments}' failed with exit code {process.ExitCode}.");
        }

        return process.ExitCode;
    }
}
