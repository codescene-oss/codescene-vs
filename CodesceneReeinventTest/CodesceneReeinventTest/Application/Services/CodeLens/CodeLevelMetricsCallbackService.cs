using CodeLensShared;
using CodesceneReeinventTest.Application.Services.FileReviewer;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;

namespace CodesceneReeinventTest.Application.Services.CodeLens;

[Export(typeof(ICodeLensCallbackListener))]
[PartCreationPolicy(CreationPolicy.Shared)]
[ContentType("CSharp")]
internal class CodeLevelMetricsCallbackService : ICodeLensCallbackListener, ICodeLevelMetricsCallbackService
{
    public static readonly ConcurrentDictionary<string, CodeLensConnection> Connections =
           new ConcurrentDictionary<string, CodeLensConnection>();
    [Import(typeof(IFileReviewer))]
    private IFileReviewer _fileReviewer;
    public async Task<string> GetFileCodeHealth()
    {
        DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

        var filePath = docView.FilePath;

        var review = _fileReviewer.Review(filePath);
        return (review.Score == 0 ? "No application code detected for scoring" : review.Score.ToString() + "/10");

    }

    public int GetVisualStudioPid()
    {
        return Process.GetCurrentProcess().Id;
    }

    public async Task InitializeRpcAsync(string dataPointId)
    {
        var stream = new NamedPipeServerStream(
            RpcPipeNames.ForCodeLens(Process.GetCurrentProcess().Id),
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

