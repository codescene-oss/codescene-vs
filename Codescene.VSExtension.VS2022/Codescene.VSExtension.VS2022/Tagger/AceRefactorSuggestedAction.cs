using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

internal class AceRefactorSuggestedAction : ISuggestedAction
{
    private readonly string _filePath;
    private readonly FnToRefactorModel _refactorableFunction;

    public AceRefactorSuggestedAction(string filePath, FnToRefactorModel refactorableFunction)
    {
        _filePath = filePath;
        _refactorableFunction = refactorableFunction;
    }

    public string DisplayText => "Refactor using CodeScene ACE";

    public bool HasActionSets => false;

    public bool HasPreview => false;

    public string IconAutomationText => null;

    public ImageMoniker IconMoniker => default;

    public string InputGestureText => null;

    public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
    }

    public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(null);
    }

    public void Invoke(CancellationToken cancellationToken)
    {
        ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            ILogger logger = null;

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                logger = await VS.GetMefServiceAsync<ILogger>();
                var onClickRefactoringHandler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

                if (onClickRefactoringHandler == null)
                {
                    return;
                }

                await onClickRefactoringHandler.HandleAsync(
                    _filePath,
                    _refactorableFunction,
                    AceConstants.AceEntryPoint.INTENTIONACTION);
            }
            catch (Exception e)
            {
                logger?.Error("Unable to handle Quick Action refactoring.", e);
            }
        });
    }

    public void Dispose()
    {
    }

    public bool TryGetTelemetryId(out Guid telemetryId)
    {
        telemetryId = Guid.Empty;
        return false;
    }
}
