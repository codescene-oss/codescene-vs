using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.FunctionLevel
{
    public class ExcessNumberOfFunctionArgumentsDataPoint : IAsyncCodeLensDataPoint
    {
        public readonly string DataPointId = Guid.NewGuid().ToString();
        public VisualStudioConnection VsConnection;

        public ExcessNumberOfFunctionArgumentsDataPoint(
           CodeLensDescriptor descriptor
        )
        {
            Descriptor = descriptor;
        }
        public async Task<CodeLensDataPointDescriptor> GetDataAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            return new CodeLensDataPointDescriptor
            {
                Description = $"Excess Number od Function Arguments"
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
                    new CustomDetailsData
                    {
                        FileName = "excess-number-of-function-arguments",
                        Title = "Excess Number od Function Arguments"
                    }
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