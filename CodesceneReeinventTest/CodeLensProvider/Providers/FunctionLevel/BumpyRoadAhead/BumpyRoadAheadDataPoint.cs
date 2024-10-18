using CodeLensProvider.Providers.Base;
using CodeLensShared;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeLensProvider.Providers.FunctionLevel.BumpyRoadAhead
{
    public class BumpyRoadAheadDataPoint : BaseDataPoint
    {
        public BumpyRoadAheadDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService) : base(descriptor, callbackService) { }
        public override Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            return Task.FromResult(new CodeLensDataPointDescriptor
            {
                Description = Constants.Titles.BUMPY_ROAD_AHEAD
            });
        }

        public override Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
        {
            var result = new CodeLensDetailsDescriptor()
            {
                CustomData = new List<CustomDetailsData>{
                    new CustomDetailsData
                    {
                        FileName = "bumpy-road-ahead",
                        Title = Constants.Titles.BUMPY_ROAD_AHEAD
                    }
                }
            };
            return Task.FromResult(result);
        }
    }
}
