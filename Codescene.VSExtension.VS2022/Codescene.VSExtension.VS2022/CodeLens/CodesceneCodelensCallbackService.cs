using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Codelens;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Codescene.VSExtension.VS2022.CodeLens;

[Export(typeof(ICodeLensCallbackListener))]
[PartCreationPolicy(CreationPolicy.Shared)]
[ContentType("CSharp")]
internal class CodesceneCodelensCallbackService : ICodeLensCallbackListener, ICodesceneCodelensCallbackService
{
    public static readonly ConcurrentDictionary<string, CodeLensConnection> Connections = new();
    public static bool CodeSceneLensesEnabled;
    //private readonly DocumentEvents _documentEvents;

    [Import]
    private readonly ICliExecuter _cliExecuter;

    public CodesceneCodelensCallbackService()
    {
        //listen to events
        //_documentEvents = VS.Events.DocumentEvents;
        //_documentEvents.Closed += OnDocumentClosed;
        //_documentEvents.Opened += OnDocumentsOpened;
        //_documentEvents.Saved += OnDocumentsSaved;
    }

    private static readonly Dictionary<string, ReviewResultModel> ActiveReviewList = [];

    private ITextView _textView; // Add this to hold the current text view
    private Timer _timer;
    private readonly int _delayInMilliseconds = 3000;
    //private async void SubscribeToChangeEvent()
    //{
    //    var temp = await VS.Documents.GetActiveDocumentViewAsync();
    //    temp.Document.TextBuffer.Changed += TextBuffer_Changed;
    //}
    //private async void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    //{
    //    var temp = await VS.Documents.GetActiveDocumentViewAsync();
    //    _timer?.Change(Timeout.Infinite, Timeout.Infinite); // Stop the timer if already running
    //    _timer = new Timer(async _ => OnDocumentsSaved(temp.FilePath), null, _delayInMilliseconds, Timeout.Infinite);
    //}
    //private async void OnDocumentsSaved(string filePath)
    //{
    //    System.Diagnostics.Debug.WriteLine($"OnDocumentsSaved called with filePath: {filePath}");
    //    _cliExecuter.RemoveFromActiveReviewList(filePath);

    //    _cliExecuter.AddToActiveReviewList(filePath);
    //    await RefreshAllCodeLensDataPointsAsync();
    //}

    //private void OnDocumentsOpened(string filePath)
    //{
    //    System.Diagnostics.Debug.WriteLine($"OnDocumentsOpened called with filePath: {filePath}");
    //    //SubscribeToChangeEvent();
    //    _cliExecuter.AddToActiveReviewList(filePath);
    //    AddWarnings(filePath);

    //}
    //private void OnDocumentClosed(string filePath)
    //{
    //    System.Diagnostics.Debug.WriteLine($"OnDocumentClosed called with filePath: {filePath}");
    //    _cliExecuter.RemoveFromActiveReviewList(filePath);
    //}

    public float GetFileReviewScore(string filePath)
    {
        var review = _cliExecuter.GetReviewObject(filePath);

        return review.Score;
    }
    public bool ShowCodeLensForIssue(string issue, string filePath, int startLine, dynamic obj)
    {
        var review = _cliExecuter.GetReviewObject(filePath);

        if (review.FunctionLevel.Any(x => x.Category == issue && x.StartLine == startLine)) return true;

        return false;

    }
    public bool IsCodeSceneLensesEnabled() => General.Instance.EnableCodeLenses;
    public int GetVisualStudioPid() => System.Diagnostics.Process.GetCurrentProcess().Id;

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

    public static async Task RefreshAllCodeLensDataPointsAsync()
    {
        await Task.WhenAll(Connections.Keys.Select(RefreshCodeLensDataPointAsync))
            .ConfigureAwait(false);
    }
}