using Codescene.VSExtension.CodeLensProvider.Abstraction;
using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FileLevel.CodeHealthScore
{
    public class CodeHealthScoreDataPoint : BaseDataPoint
    {
        public CodeHealthScoreDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }

        public override async Task<CodeLensDataPointDescriptor> GetDataAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var fileCodeHealth = await CallbackService
                .InvokeAsync<float>(
                    this,
                    nameof(ICodesceneCodelensCallbackService.GetFileReviewScore),
                    new object[]
                    {
                        Descriptor.FilePath
                    },
                    cancellationToken: token
                )
                .ConfigureAwait(false);

            var text = $"Code health score: {(fileCodeHealth != 0 ? fileCodeHealth.ToString() + "/10" : "No application code detected for scoring")}";
            return new CodeLensDataPointDescriptor
            {
                ImageId = Constants.Images.HeartbeatImageId,
                Description = text,
                TooltipText = text
            };
        }

        public override Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
            => Task.FromResult(new CodeLensDetailsDescriptor() { CustomData = new List<CustomDetailsData> { new CustomDetailsData { FileName = "general-code-health", Title = "General Code Health" } } });
    }
}
