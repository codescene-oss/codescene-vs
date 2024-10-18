using CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace CodeLensProvider.Providers.FunctionLevel.ComplexMethod
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(ComplexMethodDataPointProvider))]
    [ContentType(Constants.DATA_POINT_PROVIDER_CONTENT_TYPE)]
    [Priority(100)]
    public class ComplexMethodDataPointProvider : BaseDataPointProvider<ComplexMethodDataPoint>
    {
        public override string Name => Constants.Titles.COMPLEX_METHOD;

        [ImportingConstructor]
        public ComplexMethodDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
