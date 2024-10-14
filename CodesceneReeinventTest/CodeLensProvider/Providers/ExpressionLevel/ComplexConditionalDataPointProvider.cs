using CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace CodeLensProvider.Providers.ExpressionLevel
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(ComplexConditionalDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(101)]
    public class ComplexConditionalDataPointProvider : BaseDataPointProvider<ComplexConditionalDataPoint>
    {
        public override string Name => "Complex Conditional";

        [ImportingConstructor]
        public ComplexConditionalDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
