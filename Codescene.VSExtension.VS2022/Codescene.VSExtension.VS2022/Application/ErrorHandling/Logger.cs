// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.VS2022.Options;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

/// <summary>
/// Contains methods for logging messages in the Visual Studio extension.
/// Messages are logged to both the output pane and a local log file.
/// Log rotation is implemented to manage file size and backups.
/// File names are rotated when they exceed a specified size limit (10 MB).
/// File logs are stored in the local application data directory under "Codescene".
/// File name includes the extension version for easy identification (codescene-vs-extension-[version].log).
/// </summary>
[Export(typeof(ILogger))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class Logger : ILogger
{
    private const long MAXLOGFILESIZEBYTES = 10 * 1024 * 1024; // 10 MB
    private const int MAXBACKUPFILES = 3;
    private const string ERROR = "ERROR";
    private const string WARNING = "WARN";
    private const string INFORMATION = "INFO";
    private const string DEBUG = "DEBUG";

    private static readonly string LogFileName = "codescene-vs-extension-" + Vsix.Version + ".log";
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Codescene",
        LogFileName);

    private static readonly object FileLock = new object();

    private readonly OutputPaneManager _outputPaneManager;

    [ImportingConstructor]
    internal Logger(OutputPaneManager outputPaneManager)
    {
        _outputPaneManager = outputPaneManager ?? throw new ArgumentNullException(nameof(outputPaneManager));
        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));
    }

    public void Error(string message, Exception ex)
    {
        var fullMessage = $"{message}: {ex.Message}";
        WriteLogAsync(fullMessage, ERROR).FireAndForget();

        SendErrorTelemetryAsync(ex, message).FireAndForget();

        if (ex.Message.ToLowerInvariant().Contains("timeout"))
        {
            SendTimeoutTelemetryAsync().FireAndForget();
        }
    }

    public void Info(string message, bool statusBar = false)
    {
        WriteLogAsync(message, INFORMATION).FireAndForget();
        Console.WriteLine(message);
        if (statusBar)
        {
            SendToStatusBarAsync(message).FireAndForget();
        }
    }

    public void Warn(string message, bool statusBar = false)
    {
        WriteLogAsync(message, WARNING).FireAndForget();
        Console.WriteLine(message);
        if (statusBar)
        {
            SendToStatusBarAsync(message).FireAndForget();
        }
    }

    public void Debug(string message)
    {
        Console.WriteLine(message);

        if (General.Instance.ShowDebugLogs)
        {
            WriteLogAsync(message, DEBUG).FireAndForget();
        }
    }

    private static async Task SendToStatusBarAsync(string message)
    {
        await VS.StatusBar.ShowMessageAsync($"{Titles.CODESCENE}: {message}");
    }

    private async Task WriteLogAsync(string message, string level)
    {
        var logMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        await WriteToOutputAsync(logMsg);
        WriteToFile(logMsg);
    }

    private async Task WriteToOutputAsync(string message)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        _outputPaneManager.Pane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
    }

    private async Task SendErrorTelemetryAsync(Exception ex, string context)
    {
        var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
        telemetryManager?.SendErrorTelemetry(ex, context);
    }

    private async Task SendTimeoutTelemetryAsync()
    {
        var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
        telemetryManager?.SendTelemetry(Telemetry.REVIEWORDELTATIMEOUT);
    }

    private void WriteToFile(string message)
    {
        lock (FileLock)
        {
            try
            {
                // Check if rotation is needed before writing
                if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > MAXLOGFILESIZEBYTES)
                {
                    RotateLogFiles();
                }

                File.AppendAllText(LogFilePath, $"{message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                // Fallback to console if file writing fails
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine($"Original log message: {message}");
            }
        }
    }

    private void RotateLogFiles()
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(LogFilePath);
            var logFileName = Path.GetFileNameWithoutExtension(LogFilePath);
            var logFileExtension = Path.GetExtension(LogFilePath);

            // Remove the oldest backup if it exists
            var oldestBackup = Path.Combine(logDirectory, $"{logFileName}-{MAXBACKUPFILES}{logFileExtension}");
            if (File.Exists(oldestBackup))
            {
                File.Delete(oldestBackup);
            }

            // Shift existing backups
            for (var i = MAXBACKUPFILES - 1; i >= 1; i--)
            {
                var sourceFile = Path.Combine(logDirectory, $"{logFileName}-{i}{logFileExtension}");
                var targetFile = Path.Combine(logDirectory, $"{logFileName}-{i + 1}{logFileExtension}");

                if (File.Exists(sourceFile))
                {
                    File.Move(sourceFile, targetFile);
                }
            }

            // Move current log to .1 backup
            var firstBackup = Path.Combine(logDirectory, $"{logFileName}-1{logFileExtension}");
            if (File.Exists(LogFilePath))
            {
                File.Move(LogFilePath, firstBackup);
            }
        }
        catch (Exception ex)
        {
            // If rotation fails, truncate the current file as fallback
            Console.WriteLine($"Log rotation failed: {ex.Message}. Truncating current log file.");
            File.WriteAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] Log file truncated due to size limit{Environment.NewLine}");
        }
    }
}
