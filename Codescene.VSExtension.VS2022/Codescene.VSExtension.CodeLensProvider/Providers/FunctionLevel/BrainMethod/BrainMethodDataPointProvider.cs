using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.BrainMethod
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(BrainMethodDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [ContentType(Constants.CONTENT_TYPE_JAVASCRIPT)]
    [Priority(1040)]
    public class BrainMethodDataPointProvider : BaseDataPointProvider<BrainMethodDataPoint>
    {
        public override string Name => Constants.Titles.BRAIN_METHOD;

        [ImportingConstructor]
        public BrainMethodDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
