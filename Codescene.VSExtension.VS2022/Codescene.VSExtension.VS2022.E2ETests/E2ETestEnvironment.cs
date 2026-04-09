// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Linq;

namespace Codescene.VSExtension.VS2022.E2ETests;

internal static class E2ETestEnvironment
{
    public const string EnableVariableName = "CODESCENE_E2E";

    public const string SkipCopilotDismissVariableName = "CODESCENE_E2E_SKIP_COPILOT_DISMISS";
    public const string RootSuffix = "Exp";
    public const string PackageGuid = "CD713357-2FDA-490D-927D-805DECD1DD76";
    public const int OpenSettingsCommandId = 0x1025;
    public const int OpenCodeHealthMonitorCommandId = 0x0801;

    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(0.5);
    public static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan UpdateConfigurationTimeout = TimeSpan.FromMinutes(10);

    public static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnableVariableName);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public static bool ShouldSkipCopilotOnboardingDismiss()
    {
        var value = Environment.GetEnvironmentVariable(SkipCopilotDismissVariableName);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    public static string GetSolutionRoot() => Path.Combine(GetRepositoryRoot(), "Codescene.VSExtension.VS2022");

    public static string GetExtensionProjectRoot() => Path.Combine(GetSolutionRoot(), "Codescene.VSExtension.VS2022");

    public static string CreateArtifactsDirectory()
    {
        var path = Path.Combine(
            GetSolutionRoot(),
            "TestResults",
            "E2E",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string FindVsixPath()
    {
#if DEBUG
        var releaseDirectory = Path.Combine(GetExtensionProjectRoot(), "bin", "Debug");
#else
        var releaseDirectory = Path.Combine(GetExtensionProjectRoot(), "bin", "Release");
#endif
        if (!Directory.Exists(releaseDirectory))
        {
            throw new FileNotFoundException($"Could not find the extension Release output directory at '{releaseDirectory}'.");
        }

        var vsix = new DirectoryInfo(releaseDirectory)
            .EnumerateFiles("*.vsix", SearchOption.AllDirectories)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        if (vsix == null)
        {
            throw new FileNotFoundException("Could not find a built VSIX package. Build the solution in Release before running the e2e suite.");
        }

        return vsix.FullName;
    }

    public static string FindDevenvPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("DEVENV_EXE");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
        {
            return envOverride;
        }

        var devenv = FindVisualStudioTool("devenv.exe");
        if (devenv == null)
        {
            throw new FileNotFoundException("Could not find devenv.exe. Install Visual Studio 2022 on the runner or set DEVENV_EXE.");
        }

        return devenv;
    }

    private static string? FindVisualStudioTool(string fileName)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            var visualStudioDirectory = Path.Combine(root, "Microsoft Visual Studio", "2022");
            if (!Directory.Exists(visualStudioDirectory))
            {
                continue;
            }

            var tool = new DirectoryInfo(visualStudioDirectory)
                .EnumerateFiles(fileName, SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (tool != null)
            {
                return tool.FullName;
            }
        }

        return null;
    }
}
