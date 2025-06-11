using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.CodeLensProvider.Providers.Base.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

[Export(typeof(ILogger))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class Logger : ILogger
{
    private IVsOutputWindowPane _pane;
    private Guid PaneGuid = new("B76CFA36-066A-493B-8898-22EF97B0888F");

    [ImportingConstructor]
    public Logger([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        InitalizePaneAsync(serviceProvider).FireAndForget();
    }

    private async Task InitalizePaneAsync(IServiceProvider serviceProvider)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var outputWindow = serviceProvider.GetService<SVsOutputWindow, IVsOutputWindow>();

        const bool isVisible = true;
        const bool isClearedWithSolution = false;

        outputWindow.CreatePane(
            ref PaneGuid,
            Titles.CODESCENE,
            Convert.ToInt32(isVisible),
            Convert.ToInt32(isClearedWithSolution)
            );

        outputWindow.GetPane(ref PaneGuid, out _pane);
    }

    public void Error(string message, Exception ex)
    {
        Write($"[ERROR] {message}: {ex.Message}");
        ex.Log();
    }

    public void Info(string message)
    {
        Write($"[INFO] {message}");
        VS.StatusBar.ShowMessageAsync(message).FireAndForget();
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
        _pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
