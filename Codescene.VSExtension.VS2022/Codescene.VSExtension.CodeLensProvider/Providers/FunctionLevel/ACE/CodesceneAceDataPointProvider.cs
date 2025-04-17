using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.CodeLensProvider.Providers.FunctionLevel.ACE
{

    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(nameof(CodesceneAceDataPointProvider))]
    [ContentType(Constants.CONTENT_TYPE_CSHARP)]
    [ContentType(Constants.CONTENT_TYPE_JAVA)]
    [ContentType(Constants.CONTENT_TYPE_TYPESCRIPT)]
    [ContentType(Constants.CONTENT_TYPE_JAVASCRIPT)]
    [Priority(980)]
    public class CodesceneAceDataPointProvider : BaseDataPointProvider<CodesceneAceDataPoint>
    {
        public override string Name => Constants.Titles.CODESCENE_ACE;

        [ImportingConstructor]
        public CodesceneAceDataPointProvider(Lazy<ICodeLensCallbackService> callbackService) : base(callbackService) { }
    }
}
