using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.CodeLensProvider.Providers.Base.Constants;

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
        Write($"[ERROR] {message}: {ex.Message}");
        ex.Log();
    }

    public void Info(string message)
    {
        Write($"[INFO] {message}");
        VS.StatusBar.ShowMessageAsync($"{Titles.CODESCENE}: {message}").FireAndForget();
        Console.WriteLine(message);
    }

    public void Warn(string message)
    {
        Write($"[WARN] {message}");
        VS.StatusBar.ShowMessageAsync(message).FireAndForget();
        Console.WriteLine(message);
    }

    public void Debug(string message)
    {
        Write($"[DEBUG] {message}");
        Console.WriteLine(message);
    }

    public async Task LogAsync(string message, Exception ex)
    {
        Write($"[LOG] {message}: {ex.Message}");
        await ex.LogAsync();
    }

    private void Write(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _outputPaneManager.Pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
