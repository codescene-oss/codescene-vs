using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.Core.Application.Services.Codelens;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.ACE
{
    public sealed class CodesceneAceDataPoint : BaseDataPoint
    {
        public CodesceneAceDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }
        public override Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                ImageId = Constants.Images.WarningImageId,
                Description = Constants.Titles.CODESCENE_ACE
            });
        }

        public override async Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext ctx, CancellationToken token)
        {
            await CallbackService.InvokeAsync<object>(this, nameof(ICodesceneCodelensCallbackService.OpenAceToolWindowAsync), cancellationToken: token);
            return null;
        }
    }
}
