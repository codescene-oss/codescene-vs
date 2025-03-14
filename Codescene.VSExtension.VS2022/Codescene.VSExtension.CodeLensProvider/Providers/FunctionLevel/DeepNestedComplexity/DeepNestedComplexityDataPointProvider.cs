using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.DeepNestedComplexity
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(DeepNestedComplexityDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(100)]
    public class DeepNestedComplexityDataPointProvider : BaseDataPointProvider<DeepNestedComplexityDataPoint>
    {
        public override string Name => Constants.Titles.DEEP_NESTED_COMPLEXITY;

        [ImportingConstructor]
        public DeepNestedComplexityDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
