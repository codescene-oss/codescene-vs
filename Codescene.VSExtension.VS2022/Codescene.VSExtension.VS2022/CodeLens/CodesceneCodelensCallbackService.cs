using Codescene.VSExtension.CodeLensProvider.Abstraction;
using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;


namespace Codescene.VSExtension.VS2022.CodeLens;

[Export(typeof(ICodeLensCallbackListener))]
[ContentType(Constants.SupportedLanguages.CONTENT_TYPE_CSHARP)]
[ContentType(Constants.SupportedLanguages.CONTENT_TYPE_JAVA)]
[ContentType(Constants.SupportedLanguages.CONTENT_TYPE_TYPESCRIPT)]
[ContentType(Constants.SupportedLanguages.CONTENT_TYPE_JAVASCRIPT)]
internal class CodesceneCodelensCallbackService : ICodeLensCallbackListener, ICodesceneCodelensCallbackService
{
    public static readonly ConcurrentDictionary<string, CodeLensConnection> Connections = new();

    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly OnClickRefactoringHandler _onClickRefactoringHandler;

    [Import]
    private readonly IPreflightManager _preflightManager;

    public float GetFileReviewScore(string filePath)
    {
        var review = _reviewer.Review(filePath);

        return review.Score;
    }

    /// <summary>
    /// This method is invoked by the Codelens Data Point Provider. It iterates through the file's code lines and calls this method for each line. The method checks whether the reviewer has found an issue on the line it was called for.
    /// </summary>
    /// <param name="issue"></param>
    /// <param name="filePath"></param>
    /// <param name="lineNumber"></param>
    /// <returns></returns>
    public bool ShowCodeLensForFunction(string issue, string filePath, int lineNumber)
    {
        var review = _reviewer.Review(filePath);

        // If there is any smells this should be shown
        if (issue == Constants.Titles.CODESCENE_ACE)
        {
            var supported = IsSupportedAceCodelenseForCodeSmellsAndLanguage(review, lineNumber, filePath);
            if (!supported)
            {
                return false;
            }

            return review.FunctionLevel.Any(x => x.Range.StartLine == lineNumber);
        }

        return review.FunctionLevel.Any(x => x.Category == issue && x.Range.StartLine == lineNumber);
    }

    private bool IsSupportedAceCodelenseForCodeSmellsAndLanguage(FileReviewModel review, int lineNumber, string path)
    {
        var extension = Path.GetExtension(path);
        if (!_preflightManager.IsSupportedLanguage(extension))
        {
            return false;
        }

        //Get all codesmells found for the given line number
        var codeSmellsFoundForTheLine = review?.FunctionLevel?.Where(x => x.Range.StartLine == lineNumber).Select(x => x.Category);
        if (!codeSmellsFoundForTheLine.Any())
        {
            return false;
        }

        //Is any of those code smells refactorable
        var hasAny = _preflightManager.IsAnyCodeSmellSupported(codeSmellsFoundForTheLine);
        if (!hasAny)
        {
            return false;
        }

        return true;
    }


    public bool IsCodeSceneLensesEnabled()
    {
        return false; // General.Instance.EnableCodeLenses;
    }

    public int GetVisualStudioPid()
    {
        return System.Diagnostics.Process.GetCurrentProcess().Id;
    }

    public async Task InitializeRpcAsync(string dataPointId)
    {
        var stream = new NamedPipeServerStream(
            RpcPipeNames.ForCodeLens(GetVisualStudioPid()),
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );

        await stream.WaitForConnectionAsync().ConfigureAwait(false);

        var connection = new CodeLensConnection(stream);
        Connections[dataPointId] = connection;
    }

    private static async Task RefreshDataPointAsync(string dataPointId)
    {
        if (!Connections.TryGetValue(dataPointId, out var connectionHandler))
        {
            throw new InvalidOperationException(
                $"CodeLens data point {dataPointId} was not registered."
            );
        }

        await connectionHandler.Rpc.InvokeAsync(nameof(IRemoteCodeLens.Refresh)).ConfigureAwait(false);
    }

    public static async Task RefreshCodeLensAsync()
    {
        await Task.WhenAll(Connections.Keys.Select(RefreshDataPointAsync)).ConfigureAwait(false);
    }

    public async Task OpenAceToolWindowAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context)
    {
        /* In order that we need more information from passed parameters
        context.Properties.TryGetValue("StartLine", out var startLine);
        context.Properties.TryGetValue("StartColumn", out var startColumn);
        context.Properties.TryGetValue("FullyQualifiedName", out var fullyQualifiedName);
        var kind = descriptor.Kind;
        var elementDescription = descriptor.ElementDescription;
        */

        var path = descriptor.FilePath;

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        await _onClickRefactoringHandler.HandleAsync(path);
    }

    /// <summary>
    /// For Debug Purpose
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool SendError(string message)
    {
        _logger.Error(message: message, new Exception(message));
        return true;
    }
}