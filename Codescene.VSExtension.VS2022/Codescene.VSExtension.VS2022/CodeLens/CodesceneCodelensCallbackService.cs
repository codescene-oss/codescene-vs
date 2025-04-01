using Codescene.VSExtension.Core.Application.Services.Codelens;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Concurrent;
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
    private readonly ICodeReviewer _reviewer;

    public CodesceneCodelensCallbackService()
    {
        var emir = "";
        //listen to events
        //_documentEvents = VS.Events.DocumentEvents;
        //_documentEvents.Closed += OnDocumentClosed;
        //_documentEvents.Opened += OnDocumentsOpened;
        //_documentEvents.Saved += OnDocumentsSaved;
    }

    //private static readonly Dictionary<string, CliReviewModel> ActiveReviewList = [];

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
        var review = _reviewer.Review(filePath);

        return review.Score;
    }

    /// <summary>
    /// This method is invoked by the Codelens Data Point Provider. It iterates through the file's code lines and calls this method for each line. The method checks whether the reviewer has found an issue on the line it was called for.
    /// </summary>
    /// <param name="issue"></param>
    /// <param name="filePath"></param>
    /// <param name="lineNumber"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public bool ShowCodeLensForLine(string issue, string filePath, int lineNumber, dynamic obj)
    {
        var review = _reviewer.Review(filePath);

        if (review.FunctionLevel.Any(x => x.Category == issue && x.StartLine == lineNumber))
        {
            return true;
        }

        return false;

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

    public static async Task RefreshCodeLensDataPointAsync(string dataPointId)
    {
        if (!Connections.TryGetValue(dataPointId, out var connectionHandler))
        {
            throw new InvalidOperationException(
                $"CodeLens data point {dataPointId} was not registered."
            );
        }

        await connectionHandler.Rpc.InvokeAsync(nameof(IRemoteCodeLens.Refresh)).ConfigureAwait(false);
    }

    public static async Task RefreshAllCodeLensDataPointsAsync()
    {
        await Task.WhenAll(Connections.Keys.Select(RefreshCodeLensDataPointAsync)).ConfigureAwait(false);
    }
}