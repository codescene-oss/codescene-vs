using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.BumpyRoadAhead
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(BumpyRoadAheadDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_JS)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [Priority(1000)]
    public class BumpyRoadAheadDataPointProvider : BaseDataPointProvider<BumpyRoadAheadDataPoint>
    {
        public override string Name => Constants.Titles.BUMPY_ROAD_AHEAD;

        [ImportingConstructor]
        public BumpyRoadAheadDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
