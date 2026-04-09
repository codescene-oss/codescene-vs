// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;

namespace Codescene.VSExtension.VS2022.E2ETests;

internal sealed class E2EWorkspace : IDisposable
{
    private readonly string _rootDirectory;
    private bool _disposed;

    private E2EWorkspace(string rootDirectory, string solutionPath)
    {
        _rootDirectory = rootDirectory;
        RootDirectory = rootDirectory;
        SolutionPath = solutionPath;
    }

    public string RootDirectory { get; }

    public string SolutionPath { get; }

    public static E2EWorkspace Create(string scenarioName = "MinimalScenario")
    {
        var templateRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", scenarioName);
        if (!Directory.Exists(templateRoot))
        {
            throw new DirectoryNotFoundException($"E2E scenario template not found at '{templateRoot}'. Ensure TestAssets are copied to the output directory.");
        }

        var instanceRoot = Path.Combine(
            Path.GetTempPath(),
            "CodesceneE2E",
            scenarioName,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(instanceRoot);
        CopyDirectory(templateRoot, instanceRoot);

        var solutionFileName = FindSolutionFileName(templateRoot);
        var solutionPath = Path.Combine(instanceRoot, solutionFileName);
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Scenario '{scenarioName}' has no solution file next to the template root.");
        }

        InitializeGitRepository(instanceRoot);

        return new E2EWorkspace(instanceRoot, solutionPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, true);
            }
        }
        catch
        {
        }
    }

    private static string FindSolutionFileName(string templateRoot)
    {
        foreach (var path in Directory.EnumerateFiles(templateRoot, "*.sln", SearchOption.TopDirectoryOnly))
        {
            return Path.GetFileName(path);
        }

        throw new FileNotFoundException($"No .sln file in scenario template '{templateRoot}'.");
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = directory.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var destFile = Path.Combine(destDir, relative);
            var parent = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, destFile, true);
        }
    }

    private static void InitializeGitRepository(string workingDirectory)
    {
        RunGit(workingDirectory, "init");
        RunGit(workingDirectory, "config user.email \"e2e@codescene.local\"");
        RunGit(workingDirectory, "config user.name \"Codescene E2E\"");
        RunGit(workingDirectory, "add -A");
        RunGit(workingDirectory, "commit -m \"e2e initial\"");
    }

    private static void RunGit(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        });

        if (process == null)
        {
            throw new InvalidOperationException("Could not start git. Install Git and ensure it is on PATH for e2e runs.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git {arguments} failed with exit code {process.ExitCode}. {stderr}");
        }
    }
}
