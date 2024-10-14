using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.Base
{
    public abstract class BaseDataPoint : IBaseDataPoint
    {
        public CodeLensDescriptor Descriptor { get; private set; }
        public ICodeLensCallbackService CallbackService { get; private set; }
        public string DataPointId { get; }
        public event AsyncEventHandler InvalidatedAsync;
        public VisualStudioConnection VsConnection { get; set; }

        public BaseDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService)
        {
            Descriptor = descriptor;
            CallbackService = callbackService;
            DataPointId = Guid.NewGuid().ToString();
        }

        public abstract Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token);
        public abstract Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token);

        public void Refresh() => _ = InvalidatedAsync?.InvokeAsync(this, EventArgs.Empty).ConfigureAwait(false);
    }
}
