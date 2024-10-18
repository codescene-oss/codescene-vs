using CodeLensProvider.Providers.Base;
using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.FunctionLevel.ExcessNumberOfFunctionArguments
{
    public class ExcessNumberOfFunctionArgumentsDataPoint : BaseDataPoint
    {
        public ExcessNumberOfFunctionArgumentsDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }
        public override Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                Description = Constants.Titles.EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS
            });
        }

        public override Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var result = new CodeLensDetailsDescriptor()
            {
                CustomData = new List<CustomDetailsData>{
                    new CustomDetailsData
                    {
                        FileName = "excess-number-of-function-arguments",
                        Title = Constants.Titles.EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS
                    }
                }
            };
            return Task.FromResult(result);
        }
    }
}