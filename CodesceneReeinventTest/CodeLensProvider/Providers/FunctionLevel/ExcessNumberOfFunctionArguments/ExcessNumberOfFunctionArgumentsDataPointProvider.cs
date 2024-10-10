using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.FunctionLevel
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name("ExcessNumberOfFunctionArgumentsDataPointProvider")]
    [ContentType("csharp")]
    [Priority(100)]
    internal class ExcessNumberOfFunctionArgumentsDataPointProvider : IAsyncCodeLensDataPointProvider
    {
        private readonly Lazy<ICodeLensCallbackService> _callbackService;

        [ImportingConstructor]
        public ExcessNumberOfFunctionArgumentsDataPointProvider(Lazy<ICodeLensCallbackService> callbackService)
        {
            _callbackService = callbackService;
        }
        public async Task<bool> CanCreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var methodsOnly = descriptor.Kind == CodeElementKinds.Method;
            if (!methodsOnly) return false;

            var codeSceneLensesEnabled = await _callbackService.Value
                .InvokeAsync<bool>(
                this,
                nameof(ICodeLevelMetricsCallbackService.IsCodeSceneLensesEnabled));
            if (!codeSceneLensesEnabled) return false;

            descriptorContext.Properties.TryGetValue("StartLine", out dynamic startLineObject);

            var showCodeLens = await _callbackService.Value
               .InvokeAsync<bool>(
                   this,
                   nameof(ICodeLevelMetricsCallbackService.ShowCodeLensForIssue),
                   new object[]
                   {
                       "Excess Number of Function Arguments",
                       descriptor.FilePath,
                       (int)startLineObject + 1,
                       descriptorContext.Properties
                   },
                   cancellationToken: token
               )
               .ConfigureAwait(false);

            return (showCodeLens);
        }
        public async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var dataPoint = new ExcessNumberOfFunctionArgumentsDataPoint(descriptor);

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
