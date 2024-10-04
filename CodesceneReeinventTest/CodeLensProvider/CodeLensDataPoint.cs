using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider
{
    public class CodeLensDataPoint : IAsyncCodeLensDataPoint
    {
        private readonly ICodeLensCallbackService _callbackService;
        public readonly string DataPointId = Guid.NewGuid().ToString();

        public VisualStudioConnection VsConnection;

        public CodeLensDataPoint(
            CodeLensDescriptor descriptor,
            ICodeLensCallbackService callbackService
        )
        {
            _callbackService = callbackService;
            Descriptor = descriptor;
        }

        public async Task<CodeLensDataPointDescriptor> GetDataAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var fileCodeHealth = await _callbackService
                .InvokeAsync<string>(
                    this,
                    nameof(ICodeLevelMetricsCallbackService.GetFileCodeHealth),
                    cancellationToken: token
                )
                .ConfigureAwait(false);

            return new CodeLensDataPointDescriptor
            {
                Description = $"Code health score: {fileCodeHealth}!",
                TooltipText = $"Code health score: {fileCodeHealth}",
            };
        }

        public Task<CodeLensDetailsDescriptor> GetDetailsAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            //open markdown here
            return Task.FromResult<CodeLensDetailsDescriptor>(null);
        }

        public CodeLensDescriptor Descriptor { get; }
        public event AsyncEventHandler InvalidatedAsync;

        public void Refresh() =>
            _ = InvalidatedAsync?.InvokeAsync(this, EventArgs.Empty).ConfigureAwait(false);
    }
}
