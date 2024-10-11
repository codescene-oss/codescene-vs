using CodeLensShared;
using CodesceneReeinventTest.Application.Services.FileReviewer;
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

    private readonly DocumentEvents _documentEvents;

    private static readonly Dictionary<string, CsReview> ActiveReviewList = [];

    public CodeLevelMetricsCallbackService()
    {
        //listen to events
        _documentEvents = VS.Events.DocumentEvents;
        _documentEvents.Closed += OnDocumentClosed;
        _documentEvents.Opened += OnDocumentsOpened;
        _documentEvents.Saved += OnDocumentsSaved;
    }

    private void OnDocumentsSaved(string filePath)
    {
        RemoveFromActiveReviewList(filePath);

        AddToActiveReviewList(filePath);
    }
    private void OnDocumentsOpened(string filePath)
    {
        AddToActiveReviewList(filePath);
    }
    private void OnDocumentClosed(string filePath)
    {
        RemoveFromActiveReviewList(filePath);
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
    private CsReview GetReviewObject(string filePath)
    {
        ActiveReviewList.TryGetValue(filePath, out var review);

        //for already opened files on IDE load
        if (review == null)
        {
            AddToActiveReviewList(filePath);
            ActiveReviewList.TryGetValue(filePath, out review);
        }
        return review;
    }
    public float GetFileReviewScore(string filePath)
    {
        var review = GetReviewObject(filePath);

        return review.Score;
    }
    public bool ShowCodeLensForIssue(string issue, string filePath, int startLine, dynamic obj)
    {
        var review = GetReviewObject(filePath);

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

