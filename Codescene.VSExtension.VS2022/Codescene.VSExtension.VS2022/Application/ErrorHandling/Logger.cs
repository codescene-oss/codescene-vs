using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

[Export(typeof(ILogger))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class Logger : ILogger
{
    private readonly OutputPaneManager _outputPaneManager;

    [ImportingConstructor]
    public Logger(OutputPaneManager outputPaneManager)
    {
        _outputPaneManager = outputPaneManager ?? throw new ArgumentNullException(nameof(outputPaneManager));
    }

    public void Error(string message, Exception ex)
    {
        WriteAsync($"[ERROR] {message}: {ex.Message}").FireAndForget();
        ex.Log();

        if (ex.Message.Contains("timeout")) SendTelemetry();
    }

    public void Info(string message)
    {
        WriteAsync($"[INFO] {message}").FireAndForget();
        VS.StatusBar.ShowMessageAsync($"{Titles.CODESCENE}: {message}").FireAndForget();
        Console.WriteLine(message);
    }

    public void Warn(string message)
    {
        WriteAsync($"[WARN] {message}").FireAndForget();
        VS.StatusBar.ShowMessageAsync(message).FireAndForget();
        Console.WriteLine(message);
    }

    public void Debug(string message)
    {
        Console.WriteLine(message);

        if (General.Instance.ShowDebugLogs)
            WriteAsync($"[DEBUG] {message}").FireAndForget();
    }

    public async Task LogAsync(string message, Exception ex)
    {
        WriteAsync($"[LOG] {message}: {ex.Message}").FireAndForget();
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
