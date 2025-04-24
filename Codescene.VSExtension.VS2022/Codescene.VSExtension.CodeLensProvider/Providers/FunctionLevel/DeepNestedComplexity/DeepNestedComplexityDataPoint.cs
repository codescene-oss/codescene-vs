using Codescene.VSExtension.CodeLensProvider.Abstraction;
using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.DeepNestedComplexity
{
    public class DeepNestedComplexityDataPoint : BaseDataPoint
    {
        public DeepNestedComplexityDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }
        public override Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                ImageId = Constants.Images.WarningImageId,
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
