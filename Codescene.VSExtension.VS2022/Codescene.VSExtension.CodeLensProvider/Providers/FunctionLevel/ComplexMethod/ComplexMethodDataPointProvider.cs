using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.ComplexMethod
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(ComplexMethodDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_JS)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [Priority(1020)]
    public class ComplexMethodDataPointProvider : BaseDataPointProvider<ComplexMethodDataPoint>
    {
        public override string Name => Constants.Titles.COMPLEX_METHOD;

        [ImportingConstructor]
        public ComplexMethodDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
