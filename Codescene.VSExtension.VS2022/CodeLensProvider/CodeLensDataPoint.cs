using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider
{
    public class CodeLensDataPoint : IAsyncCodeLensDataPoint
    {
        public CodeLensDescriptor Descriptor { get; }
        public event AsyncEventHandler InvalidatedAsync;

        public CodeLensDataPoint(CodeLensDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                Description = "Shows Up Inline",
                //ImageId = Shows an image next to the Code Lens entry
                //IntValue = I haven't figured this one out yet!
                TooltipText = "Shows Up On Hover"
            });
        }

        public Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            // this is what gets triggered when you click a Code Lens entry, and we don't really care about this part for now
            return Task.FromResult<CodeLensDetailsDescriptor>(null);
        }
    }
}
