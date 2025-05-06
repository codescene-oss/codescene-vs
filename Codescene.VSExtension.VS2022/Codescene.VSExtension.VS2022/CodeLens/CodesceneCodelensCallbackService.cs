using Codescene.VSExtension.CodeLensProvider.Abstraction;
using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
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
[ContentType(Constants.CONTENT_TYPE_CSHARP)]
[ContentType(Constants.CONTENT_TYPE_JAVA)]
[ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
[ContentType(Constants.CONTENT_TYPE_JAVASCRIPT)]
internal class CodesceneCodelensCallbackService : ICodeLensCallbackListener, ICodesceneCodelensCallbackService
{
    public static readonly ConcurrentDictionary<string, CodeLensConnection> Connections = new();

    [Import]
    private readonly ICodeReviewer _reviewer;

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
            return review.FunctionLevel.Any(x => x.StartLine == lineNumber);
        }

        return review.FunctionLevel.Any(x => x.Category == issue && x.StartLine == lineNumber);
    }


    public bool IsCodeSceneLensesEnabled()
    {
        return General.Instance.EnableCodeLenses;
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

    //public async Task OpenAceToolWindowAsync()
    //{
    //    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
    //    await AceToolWindow.ShowAsync();
    //}

    //public Task OpenAceToolWindowAsync(object descriptor, object context)
    //{
    //    var desc = (CodeLensDescriptor)descriptor;
    //    var ctx = (CodeLensDescriptorContext)context;
    //    return null;
    //}

    public async Task OpenAceToolWindowAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context)
    {
        context.Properties.TryGetValue("StartLine", out var startLine);
        context.Properties.TryGetValue("StartColumn", out var startColumn);
        context.Properties.TryGetValue("FullyQualifiedName", out var fullyQualifiedName);
        //var path = descriptor.FilePath;
        var path = "C:\\Users\\User\\source\\repos\\codescene-vs\\Codescene.VSExtension.VS2022\\Codescene.VSExtension.CodeSmells\\Issues\\Javascript\\DeepGlobalNestedComplexityExample.js";
        var kind = descriptor.Kind;
        var elementDescription = descriptor.ElementDescription;
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        using (var reader = File.OpenText(path))
        {
            var content = await reader.ReadToEndAsync();
            var refactored = await _reviewer.Refactor(path: path, content: content);
        }

        await AceToolWindow.ShowAsync();
    }

    public bool ThrowException(Exception ex)
    {
        var m = ex.Message;
        return true;
    }
}