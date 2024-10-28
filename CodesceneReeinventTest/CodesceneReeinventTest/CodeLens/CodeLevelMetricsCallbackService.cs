using CodeLensShared;
using Core.Application.Services.FileReviewer;
using Core.Models.ReviewResultModel;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Linq;
using System.Threading;


namespace CodesceneReeinventTest.CodeLens;

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

    private static readonly Dictionary<string, ReviewResultModel> ActiveReviewList = [];

    [Import]
    internal ITextDocumentFactoryService _textDocumentFactoryService { get; set; }

    private IVsTextManager _textManager { get; set; }
    private ITextView _textView; // Add this to hold the current text view
    private Timer _timer;
    private readonly int _delayInMilliseconds = 3000;

    public CodeLevelMetricsCallbackService()
    {
        _textManager = ServiceProvider.GlobalProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;

        //listen to events
        _documentEvents = VS.Events.DocumentEvents;
        _documentEvents.Closed += OnDocumentClosed;
        _documentEvents.Opened += OnDocumentsOpened;
        _documentEvents.Saved += OnDocumentsSaved;
    }
    private async void SubscribeToChangeEvent()
    {
        var temp = await VS.Documents.GetActiveDocumentViewAsync();
        temp.Document.TextBuffer.Changed += TextBuffer_Changed;
    }
    private async void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
        var temp = await VS.Documents.GetActiveDocumentViewAsync();
        _timer?.Change(Timeout.Infinite, Timeout.Infinite); // Stop the timer if already running
        _timer = new Timer(async _ => OnDocumentsSaved(temp.FilePath), null, _delayInMilliseconds, Timeout.Infinite);
    }
    private async void OnDocumentsSaved(string filePath)
    {
        _fileReviewer.RemoveFromActiveReviewList(filePath);

        _fileReviewer.AddToActiveReviewList(filePath);
        await RefreshAllCodeLensDataPointsAsync();
    }

    private void OnDocumentsOpened(string filePath)
    {
        SubscribeToChangeEvent();
        _fileReviewer.AddToActiveReviewList(filePath);
        AddWarnings(filePath);

    }
    private void OnDocumentClosed(string filePath)
    {
        _fileReviewer.RemoveFromActiveReviewList(filePath);
    }
    public float GetFileReviewScore(string filePath)
    {
        var review = _fileReviewer.GetReviewObject(filePath);

        return review.Score;
    }
    public bool ShowCodeLensForIssue(string issue, string filePath, int startLine, dynamic obj)
    {
        var review = _fileReviewer.GetReviewObject(filePath);

        if (review.FunctionLevel.Any(x => x.Category == issue && x.StartLine == startLine)) return true;

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


    private async void AddWarnings(string filePath)
    {
        var errorListProvider = new ErrorListProvider(ServiceProvider.GlobalProvider);
        var review = _fileReviewer.GetReviewObject(filePath);

        //foreach (var issues in review.Review)
        //{
        //    foreach (var function in issues.Functions)
        //    {
        //        var errorTask = new ErrorTask
        //        {

        //            Text = issues.Category + " (" + function.Details + ")",
        //            Document = filePath,
        //            Line = function.Startline - 1,
        //            Column = function.Startline,
        //            Category = TaskCategory.BuildCompile,
        //            ErrorCategory = TaskErrorCategory.Warning,
        //        };
        //        errorListProvider.Tasks.Add(errorTask);
        //    }
        //}
        errorListProvider.Show();
    }


}

