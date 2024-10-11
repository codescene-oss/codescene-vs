using CodeLensShared;
using CodesceneReeinventTest.Application.Services.FileReviewer;
using EnvDTE;
using EnvDTE80;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Linq;

namespace CodesceneReeinventTest.Application.Services.CodeLens;

[Export(typeof(ICodeLensCallbackListener))]
[PartCreationPolicy(CreationPolicy.Shared)]
[ContentType("CSharp")]
internal class CodeLevelMetricsCallbackService : ICodeLensCallbackListener, ICodeLevelMetricsCallbackService
{
    public static readonly ConcurrentDictionary<string, CodeLensConnection> Connections = new();
    public static bool CodeSceneLensesEnabled;
    [Import(typeof(IFileReviewer))]
    private readonly IFileReviewer _fileReviewer;

    private readonly DTE2 _dte;
    private readonly EnvDTE.DocumentEvents _documentEvents;

    private static readonly Dictionary<string, CsReview> ActiveReviewList = [];

    public CodeLevelMetricsCallbackService()
    {
        _dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
        //listen to events
        _documentEvents = _dte.Events.DocumentEvents;
        _documentEvents.DocumentClosing += OnDocumentClosed;
        _documentEvents.DocumentOpening += OnDocumentsOpened;
        _documentEvents.DocumentSaved += OnDocumentsSaved;
    }
    private void OnDocumentsSaved(Document document)
    {
        RemoveFromActiveReviewList(document.FullName);

        AddToActiveReviewList(document.FullName);
    }
    private void OnDocumentsOpened(string documentPath, bool readOnly)
    {
        AddToActiveReviewList(documentPath);
    }
    private void OnDocumentClosed(Document document)
    {
        RemoveFromActiveReviewList(document.FullName);
    }
    private void AddToActiveReviewList(string documentPath)
    {
        var review = _fileReviewer.Review(documentPath);
        ActiveReviewList.Add(documentPath, review);
    }
    private void RemoveFromActiveReviewList(string documentPath)
    {
        ActiveReviewList.Remove(documentPath);
    }
    public float GetFileReviewScore(string filePath)
    {
        ActiveReviewList.TryGetValue(filePath, out var review);

        //for already opened files on IDE load
        if (review == null)
        {
            AddToActiveReviewList(filePath);
            ActiveReviewList.TryGetValue(filePath, out review);
        }

        return review.Score;
    }
    public bool ShowCodeLensForIssue(string issue, string filePath, int startLine, dynamic obj)
    {
        ActiveReviewList.TryGetValue(filePath, out var review);

        //for already opened files on IDE load
        if (review == null)
        {
            AddToActiveReviewList(filePath);
            ActiveReviewList.TryGetValue(filePath, out review);
        }
        if (!review.Review.Any(x => x.Category == issue)) return false;

        var listOfFunctions = review.Review.Where(x => x.Category == issue).FirstOrDefault().Functions;

        if (!listOfFunctions.Any(x => x.Startline == startLine)) return false;

        return true;

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
            RpcPipeNames.ForCodeLens(System.Diagnostics.Process.GetCurrentProcess().Id),
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );

        await stream.WaitForConnectionAsync().ConfigureAwait(false);

        var connection = new CodeLensConnection(stream);
        Connections[dataPointId] = connection;
    }
    public static async Task RefreshCodeLensDataPointAsync(string dataPointId)
    {
        if (!Connections.TryGetValue(dataPointId, out var connectionHandler))
        {
            throw new InvalidOperationException(
                $"CodeLens data point {dataPointId} was not registered."
            );
        }

        await connectionHandler
            .Rpc.InvokeAsync(nameof(IRemoteCodeLens.Refresh))
            .ConfigureAwait(false);
    }
    public static async Task RefreshAllCodeLensDataPointsAsync() =>
        await Task.WhenAll(Connections.Keys.Select(RefreshCodeLensDataPointAsync))
            .ConfigureAwait(false);
}

