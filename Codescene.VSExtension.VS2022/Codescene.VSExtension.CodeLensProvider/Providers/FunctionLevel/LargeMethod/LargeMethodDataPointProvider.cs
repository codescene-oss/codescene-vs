using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.LargeMethod
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(LargeMethodDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_JS)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [Priority(1060)]
    public class LargeMethodDataPointProvider : BaseDataPointProvider<LargeMethodDataPoint>
    {
        public override string Name => Constants.Titles.LARGE_METHOD;

        [ImportingConstructor]
        public LargeMethodDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
