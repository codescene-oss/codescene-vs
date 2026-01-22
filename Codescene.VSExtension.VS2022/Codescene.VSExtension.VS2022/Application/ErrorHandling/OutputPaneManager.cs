using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

[Export(typeof(OutputPaneManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class OutputPaneManager
{
    private IServiceProvider _serviceProvider;
    private IVsOutputWindowPane _pane;
    private Guid _paneGuid = new("B76CFA36-066A-493B-8898-22EF97B0888F");
    private const string PaneTitle = Titles.CODESCENE;

    [ImportingConstructor]
    public OutputPaneManager([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IVsOutputWindowPane Pane
    {
        get
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _pane;
        }
    }

    public async Task InitializeAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        const bool isVisible = true;
        const bool isClearedWithSolution = false;

        if (_serviceProvider.GetService(typeof(SVsOutputWindow)) is not IVsOutputWindow outputWindow)
            throw new InvalidOperationException("Could not get SVsOutputWindow service.");

        outputWindow.CreatePane(
            ref _paneGuid,
            PaneTitle,
            Convert.ToInt32(isVisible),
            Convert.ToInt32(isClearedWithSolution)
        );
        outputWindow.GetPane(ref _paneGuid, out _pane);
    }
}