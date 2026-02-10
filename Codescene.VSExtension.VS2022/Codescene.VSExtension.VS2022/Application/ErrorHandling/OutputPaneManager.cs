// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.Application.ErrorHandling;

[Export(typeof(OutputPaneManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class OutputPaneManager
{
    private const string PaneTitle = Titles.CODESCENE;

    private IServiceProvider _serviceProvider;
    private IVsOutputWindowPane _pane;
    private Guid _paneGuid = new Guid("B76CFA36-066A-493B-8898-22EF97B0888F");

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
        {
            throw new InvalidOperationException("Could not get SVsOutputWindow service.");
        }

        outputWindow.CreatePane(
            ref _paneGuid,
            PaneTitle,
            Convert.ToInt32(isVisible),
            Convert.ToInt32(isClearedWithSolution));
        outputWindow.GetPane(ref _paneGuid, out _pane);
    }

    public async Task ShowAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (_pane == null)
        {
            await InitializeAsync();
        }

        _pane?.Activate();

        if (_serviceProvider.GetService(typeof(SVsUIShell)) is not IVsUIShell shell)
        {
            throw new InvalidOperationException("Could not get SVsUIShell service.");
        }

        object inputVariant = null;
        shell.PostExecCommand(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.OutputWindow, 0, ref inputVariant);
    }
}
