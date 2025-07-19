using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

[Export(typeof(ILogger))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class Logger : ILogger
{
    private readonly OutputPaneManager _outputPaneManager;
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Codescene",
        "codescene-extension.log");
    private static readonly object _fileLock = new object();

    [ImportingConstructor]
    public Logger(OutputPaneManager outputPaneManager)
    {
        _outputPaneManager = outputPaneManager ?? throw new ArgumentNullException(nameof(outputPaneManager));
        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));
    }

    private void WriteToFile(string message)
    {
        lock (_fileLock)
        {
            File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
    }

    public void Error(string message, Exception ex)
    {
        var logMsg = $"[ERROR] {message}: {ex.Message}";
        WriteAsync(logMsg).FireAndForget();
        WriteToFile(logMsg);
        ex.Log();

        if (ex.Message.Contains("timeout")) SendTelemetry();
    }

    public void Info(string message)
    {
        var logMsg = $"[INFO] {message}";
        WriteAsync(logMsg).FireAndForget();
        WriteToFile(logMsg);
        VS.StatusBar.ShowMessageAsync($"{Titles.CODESCENE}: {message}").FireAndForget();
        Console.WriteLine(message);
    }

    public void Warn(string message)
    {
        var logMsg = $"[WARN] {message}";
        WriteAsync(logMsg).FireAndForget();
        WriteToFile(logMsg);
        VS.StatusBar.ShowMessageAsync(message).FireAndForget();
        Console.WriteLine(message);
    }

    public void Debug(string message)
    {
        Console.WriteLine(message);

        if (General.Instance.ShowDebugLogs)
        {
            var logMsg = $"[DEBUG] {message}";
            WriteAsync(logMsg).FireAndForget();
            WriteToFile(logMsg);
        }
    }

    public async Task LogAsync(string message, Exception ex)
    {
        var logMsg = $"[LOG] {message}: {ex.Message}";
        WriteAsync(logMsg).FireAndForget();
        WriteToFile(logMsg);
        await ex.LogAsync();
    }

    private async Task WriteAsync(string message)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        _outputPaneManager.Pane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
    }

    private void SendTelemetry()
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(Telemetry.REVIEW_OR_DELTA_TIMEOUT);
        }).FireAndForget();
    }
}
