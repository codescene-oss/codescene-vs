using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.ExpressionLevel.ComplexConditional
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(ComplexConditionalDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(990)]
    public class ComplexConditionalDataPointProvider : BaseDataPointProvider<ComplexConditionalDataPoint>
    {
        public override string Name => Constants.Titles.COMPLEX_CONDITIONAL;

        [ImportingConstructor]
        public ComplexConditionalDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
