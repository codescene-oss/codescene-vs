using CodeLensProvider.Providers.Base;
using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.ExpressionLevel
{
    public class ComplexConditionalDataPoint : BaseDataPoint
    {
        public ComplexConditionalDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }

        public override Task<CodeLensDataPointDescriptor> GetDataAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                Description = $"Complex Conditional"
            });
        }

        public override Task<CodeLensDetailsDescriptor> GetDetailsAsync(
            CodeLensDescriptorContext descriptorContext,
            CancellationToken token
        )
        {
            var result = new CodeLensDetailsDescriptor()
            {
                CustomData = new List<CustomDetailsData>{
                    new CustomDetailsData
                    {
                        FileName = "complex-conditional",
                        Title = "Complex Conditional"
                    }
                }
            };
            return Task.FromResult(result);
        }
    }
}
