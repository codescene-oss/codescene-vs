using Codescene.VSExtension.Core.Application.Services.Git;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;

namespace Codescene.VSExtension.VS2022.Application.Git;

[Export(typeof(IGitService))]
public class GitCliService : IGitService
{
    private readonly string _workingDirectory;

    [ImportingConstructor]
    public GitCliService()
    {
        _workingDirectory = Environment.CurrentDirectory;
    }

    public GitCliService(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public GitResult ExecuteGitCommand(string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true, // Captures the output that the process writes to the console (stdout)
                RedirectStandardError = true, // Captures error messages that the process writes to the error stream (stderr)
                UseShellExecute = false, // Runs the process directly without using the shell
                CreateNoWindow = true, // Prevents a new console window from appearing when the process runs
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Capture standard output asynchronously
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            // Capture standard error asynchronously
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new GitResult
            {
                ExitCode = process.ExitCode,
                Output = outputBuilder.ToString().Trim(),
                Error = errorBuilder.ToString().Trim()
            };
        }
        catch (Exception ex)
        {
            return new GitResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"Exception while executing git command: {ex.Message}"
            };
        }
    }
}
