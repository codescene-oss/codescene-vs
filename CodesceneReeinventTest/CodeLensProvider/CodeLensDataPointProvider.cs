using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name("CodeLensDataPointProvider")]
    [ContentType("csharp")]
    [Priority(200)]
    internal class CodeLensDataPointProvider : IAsyncCodeLensDataPointProvider
    {
        private readonly Lazy<ICodeLensCallbackService> _callbackService;

        [ImportingConstructor]
        public CodeLensDataPointProvider(Lazy<ICodeLensCallbackService> callbackService)
        {
            _callbackService = callbackService;
        }

        public Task<bool> CanCreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var methodsOnly = descriptor.Kind == CodeElementKinds.Type;
            return Task.FromResult(methodsOnly);
        }

        /// <summary>
        /// Responsible for creating the actual datapoint and setting up two-way communication over RPC back to the in-process extension
        /// </summary>
        public async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var dataPoint = new CodeLensDataPoint(descriptor, _callbackService.Value);

            var vsPid = await _callbackService
                .Value.InvokeAsync<int>(
                    this,
                    nameof(ICodeLevelMetricsCallbackService.GetVisualStudioPid),
                    cancellationToken: token
                )
                .ConfigureAwait(false);

            _ = _callbackService
                .Value.InvokeAsync(
                    this,
                    nameof(ICodeLevelMetricsCallbackService.InitializeRpcAsync),
                    new[] { dataPoint.DataPointId },
                    token
                )
                .ConfigureAwait(false);

            var connection = new VisualStudioConnection(dataPoint, vsPid);
            await connection.ConnectAsync(token);
            dataPoint.VsConnection = connection;

            return dataPoint;
        }
    }
}
