using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.MethodIssue
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name("MethodIssueDataPointProvider")]
    [ContentType("csharp")]
    [Priority(100)]
    internal class MethodIssueDataPointProvider : IAsyncCodeLensDataPointProvider
    {
        private readonly Lazy<ICodeLensCallbackService> _callbackService;

        [ImportingConstructor]
        public MethodIssueDataPointProvider(Lazy<ICodeLensCallbackService> callbackService)
        {
            _callbackService = callbackService;
        }
        public async Task<bool> CanCreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var fileReview = await _callbackService.Value
               .InvokeAsync<bool>(
                   this,
                   nameof(ICodeLevelMetricsCallbackService.HasComplexConditionalIssue),
                   new object[]
                   {
                       descriptor.ProjectGuid,
                       descriptor.ElementDescription,
                       descriptor.FilePath,
                       descriptorContext.ApplicableSpan.Value.Start,
                       descriptorContext.ApplicableSpan.Value.End,
                       descriptorContext.Properties.Values
                   },
                   cancellationToken: token
               )
               .ConfigureAwait(false);
            var methodsOnly = descriptor.Kind == CodeElementKinds.Method;

            return (methodsOnly
                && fileReview
 && await _callbackService.Value.InvokeAsync<bool>(this, nameof(ICodeLevelMetricsCallbackService.IsCodeSceneLensesEnabled))
                );
        }
        public async Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(
            CodeLensDescriptor descriptor,
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var dataPoint = new MethodIssueDataPoint(descriptor, _callbackService.Value);

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
