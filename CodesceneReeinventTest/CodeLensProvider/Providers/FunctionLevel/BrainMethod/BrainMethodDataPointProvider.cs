using CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace CodeLensProvider.Providers.FunctionLevel.BrainMethod
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(BrainMethodDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(100)]
    public class BrainMethodDataPointProvider : BaseDataPointProvider<BrainMethodDataPoint>
    {
        public override string Name => Constants.Titles.BRAIN_METHOD;

        [ImportingConstructor]
        public BrainMethodDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
