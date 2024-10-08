using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.MethodIssue
{
    public class MethodIssueDataPoint : IAsyncCodeLensDataPoint
    {
        private readonly ICodeLensCallbackService _callbackService;
        public readonly string DataPointId = Guid.NewGuid().ToString();


        public MethodIssueDataPoint(
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
            var fileReview = await _callbackService
                .InvokeAsync<CsReview>(
                    this,
                    nameof(ICodeLevelMetricsCallbackService.GetFileReviewData),
                    cancellationToken: token
                )
                .ConfigureAwait(false);
            return new CodeLensDataPointDescriptor
            {
                Description = $"{fileReview.Review.FirstOrDefault().Category}",
            };
        }

        public Task<CodeLensDetailsDescriptor> GetDetailsAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var result = new CodeLensDetailsDescriptor()
            {
                CustomData = new List<CustomDetailsData>{
                    new CustomDetailsData { FileName = "complex-conditional", Title = "Complex Conditional"}
                }
            };
            return Task.FromResult(result);
        }

        public CodeLensDescriptor Descriptor { get; }
        public event AsyncEventHandler InvalidatedAsync;

        public void Refresh() =>
            _ = InvalidatedAsync?.InvokeAsync(this, EventArgs.Empty).ConfigureAwait(false);
    }
}
