using CodeLensProvider.Providers.Base;
using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.FunctionLevel.DeepNestedComplexity
{
    public class DeepNestedComplexityDataPoint : BaseDataPoint
    {
        public DeepNestedComplexityDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }
        public override Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                Description = Constants.Titles.DEEP_NESTED_COMPLEXITY
            });
        }

        public override Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var result = new CodeLensDetailsDescriptor()
            {
                CustomData = new List<CustomDetailsData>{
                    new CustomDetailsData
                    {
                        FileName = "deep-nested-complexity",
                        Title = Constants.Titles.DEEP_NESTED_COMPLEXITY
                    }
                }
            };
            return Task.FromResult(result);
        }
    }
}
