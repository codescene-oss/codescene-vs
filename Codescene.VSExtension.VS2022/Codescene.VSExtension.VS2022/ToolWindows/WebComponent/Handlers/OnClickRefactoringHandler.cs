// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.VS2022.Application.Services;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(OnClickRefactoringHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnClickRefactoringHandler
{
    [Import]
    private readonly AceComponentMapper _mapper;

    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly IAceManager _aceManager;

    private CancellationTokenSource _cancellationTokenSource = null;

    public string Path { get; private set; }

    public FnToRefactorModel RefactorableFunction { get; private set; }

    public async Task HandleAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint)
    {
        Path = path;
        RefactorableFunction = refactorableFunction;

        // Check if user has acknowledged ACE usage
        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        if (!acknowledgementStateService.IsAcknowledged())
        {
            AceAcknowledgeToolWindow.UpdateRefactoringData(refactorableFunction, path);

            if (AceAcknowledgeToolWindow.IsCreated())
            {
                await AceAcknowledgeToolWindow.UpdateViewAsync();
            }
            else
            {
                await AceAcknowledgeToolWindow.ShowAsync();
            }

            return;
        }

        if (AceToolWindow.IsCreated())
        {
            SetViewToLoadingMode(path, refactorableFunction);
        }

        await AceToolWindow.ShowAsync();

        _cancellationTokenSource = new CancellationTokenSource();

        // Run on background thread:
        Task.Run(() => DoRefactorAndUpdateViewAsync(Path, RefactorableFunction, entryPoint, _cancellationTokenSource.Token), _cancellationTokenSource.Token).FireAndForget();
    }

    public void HandleCancel()
    {
        try
        {
            if (_cancellationTokenSource != null && _cancellationTokenSource.Token.CanBeCanceled)
            {
                _logger.Debug("Cancelling ACE refactoring");
                _cancellationTokenSource.Cancel();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error ocurred during ACE refactoring cancellation", ex);
        }
    }

    private void SetViewToLoadingMode(string path, FnToRefactorModel refactorableFunction)
    {
        AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
        {
            MessageType = MessageTypes.UPDATERENDERER,
            Payload = new WebComponentPayload<AceComponentData>
            {
                IdeType = VISUALSTUDIOIDETYPE,
                View = ViewTypes.ACE,
                Data = _mapper.Map(path, refactorableFunction),
            },
        });
    }

    private async Task DoRefactorAndUpdateViewAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var refactored = _aceManager.Refactor(path: path, refactorableFunction: refactorableFunction, entryPoint);
            AceComponentData data;
            if (refactored != null)
            {
                data = _mapper.Map(refactored);
            }
            else
            {
                data = _mapper.Map(path, refactorableFunction, AceViewErrorTypes.GENERIC);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
            {
                MessageType = MessageTypes.UPDATERENDERER,
                Payload = new WebComponentPayload<AceComponentData>
                {
                    IdeType = VISUALSTUDIOIDETYPE,
                    View = ViewTypes.ACE,
                    Data = data,
                },
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Error ocurred during ACE refactoring", ex);

            // Determine error type based on exception
            string errorType = DetermineErrorType(ex);

            AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
            {
                MessageType = MessageTypes.UPDATERENDERER,
                Payload = new WebComponentPayload<AceComponentData>
                {
                    IdeType = VISUALSTUDIOIDETYPE,
                    View = ViewTypes.ACE,
                    Data = _mapper.Map(path, refactorableFunction, errorType),
                },
            });
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private string DetermineErrorType(Exception ex)
    {
        if (ex != null)
        {
            var isMissingToken = ex is MissingAuthTokenException;
            var isAuthError = ex is DevtoolsException exception && exception.Status == 401;
            if (isMissingToken || isAuthError)
            {
                return AceViewErrorTypes.AUTH;
            }
        }

        return AceViewErrorTypes.GENERIC;
    }
}
